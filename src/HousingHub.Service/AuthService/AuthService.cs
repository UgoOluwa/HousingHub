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
            bool exists = await _unitOfWork.CustomerQueries.GetByEmailAsync(request.Email) != null
                       || await _unitOfWork.CustomerQueries.GetByPhoneNumberAsync(request.PhoneNumber) != null;

            if (exists)
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.CustomerAlreadyExists);

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

            var dto = _mapper.Map<CustomerDto>(customer);
            return new BaseResponse<CustomerDto>(dto, true, string.Empty,
                ResponseMessages.SetCreationSuccessMessage("customer") + ". Please verify your email.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Register: {Message}", ex.Message);
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ex.Message);
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
            if (string.IsNullOrEmpty(customer.PasswordHash))
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty,
                    string.IsNullOrEmpty(customer.GoogleId)
                        ? ResponseMessages.InvalidCredentials
                        : ResponseMessages.AccountHasNoPassword);

            if (!_passwordHasher.Verify(request.Password, customer.PasswordHash))
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.InvalidCredentials);

            if (!customer.EmailVerified)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.EmailNotVerified);

            string token = _tokenProvider.Create(customer);
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Login: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> VerifyEmail(VerifyEmailRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByEmailAsync(request.Email);

            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (customer.EmailVerified)
                return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.EmailAlreadyVerified);

            if (customer.EmailVerificationToken != request.Token
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
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<string>> ForgotPassword(ForgotPasswordRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByEmailAsync(request.Email);

            if (customer == null)
                return new BaseResponse<string>(null, true, string.Empty, ResponseMessages.PasswordResetTokenSent);

            // Google-only accounts are allowed through: this is how a customer who
            // signed up with Google adds a password so either method works.
            customer.PasswordResetToken = GenerateSecureToken();
            customer.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

            await _unitOfWork.CustomerCommands.UpdateAsync(customer);
            await _unitOfWork.SaveAsync();

            await _emailService.SendPasswordResetAsync(customer.Email, customer.FirstName, customer.PasswordResetToken!);

            return new BaseResponse<string>(null, true, string.Empty, ResponseMessages.PasswordResetTokenSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ForgotPassword: {Message}", ex.Message);
            return new BaseResponse<string>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> ResetPassword(ResetPasswordRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByEmailAsync(request.Email);

            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (customer.PasswordResetToken != request.Token
                || customer.PasswordResetTokenExpiry == null
                || customer.PasswordResetTokenExpiry < DateTime.UtcNow)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.PasswordResetFailed);

            customer.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            customer.PasswordResetToken = null;
            customer.PasswordResetTokenExpiry = null;

            await _unitOfWork.CustomerCommands.UpdateAsync(customer);
            await _unitOfWork.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.PasswordResetSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ResetPassword: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
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

            await _unitOfWork.CustomerCommands.UpdateAsync(customer);
            await _unitOfWork.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.PasswordChangeSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChangePassword: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
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
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GoogleSignIn: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ex.Message);
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
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GoogleSignInFromClaims: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ex.Message);
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
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SetAccountType: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ex.Message);
        }
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

            if (customer == null)
                return new BaseResponse<int>(0, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (customer.EmailVerified)
                return new BaseResponse<int>(0, false, string.Empty, ResponseMessages.EmailAlreadyVerified);

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

            return new BaseResponse<int>((int)ResendVerificationCooldown.TotalSeconds, true, string.Empty,
                "Email verification link sent successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ResendEmailVerificationToken: {Message}", ex.Message);
            return new BaseResponse<int>(0, false, string.Empty, ex.Message);
        }
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
