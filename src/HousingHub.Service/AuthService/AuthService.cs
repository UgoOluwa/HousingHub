using System.Security.Cryptography;
using HousingHub.Service.Commons.Mappings;
using Google.Apis.Auth;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Auth;
using HousingHub.Service.Dtos.Customer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.AuthService;

public class AuthService : IAuthService
{
    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenProvider _tokenProvider;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IEmailService _emailService;

    /// <summary>How long a user must wait between verification-email resends.</summary>
    private static readonly TimeSpan ResendVerificationCooldown = TimeSpan.FromMinutes(5);

    /// <summary>How long a user must wait between password-reset email resends.</summary>
    private static readonly TimeSpan PasswordResetCooldown = TimeSpan.FromMinutes(5);

    /// <summary>How long a refresh token stays valid before the user must log in again.</summary>
    private static readonly TimeSpan RefreshTokenValidity = TimeSpan.FromDays(30);

    public AuthService(
        IUnitOfWOrk unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenProvider tokenProvider,
        IMapper mapper,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenProvider = tokenProvider;
        _mapper = mapper;
        _configuration = configuration;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<BaseResponse<CustomerDto>> Register(RegisterCustomerDto request)
    {
        try
        {
            var existingByEmail = await _unitOfWork.CustomerQueries.GetByEmailAsync(request.Email);
            var existing = existingByEmail
                        ?? await _unitOfWork.CustomerQueries.GetByPhoneNumberAsync(request.PhoneNumber);

            if (existing is not null)
            {
                // Do not reveal that the address is taken. Returning "customer already
                // exists" turned registration into an oracle: submit an address, learn
                // whether it has an account here.
                //
                // Instead the real account holder is emailed, which is both the
                // security notification and the recovery path for someone who forgot
                // they had signed up. Best-effort — a mail failure must not change the
                // response, or the timing and outcome would leak the same fact again.
                await SafeSendExistingAccountNoticeAsync(existing);

                return RegistrationAccepted();
            }

            string passwordHash = _passwordHasher.Hash(request.Password);

            var customer = new Customer(
                request.FirstName, request.LastName, request.Email,
                request.PhoneNumber, request.CustomerType, passwordHash)
            {
                EmailVerificationToken = GenerateSecureToken(),
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
                AuthProvider = AuthProvider.Local
            };

            bool inserted = await _unitOfWork.CustomerCommands.InsertAsync(customer);
            if (!inserted)
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage("customer"));

            await _unitOfWork.SaveAsync();

            await _emailService.SendEmailVerificationAsync(customer.Email, customer.FirstName, customer.EmailVerificationToken!);

            // Deliberately returns no customer data. Echoing the created CustomerDto
            // here while the duplicate path returns null would make the two responses
            // trivially distinguishable, undoing the whole point. The client only
            // reads IsSuccessful and navigates using the address it already submitted.
            return RegistrationAccepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Register: {Message}", ex.Message);
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// The single response registration ever gives, whether the address was free or
    /// already taken. Both paths must return this exact value — any difference in
    /// data, flag or wording re-opens the enumeration hole.
    /// </summary>
    private static BaseResponse<CustomerDto> RegistrationAccepted() =>
        new(null, true, string.Empty,
            "Account created. Please check your email to verify your address.");

    /// <summary>
    /// The response resend-verification gives for an unknown address, an
    /// already-verified address, and a genuine send alike.
    /// </summary>
    /// <remarks>
    /// Carries the full cooldown so the client's countdown behaves the same in every
    /// case. One residual signal remains: a request inside an existing cooldown
    /// returns the *remaining* seconds, which differs from the full value an unknown
    /// address gets. Closing that would mean tracking cooldown state for addresses
    /// that have no account, which is its own data problem; the rate limiter makes
    /// exploiting the timing difference expensive in the meantime.
    /// </remarks>
    private static BaseResponse<int> ResendAccepted() =>
        new((int)ResendVerificationCooldown.TotalSeconds, true, string.Empty,
            "Email verification link sent successfully.");

    /// <summary>
    /// Tells an existing account holder that someone tried to register with their
    /// address, or that it is already verified. Swallows failures so the caller's
    /// response never varies.
    /// </summary>
    private async Task SafeSendExistingAccountNoticeAsync(Customer existing)
    {
        try
        {
            await _emailService.SendRegistrationAttemptOnExistingAccountAsync(
                existing.Email, existing.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send existing-account notice to {CustomerId}", existing.Id);
        }
    }

    public async Task<BaseResponse<LoginCustomerResponseDto>> Login(LoginCustomerDto request)
    {
        try
        {
            var emailOrPhone = request.EmailOrPhone.Trim();
            var customer = await _unitOfWork.CustomerQueries.GetByEmailOrPhoneAsync(emailOrPhone);

            if (customer == null)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.InvalidCredentials);

            // Whether a sign-in method is available is decided by the credentials the
            // account actually holds, not by which provider created it — an account can
            // hold both a password and a linked Google identity.
            // Deliberately identical to a wrong password. Distinguishing "this account
            // exists but has no password" confirmed both that the address is registered
            // and that it is a Google-only account — a useful pair of facts for an
            // attacker, and not something a legitimate user needs at this point. The
            // reset flow handles adding a password and says so in its own copy.
            if (string.IsNullOrEmpty(customer.PasswordHash))
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty,
                    ResponseMessages.InvalidCredentials);

            if (!_passwordHasher.Verify(request.Password, customer.PasswordHash))
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.InvalidCredentials);

            // Sign-in is the only moment we legitimately hold the plaintext, so it is
            // the only moment a stored hash can be migrated to current parameters.
            // Best-effort: a failure here must not cost the user their session, since
            // the password they supplied is correct either way.
            if (_passwordHasher.NeedsRehash(customer.PasswordHash))
            {
                try
                {
                    customer.PasswordHash = _passwordHasher.Hash(request.Password);
                    await _unitOfWork.CustomerCommands.UpdateAsync(customer);
                    await _unitOfWork.SaveAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not upgrade password hash for customer {CustomerId}", customer.Id);
                }
            }

            if (!customer.EmailVerified)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.EmailNotVerified);

            string token = _tokenProvider.Create(customer);
            string refreshToken = await IssueRefreshTokenAsync(customer.Id);
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token, refreshToken = refreshToken };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Login: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> VerifyEmail(VerifyEmailRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByEmailAsync(request.Email);

            // Same response as a bad token: an unknown address must not be
            // distinguishable from a wrong code, or this becomes a registration oracle.
            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.EmailVerificationFailed);

            if (customer.EmailVerified)
                return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.EmailAlreadyVerified);

            if (!FixedTimeEquals(customer.EmailVerificationToken, request.Token)
                || customer.EmailVerificationTokenExpiry == null
                || customer.EmailVerificationTokenExpiry < DateTime.UtcNow)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.EmailVerificationFailed);

            customer.EmailVerified = true;
            customer.EmailVerificationToken = null;
            customer.EmailVerificationTokenExpiry = null;

            await _unitOfWork.CustomerCommands.UpdateAsync(customer);
            await _unitOfWork.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.EmailVerificationSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in VerifyEmail: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<string>> ForgotPassword(ForgotPasswordRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByEmailAsync(request.Email);

            // Every branch below returns the exact same response — whether the email
            // doesn't exist or was throttled — so a caller can never distinguish
            // "not registered" from "already got a link within the cooldown."
            if (customer == null)
                return new BaseResponse<string>(null, true, string.Empty, ResponseMessages.PasswordResetTokenSent);

            if (customer.LastPasswordResetRequestedAt is { } lastSent
                && DateTime.UtcNow - lastSent < PasswordResetCooldown)
                return new BaseResponse<string>(null, true, string.Empty, ResponseMessages.PasswordResetTokenSent);

            // Google-only accounts are allowed through: this is how a customer who
            // signed up with Google adds a password so either method works.
            customer.PasswordResetToken = GenerateSecureToken();
            customer.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            customer.LastPasswordResetRequestedAt = DateTime.UtcNow;

            await _unitOfWork.CustomerCommands.UpdateAsync(customer);
            await _unitOfWork.SaveAsync();

            await _emailService.SendPasswordResetAsync(customer.Email, customer.FirstName, customer.PasswordResetToken!);

            return new BaseResponse<string>(null, true, string.Empty, ResponseMessages.PasswordResetTokenSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ForgotPassword: {Message}", ex.Message);
            return new BaseResponse<string>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> ResetPassword(ResetPasswordRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByEmailAsync(request.Email);

            // Same response as an invalid token, so this cannot be used to test whether
            // an address is registered.
            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.PasswordResetFailed);

            if (!FixedTimeEquals(customer.PasswordResetToken, request.Token)
                || customer.PasswordResetTokenExpiry == null
                || customer.PasswordResetTokenExpiry < DateTime.UtcNow)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.PasswordResetFailed);

            customer.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            customer.PasswordResetToken = null;
            customer.PasswordResetTokenExpiry = null;

            // The whole point of a reset is often that someone else has access. Leaving
            // their refresh token valid means the password change achieves nothing.
            await RevokeAllRefreshTokensAsync(customer.Id);

            await _unitOfWork.CustomerCommands.UpdateAsync(customer);
            await _unitOfWork.SaveAsync();

            // Security confirmation so an account owner is alerted if the reset wasn't
            // them. Best-effort: a mail failure must not fail the reset itself.
            await SafeSendPasswordChangedAsync(customer);

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.PasswordResetSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ResetPassword: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> ChangePassword(ChangePasswordRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByIdAsync(request.CustomerId);

            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            // A Google account that has since set a password can change it here; one
            // that has no password yet must go through the reset flow to create it.
            if (string.IsNullOrEmpty(customer.PasswordHash))
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.AccountHasNoPassword);

            if (!_passwordHasher.Verify(request.CurrentPassword, customer.PasswordHash))
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.CurrentPasswordIncorrect);

            customer.PasswordHash = _passwordHasher.Hash(request.NewPassword);

            // Same reasoning as ResetPassword: end every other session so a compromised
            // one cannot survive the password change.
            await RevokeAllRefreshTokensAsync(customer.Id);

            await _unitOfWork.CustomerCommands.UpdateAsync(customer);
            await _unitOfWork.SaveAsync();

            await SafeSendPasswordChangedAsync(customer);

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.PasswordChangeSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChangePassword: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<LoginCustomerResponseDto>> GoogleSignIn(GoogleSignInRequestDto request)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"]! }
            };

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            }
            catch
            {
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.GoogleSignInFailed);
            }

            var customer = await _unitOfWork.CustomerQueries.GetByEmailAsync(payload.Email);

            if (customer != null)
            {
                var link = await LinkGoogleIdentity(customer, payload.Subject, payload.EmailVerified);
                if (link != null)
                    return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, link);
            }

            if (customer == null)
            {
                // Type is chosen by the user in the onboarding step after first sign-in.
                customer = new Customer(
                    payload.GivenName ?? string.Empty,
                    payload.FamilyName ?? string.Empty,
                    payload.Email,
                    string.Empty,
                    CustomerType.Unset,
                    string.Empty)
                {
                    GoogleId = payload.Subject,
                    AuthProvider = AuthProvider.Google,
                    EmailVerified = true
                };

                bool inserted = await _unitOfWork.CustomerCommands.InsertAsync(customer);
                if (!inserted)
                    return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty,
                        ResponseMessages.SetCreationFailureMessage("customer"));

                await _unitOfWork.SaveAsync();
            }

            string token = _tokenProvider.Create(customer);
            string refreshToken = await IssueRefreshTokenAsync(customer.Id);
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token, refreshToken = refreshToken };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GoogleSignIn: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// Links a Google identity onto an existing account so a customer can use either
    /// sign-in method and land on the same record. Returns null on success, or an error
    /// message when linking must be refused.
    ///
    /// SECURITY: linking is only allowed when Google reports the address as verified.
    /// Without that check, anyone able to create an identity-provider account using
    /// someone else's address could sign in and take over their Housing Hub account —
    /// the standard account pre-hijacking attack.
    /// </summary>
    private async Task<string?> LinkGoogleIdentity(Customer customer, string googleId, bool emailVerifiedByGoogle)
    {
        // Already linked to this Google account — nothing to do.
        if (customer.GoogleId == googleId)
            return null;

        if (!string.IsNullOrEmpty(customer.GoogleId))
            return ResponseMessages.GoogleAccountMismatch;

        if (!emailVerifiedByGoogle)
            return ResponseMessages.GoogleEmailNotVerified;

        customer.GoogleId = googleId;

        // Google has proven ownership of the mailbox, so an account that signed up with
        // a password but never confirmed its email is verified by this link.
        customer.EmailVerified = true;

        await _unitOfWork.CustomerCommands.UpdateAsync(customer);
        await _unitOfWork.SaveAsync();

        return null;
    }

    public async Task<BaseResponse<LoginCustomerResponseDto>> GoogleSignInFromClaims(GoogleClaimsDto claims)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByEmailAsync(claims.Email);

            if (customer != null)
            {
                var link = await LinkGoogleIdentity(customer, claims.GoogleId, claims.EmailVerified);
                if (link != null)
                    return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, link);
            }

            if (customer == null)
            {
                // Type is chosen by the user in the onboarding step after first sign-in.
                customer = new Customer(
                    claims.FirstName ?? string.Empty,
                    claims.LastName ?? string.Empty,
                    claims.Email,
                    string.Empty,
                    CustomerType.Unset,
                    string.Empty)
                {
                    GoogleId = claims.GoogleId,
                    AuthProvider = AuthProvider.Google,
                    EmailVerified = true
                };

                bool inserted = await _unitOfWork.CustomerCommands.InsertAsync(customer);
                if (!inserted)
                    return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty,
                        ResponseMessages.SetCreationFailureMessage("customer"));

                await _unitOfWork.SaveAsync();
            }

            string token = _tokenProvider.Create(customer);
            string refreshToken = await IssueRefreshTokenAsync(customer.Id);
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token, refreshToken = refreshToken };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GoogleSignInFromClaims: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// One-time onboarding step for accounts created through an external provider.
    /// Only an Unset account can be assigned a type, so this cannot be replayed to
    /// escalate a Customer into a HouseOwner/Agent (or an Admin).
    /// A fresh JWT is returned because the customer_type claim drives authorization.
    /// </summary>
    public async Task<BaseResponse<LoginCustomerResponseDto>> SetAccountType(Guid customerId, CustomerType customerType)
    {
        try
        {
            if (!customerType.IsSelectableAtOnboarding())
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty,
                    ResponseMessages.InvalidAccountType);

            var customer = await _unitOfWork.CustomerQueries.GetByIdAsync(customerId);

            if (customer == null)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty,
                    ResponseMessages.SetNotFoundMessage("customer"));

            if (customer.CustomerType != CustomerType.Unset)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty,
                    ResponseMessages.AccountTypeAlreadySet);

            customer.CustomerType = customerType;

            await _unitOfWork.CustomerCommands.UpdateAsync(customer);
            await _unitOfWork.SaveAsync();

            string token = _tokenProvider.Create(customer);
            string refreshToken = await IssueRefreshTokenAsync(customer.Id);
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token, refreshToken = refreshToken };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SetAccountType: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token, rotating the refresh token
    /// in the process: the presented token is revoked and a fresh one is issued.
    ///
    /// SECURITY: if a token that's already been revoked (i.e. already used once, or
    /// explicitly revoked) is presented again, every refresh token for that customer
    /// is revoked. A legitimate client always uses the newest token it was issued, so
    /// a revoked token showing up again means it was stolen and the thief and the
    /// legitimate owner are now racing each other — the only safe response is to end
    /// every session and force a fresh login.
    /// </summary>
    public async Task<BaseResponse<LoginCustomerResponseDto>> RefreshToken(string refreshToken)
    {
        try
        {
            string tokenHash = HashToken(refreshToken);
            var existing = await _unitOfWork.RefreshTokenQueries.GetByTokenHashAsync(tokenHash);

            if (existing == null)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.InvalidRefreshToken);

            if (existing.IsRevoked)
            {
                await RevokeAllRefreshTokensAsync(existing.CustomerId);
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.InvalidRefreshToken);
            }

            if (existing.ExpiresAt < DateTime.UtcNow)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.InvalidRefreshToken);

            var customer = await _unitOfWork.CustomerQueries.GetByIdAsync(existing.CustomerId);
            if (customer == null)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.InvalidRefreshToken);

            // NOTE: deliberately does NOT gate on customer.IsActive.
            //
            // IsActive defaults to false and was never set at registration, so every
            // existing customer row has IsActive == false. Refusing to refresh on that
            // basis would sign out the entire user base within one access-token
            // lifetime. The constructor now sets it correctly for new accounts, but
            // existing rows need backfilling first — see docs/data-backfill-required.md.
            //
            // Suspension is enforced instead by revoking the token family at the moment
            // an admin suspends the account (CustomerCommandService.SuspendCustomer),
            // which achieves the same outcome without depending on this field. Once the
            // backfill has run, add the IsActive check here as defence in depth.

            existing.IsRevoked = true;
            await _unitOfWork.RefreshTokenCommands.UpdateAsync(existing);

            string newAccessToken = _tokenProvider.Create(customer);
            string newRefreshToken = await IssueRefreshTokenAsync(customer.Id);

            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = newAccessToken, refreshToken = newRefreshToken };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RefreshToken: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// Ends the presented session by revoking its refresh token.
    /// </summary>
    /// <remarks>
    /// There was previously no server-side logout at all — signing out only cleared
    /// client state, leaving the refresh token valid for its full 30-day life. Anyone
    /// who recovered it from a shared machine, a backup or an XSS payload had a working
    /// session long after the user believed they had left.
    ///
    /// Always reports success: whether the token was already revoked, expired or never
    /// existed, the caller's session is over either way, and distinguishing those cases
    /// would leak whether a token value is real.
    /// </remarks>
    public async Task<BaseResponse<bool>> Logout(string refreshToken, bool allSessions = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.Successful);

            var existing = await _unitOfWork.RefreshTokenQueries.GetByTokenHashAsync(HashToken(refreshToken));

            if (existing is not null)
            {
                if (allSessions)
                {
                    await RevokeAllRefreshTokensAsync(existing.CustomerId);
                }
                else if (!existing.IsRevoked)
                {
                    existing.IsRevoked = true;
                    await _unitOfWork.RefreshTokenCommands.UpdateAsync(existing);
                }

                await _unitOfWork.SaveAsync();
            }

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Logout: {Message}", ex.Message);
            // Never surface a logout failure — the client is discarding its state
            // regardless, and a false error would only encourage a retry loop.
            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.Successful);
        }
    }

    /// <summary>Generates a new refresh token, persists its hash, and returns the raw value to hand to the client.</summary>
    private async Task<string> IssueRefreshTokenAsync(Guid customerId)
    {
        string rawToken = GenerateSecureToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenValidity),
            IsRevoked = false,
            IsActive = true
        };

        await _unitOfWork.RefreshTokenCommands.InsertAsync(refreshToken);
        return rawToken;
    }

    /// <summary>
    /// Public entry point for revoking every session on an account. Used by admin
    /// suspension, which previously left the suspended user's tokens working.
    /// </summary>
    public async Task RevokeAllSessionsAsync(Guid customerId)
    {
        await RevokeAllRefreshTokensAsync(customerId);
        await _unitOfWork.SaveAsync();
    }

    private async Task RevokeAllRefreshTokensAsync(Guid customerId)
    {
        var activeTokens = await _unitOfWork.RefreshTokenQueries.GetActiveByCustomerIdAsync(customerId);
        foreach (var activeToken in activeTokens)
        {
            activeToken.IsRevoked = true;
            await _unitOfWork.RefreshTokenCommands.UpdateAsync(activeToken);
        }
    }

    /// <summary>
    /// Length-independent, constant-time comparison for secret values.
    /// </summary>
    /// <remarks>
    /// Ordinary string equality short-circuits at the first differing character, which
    /// leaks how much of a guess was correct. These tokens are 256-bit so the practical
    /// risk is small, but the correct comparison costs nothing.
    /// </remarks>
    private static bool FixedTimeEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));
    }

    private static string HashToken(string rawToken)
    {
        byte[] hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Resends the email verification link, throttled server-side so the endpoint
    /// cannot be used to spam an inbox (it is unauthenticated by necessity).
    /// Data carries the seconds remaining until the next resend is allowed, so the
    /// client can render an accurate countdown instead of guessing.
    /// </summary>
    public async Task<BaseResponse<int>> ResendEmailVerificationToken(string email)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByEmailAsync(email);

            // Unknown address: same response as a successful send, including the
            // cooldown value so the client countdown behaves identically. Anything
            // else turns this into a registration oracle.
            if (customer == null)
                return ResendAccepted();

            // Already verified: also the same response. Telling the caller "already
            // verified" confirmed both that the address is registered and that it is
            // usable, which is precisely what an attacker enumerating addresses wants.
            //
            // The real account holder is not left confused — they are emailed to say
            // the address is already verified and they can simply sign in.
            if (customer.EmailVerified)
            {
                await SafeSendExistingAccountNoticeAsync(customer);
                return ResendAccepted();
            }

            if (customer.LastVerificationEmailSentAt is { } lastSent)
            {
                var elapsed = DateTime.UtcNow - lastSent;
                if (elapsed < ResendVerificationCooldown)
                {
                    var remaining = (int)Math.Ceiling((ResendVerificationCooldown - elapsed).TotalSeconds);
                    return new BaseResponse<int>(remaining, false, string.Empty,
                        ResponseMessages.ResendVerificationTooSoon(remaining));
                }
            }

            customer.EmailVerificationToken = GenerateSecureToken();
            customer.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
            customer.LastVerificationEmailSentAt = DateTime.UtcNow;

            await _unitOfWork.CustomerCommands.UpdateAsync(customer);
            await _unitOfWork.SaveAsync();

            await _emailService.SendEmailVerificationAsync(customer.Email, customer.FirstName, customer.EmailVerificationToken!);

            return ResendAccepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ResendEmailVerificationToken: {Message}", ex.Message);
            return new BaseResponse<int>(0, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>
    /// Sends the "password changed" security notice without letting a mail failure
    /// fail the password operation that already succeeded.
    /// </summary>
    private async Task SafeSendPasswordChangedAsync(Customer customer)
    {
        try
        {
            await _emailService.SendPasswordChangedAsync(customer.Email, customer.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password-changed email to {Email}", customer.Email);
        }
    }
}
