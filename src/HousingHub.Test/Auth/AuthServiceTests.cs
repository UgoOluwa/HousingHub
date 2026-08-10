using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.AuthService;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.Dtos.Auth;
using HousingHub.Service.Dtos.Customer;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Auth;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenProvider> _tokenProviderMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly IMapper _mapper;
    private readonly AuthService _sut;

    private const string TestPasswordHash = "hashed_password";
    private const string TestToken = "jwt_token_123";

    public AuthServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenProviderMock = new Mock<ITokenProvider>();
        _emailServiceMock = new Mock<IEmailService>();
        var logger = NullLogger<AuthService>.Instance;

        var config = new TypeAdapterConfig();
        new CustomerMapper().Register(config);
        _mapper = new ObjectMapper(config);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Google:ClientId", "test-client-id" }
            })
            .Build();

        _unitOfWorkMock.Setup(u => u.CustomerCommands.InsertAsync(It.IsAny<Customer>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.CustomerCommands.UpdateAsync(It.IsAny<Customer>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RefreshTokenCommands.InsertAsync(It.IsAny<RefreshToken>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.RefreshTokenCommands.UpdateAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

        _passwordHasherMock.Setup(p => p.Hash(It.IsAny<string>())).Returns(TestPasswordHash);
        _passwordHasherMock.Setup(p => p.Verify(It.IsAny<string>(), TestPasswordHash)).Returns(true);
        _tokenProviderMock.Setup(t => t.Create(It.IsAny<Customer>())).Returns(TestToken);
        _emailServiceMock.Setup(e => e.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        _emailServiceMock.Setup(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        _sut = new AuthService(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _tokenProviderMock.Object,
            _mapper,
            configuration,
            logger,
            _emailServiceMock.Object);
    }

    private static Customer CreateCustomer(Guid? id = null, string email = "test@test.com",
        AuthProvider authProvider = AuthProvider.Local, bool emailVerified = true) =>
        new("John", "Doe", email, "08012345678", CustomerType.Customer, TestPasswordHash)
        {
            Id = id ?? Guid.NewGuid(),
            AuthProvider = authProvider,
            EmailVerified = emailVerified,
            EmailVerificationToken = "verify_token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
        };

    /// <summary>
    /// Points every customer lookup at the same result. AuthService reads through
    /// indexed lookups (GetByEmailAsync / GetByIdAsync / ...) rather than table
    /// scans, so tests shouldn't depend on which one a given method happens to use.
    /// </summary>
    private void SetupCustomerLookup(Customer? customer)
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(customer);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(customer);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByPhoneNumberAsync(It.IsAny<string>())).ReturnsAsync(customer);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByEmailOrPhoneAsync(It.IsAny<string>())).ReturnsAsync(customer);
    }

    // ── Register ─────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_ReturnsSuccess()
    {
        SetupCustomerLookup(null);

        var dto = new RegisterCustomerDto("John", "Doe", "john@test.com", "08012345678", "Password123!", CustomerType.Customer);
        var result = await _sut.Register(dto);

        Assert.True(result.IsSuccessful);

        // No customer data is returned on purpose — echoing the created record here
        // while the duplicate path returns null would make the two distinguishable.
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Register_WithExistingEmail_IsIndistinguishableFromSuccess()
    {
        SetupCustomerLookup(null);
        var freshResult = await _sut.Register(
            new RegisterCustomerDto("John", "Doe", "new@test.com", "08012345678", "Password123!", CustomerType.Customer));

        SetupCustomerLookup(CreateCustomer());
        var duplicateResult = await _sut.Register(
            new RegisterCustomerDto("John", "Doe", "existing@test.com", "08012345678", "Password123!", CustomerType.Customer));

        // The whole point: an attacker must not be able to tell these apart.
        Assert.Equal(freshResult.IsSuccessful, duplicateResult.IsSuccessful);
        Assert.Equal(freshResult.Message, duplicateResult.Message);
        Assert.Equal(freshResult.Data, duplicateResult.Data);
    }

    [Fact]
    public async Task Register_WithExistingEmail_DoesNotCreateASecondAccount()
    {
        SetupCustomerLookup(CreateCustomer());

        var dto = new RegisterCustomerDto("John", "Doe", "existing@test.com", "08012345678", "Password123!", CustomerType.Customer);
        await _sut.Register(dto);

        _unitOfWorkMock.Verify(u => u.CustomerCommands.InsertAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Fact]
    public async Task Register_WithExistingEmail_NotifiesTheRealAccountHolder()
    {
        var existing = CreateCustomer();
        SetupCustomerLookup(existing);

        var dto = new RegisterCustomerDto("Someone", "Else", "existing@test.com", "08012345678", "Password123!", CustomerType.Customer);
        await _sut.Register(dto);

        _emailServiceMock.Verify(
            e => e.SendRegistrationAttemptOnExistingAccountAsync(existing.Email, existing.FirstName),
            Times.Once);

        // And no verification email to the person who attempted it.
        _emailServiceMock.Verify(
            e => e.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_WhenInsertFails_ReturnsFailure()
    {
        SetupCustomerLookup(null);
        _unitOfWorkMock.Setup(u => u.CustomerCommands.InsertAsync(It.IsAny<Customer>())).ReturnsAsync(false);

        var dto = new RegisterCustomerDto("John", "Doe", "john@test.com", "08012345678", "Password123!", CustomerType.Customer);
        var result = await _sut.Register(dto);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task Register_SendsVerificationEmail()
    {
        SetupCustomerLookup(null);

        var dto = new RegisterCustomerDto("John", "Doe", "john@test.com", "08012345678", "Password123!", CustomerType.Customer);
        await _sut.Register(dto);

        _emailServiceMock.Verify(e => e.SendEmailVerificationAsync("john@test.com", "John", It.IsAny<string>()), Times.Once);
    }

    // ── Login ────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccess()
    {
        var customer = CreateCustomer();
        SetupCustomerLookup(customer);

        var dto = new LoginCustomerDto("test@test.com", "Password123!");
        var result = await _sut.Login(dto);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(TestToken, result.Data.token);
        Assert.False(string.IsNullOrEmpty(result.Data.refreshToken));
        Assert.Equal(ResponseMessages.LoginSuccess, result.Message);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsFailure()
    {
        var customer = CreateCustomer();
        SetupCustomerLookup(customer);
        _passwordHasherMock.Setup(p => p.Verify("wrong_password", TestPasswordHash)).Returns(false);

        var dto = new LoginCustomerDto("test@test.com", "wrong_password");
        var result = await _sut.Login(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InvalidCredentials, result.Message);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsFailure()
    {
        SetupCustomerLookup(null);

        var dto = new LoginCustomerDto("nonexistent@test.com", "Password123!");
        var result = await _sut.Login(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InvalidCredentials, result.Message);
    }

    [Fact]
    public async Task Login_WithLinkedGoogleAccountThatHasAPassword_Succeeds()
    {
        var customer = CreateCustomer(authProvider: AuthProvider.Google);
        customer.GoogleId = "google123";
        SetupCustomerLookup(customer);

        var dto = new LoginCustomerDto("test@test.com", "Password123!");
        var result = await _sut.Login(dto);

        // A linked account reconciles: either sign-in method reaches the same record.
        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Login_WithUnverifiedEmail_ReturnsFailure()
    {
        var customer = CreateCustomer(emailVerified: false);
        SetupCustomerLookup(customer);

        var dto = new LoginCustomerDto("test@test.com", "Password123!");
        var result = await _sut.Login(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.EmailNotVerified, result.Message);
    }

    // ── RefreshToken ─────────────────────────────────────────────

    [Fact]
    public async Task RefreshToken_ValidToken_RotatesAndReturnsNewTokens()
    {
        var customer = CreateCustomer();
        SetupCustomerLookup(customer);

        var existing = new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            TokenHash = "irrelevant-in-this-test",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            IsRevoked = false
        };
        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(existing);

        var result = await _sut.RefreshToken("some-raw-refresh-token");

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(TestToken, result.Data.token);
        Assert.False(string.IsNullOrEmpty(result.Data.refreshToken));
        Assert.True(existing.IsRevoked);
        _unitOfWorkMock.Verify(u => u.RefreshTokenCommands.UpdateAsync(existing), Times.Once);
        _unitOfWorkMock.Verify(u => u.RefreshTokenCommands.InsertAsync(It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshToken_UnknownToken_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        var result = await _sut.RefreshToken("unknown-token");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InvalidRefreshToken, result.Message);
    }

    [Fact]
    public async Task RefreshToken_ExpiredToken_ReturnsFailure()
    {
        var customer = CreateCustomer();
        var existing = new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            IsRevoked = false
        };
        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(existing);

        var result = await _sut.RefreshToken("expired-token");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InvalidRefreshToken, result.Message);
    }

    [Fact]
    public async Task RefreshToken_AlreadyRevokedToken_RevokesAllSessionsAndReturnsFailure()
    {
        var customer = CreateCustomer();
        var reusedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            IsRevoked = true // already used once — this presentation is a replay
        };
        var otherActiveToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            TokenHash = "other-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            IsRevoked = false
        };
        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(reusedToken);
        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetActiveByCustomerIdAsync(customer.Id))
            .ReturnsAsync(new List<RefreshToken> { otherActiveToken });

        var result = await _sut.RefreshToken("stolen-token");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InvalidRefreshToken, result.Message);
        Assert.True(otherActiveToken.IsRevoked);
        _unitOfWorkMock.Verify(u => u.RefreshTokenCommands.UpdateAsync(otherActiveToken), Times.Once);
    }

    // ── Logout ───────────────────────────────────────────────────

    [Fact]
    public async Task Logout_WithValidToken_RevokesIt()
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            IsRevoked = false
        };
        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(token);

        var result = await _sut.Logout("some-token");

        Assert.True(result.IsSuccessful);
        Assert.True(token.IsRevoked);
        _unitOfWorkMock.Verify(u => u.RefreshTokenCommands.UpdateAsync(token), Times.Once);
    }

    [Fact]
    public async Task Logout_WithUnknownToken_StillReportsSuccess()
    {
        // Reporting failure would confirm whether a token value is real, and the
        // caller's session is over either way.
        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync((RefreshToken?)null);

        var result = await _sut.Logout("never-issued");

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Logout_WithAllSessions_RevokesEveryActiveToken()
    {
        var customerId = Guid.NewGuid();
        var presented = new RefreshToken { Id = Guid.NewGuid(), CustomerId = customerId, TokenHash = "a", ExpiresAt = DateTime.UtcNow.AddDays(10) };
        var other = new RefreshToken { Id = Guid.NewGuid(), CustomerId = customerId, TokenHash = "b", ExpiresAt = DateTime.UtcNow.AddDays(10) };

        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(presented);
        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetActiveByCustomerIdAsync(customerId))
            .ReturnsAsync(new List<RefreshToken> { presented, other });

        var result = await _sut.Logout("some-token", allSessions: true);

        Assert.True(result.IsSuccessful);
        Assert.True(presented.IsRevoked);
        Assert.True(other.IsRevoked);
    }

    [Fact]
    public async Task RefreshToken_CustomerNotFound_ReturnsFailure()
    {
        var existing = new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            IsRevoked = false
        };
        _unitOfWorkMock.Setup(u => u.RefreshTokenQueries.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(existing);
        SetupCustomerLookup(null);

        var result = await _sut.RefreshToken("orphaned-token");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InvalidRefreshToken, result.Message);
    }

    // ── VerifyEmail ──────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_WithValidToken_ReturnsSuccess()
    {
        var customer = CreateCustomer(emailVerified: false);
        SetupCustomerLookup(customer);

        var dto = new VerifyEmailRequestDto("test@test.com", "verify_token");
        var result = await _sut.VerifyEmail(dto);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.EmailVerificationSuccess, result.Message);
    }

    // ── ResendEmailVerificationToken ─────────────────────────────

    [Fact]
    public async Task ResendVerification_UnknownAddressAndAlreadyVerified_AreIndistinguishable()
    {
        SetupCustomerLookup(null);
        var unknown = await _sut.ResendEmailVerificationToken("nobody@test.com");

        SetupCustomerLookup(CreateCustomer(emailVerified: true));
        var verified = await _sut.ResendEmailVerificationToken("verified@test.com");

        // Reporting "already verified" confirmed both that the address is registered
        // and that it is usable — exactly what an enumeration attack is looking for.
        Assert.Equal(unknown.IsSuccessful, verified.IsSuccessful);
        Assert.Equal(unknown.Message, verified.Message);
        Assert.Equal(unknown.Data, verified.Data);
    }

    [Fact]
    public async Task ResendVerification_WhenAlreadyVerified_TellsTheAccountHolderInstead()
    {
        var customer = CreateCustomer(emailVerified: true);
        SetupCustomerLookup(customer);

        await _sut.ResendEmailVerificationToken(customer.Email);

        // The real user is not left waiting for an email that will never arrive.
        _emailServiceMock.Verify(
            e => e.SendRegistrationAttemptOnExistingAccountAsync(customer.Email, customer.FirstName),
            Times.Once);
    }

    [Fact]
    public async Task ResendVerification_UnknownAddress_SendsNothing()
    {
        SetupCustomerLookup(null);

        await _sut.ResendEmailVerificationToken("nobody@test.com");

        _emailServiceMock.Verify(
            e => e.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyEmail_WhenAlreadyVerified_ReturnsSuccess()
    {
        var customer = CreateCustomer(emailVerified: true);
        SetupCustomerLookup(customer);

        var dto = new VerifyEmailRequestDto("test@test.com", "verify_token");
        var result = await _sut.VerifyEmail(dto);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.EmailAlreadyVerified, result.Message);
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ReturnsFailure()
    {
        var customer = CreateCustomer(emailVerified: false);
        SetupCustomerLookup(customer);

        var dto = new VerifyEmailRequestDto("test@test.com", "wrong_token");
        var result = await _sut.VerifyEmail(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.EmailVerificationFailed, result.Message);
    }

    [Fact]
    public async Task VerifyEmail_WithNonExistentUser_ReturnsFailure()
    {
        SetupCustomerLookup(null);

        var dto = new VerifyEmailRequestDto("none@test.com", "token");
        var result = await _sut.VerifyEmail(dto);

        Assert.False(result.IsSuccessful);
    }

    // ── ForgotPassword ───────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_WithExistingUser_SendsResetEmail()
    {
        var customer = CreateCustomer();
        SetupCustomerLookup(customer);

        var dto = new ForgotPasswordRequestDto("test@test.com");
        var result = await _sut.ForgotPassword(dto);

        Assert.True(result.IsSuccessful);
        _emailServiceMock.Verify(e => e.SendPasswordResetAsync("test@test.com", "John", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentUser_StillReturnsSuccess()
    {
        SetupCustomerLookup(null);

        var dto = new ForgotPasswordRequestDto("none@test.com");
        var result = await _sut.ForgotPassword(dto);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PasswordResetTokenSent, result.Message);
    }

    [Fact]
    public async Task ForgotPassword_WithGoogleAccount_SendsResetSoTheyCanAddAPassword()
    {
        var customer = CreateCustomer(authProvider: AuthProvider.Google);
        customer.PasswordHash = string.Empty;
        SetupCustomerLookup(customer);

        var dto = new ForgotPasswordRequestDto("test@test.com");
        var result = await _sut.ForgotPassword(dto);

        // This is how a Google customer adds a password so either method works.
        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PasswordResetTokenSent, result.Message);
        Assert.NotNull(customer.PasswordResetToken);
    }

    [Fact]
    public async Task ForgotPassword_WithinCooldown_DoesNotResendOrRegenerateToken()
    {
        var customer = CreateCustomer();
        customer.PasswordResetToken = "existing-token";
        customer.LastPasswordResetRequestedAt = DateTime.UtcNow.AddMinutes(-1); // well inside the 5-minute cooldown
        SetupCustomerLookup(customer);

        var dto = new ForgotPasswordRequestDto("test@test.com");
        var result = await _sut.ForgotPassword(dto);

        // Identical outward response to every other branch — no account-existence
        // or throttle-state signal leaks through.
        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PasswordResetTokenSent, result.Message);
        Assert.Equal("existing-token", customer.PasswordResetToken);
        _emailServiceMock.Verify(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_AfterCooldownExpires_SendsNewResetEmail()
    {
        var customer = CreateCustomer();
        customer.LastPasswordResetRequestedAt = DateTime.UtcNow.AddMinutes(-6); // just past the 5-minute cooldown
        SetupCustomerLookup(customer);

        var dto = new ForgotPasswordRequestDto("test@test.com");
        var result = await _sut.ForgotPassword(dto);

        Assert.True(result.IsSuccessful);
        _emailServiceMock.Verify(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ── ResetPassword ────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_WithValidToken_ReturnsSuccess()
    {
        var customer = CreateCustomer();
        customer.PasswordResetToken = "reset_token";
        customer.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        SetupCustomerLookup(customer);

        var dto = new ResetPasswordRequestDto("test@test.com", "reset_token", "NewPassword123!");
        var result = await _sut.ResetPassword(dto);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PasswordResetSuccess, result.Message);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsFailure()
    {
        var customer = CreateCustomer();
        customer.PasswordResetToken = "reset_token";
        customer.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        SetupCustomerLookup(customer);

        var dto = new ResetPasswordRequestDto("test@test.com", "wrong_token", "NewPassword123!");
        var result = await _sut.ResetPassword(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PasswordResetFailed, result.Message);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_ReturnsFailure()
    {
        var customer = CreateCustomer();
        customer.PasswordResetToken = "reset_token";
        customer.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1);
        SetupCustomerLookup(customer);

        var dto = new ResetPasswordRequestDto("test@test.com", "reset_token", "NewPassword123!");
        var result = await _sut.ResetPassword(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PasswordResetFailed, result.Message);
    }

    [Fact]
    public async Task ResetPassword_WithNonExistentUser_ReturnsFailure()
    {
        SetupCustomerLookup(null);

        var dto = new ResetPasswordRequestDto("none@test.com", "token", "NewPassword!");
        var result = await _sut.ResetPassword(dto);

        Assert.False(result.IsSuccessful);
    }

    // ── ChangePassword ───────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_ReturnsSuccess()
    {
        var customerId = Guid.NewGuid();
        var customer = CreateCustomer(id: customerId);
        SetupCustomerLookup(customer);

        var dto = new ChangePasswordRequestDto(customerId, "Password123!", "NewPassword123!");
        var result = await _sut.ChangePassword(dto);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PasswordChangeSuccess, result.Message);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsFailure()
    {
        var customerId = Guid.NewGuid();
        var customer = CreateCustomer(id: customerId);
        SetupCustomerLookup(customer);
        _passwordHasherMock.Setup(p => p.Verify("wrong_password", TestPasswordHash)).Returns(false);

        var dto = new ChangePasswordRequestDto(customerId, "wrong_password", "NewPassword123!");
        var result = await _sut.ChangePassword(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.CurrentPasswordIncorrect, result.Message);
    }

    [Fact]
    public async Task ChangePassword_WhenAccountHasNoPassword_DirectsUserToResetFlow()
    {
        var customerId = Guid.NewGuid();
        var customer = CreateCustomer(id: customerId, authProvider: AuthProvider.Google);
        customer.PasswordHash = string.Empty;
        SetupCustomerLookup(customer);

        var dto = new ChangePasswordRequestDto(customerId, "Password123!", "NewPassword123!");
        var result = await _sut.ChangePassword(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.AccountHasNoPassword, result.Message);
    }

    [Fact]
    public async Task ChangePassword_WithLinkedGoogleAccountThatHasAPassword_Succeeds()
    {
        var customerId = Guid.NewGuid();
        var customer = CreateCustomer(id: customerId, authProvider: AuthProvider.Google);
        customer.GoogleId = "google123";
        SetupCustomerLookup(customer);

        var dto = new ChangePasswordRequestDto(customerId, "Password123!", "NewPassword123!");
        var result = await _sut.ChangePassword(dto);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task ChangePassword_WithNonExistentUser_ReturnsFailure()
    {
        SetupCustomerLookup(null);

        var dto = new ChangePasswordRequestDto(Guid.NewGuid(), "Password123!", "NewPassword123!");
        var result = await _sut.ChangePassword(dto);

        Assert.False(result.IsSuccessful);
    }

    // ── GoogleSignInFromClaims ───────────────────────────────────

    [Fact]
    public async Task GoogleSignInFromClaims_WithNewUser_CreatesAndReturnsSuccess()
    {
        SetupCustomerLookup(null);

        var claims = new GoogleClaimsDto("new@google.com", "google123", "Jane", "Doe", EmailVerified: true);
        var result = await _sut.GoogleSignInFromClaims(claims);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(TestToken, result.Data.token);
    }

    [Fact]
    public async Task GoogleSignInFromClaims_WithExistingGoogleUser_ReturnsSuccess()
    {
        var customer = CreateCustomer(authProvider: AuthProvider.Google);
        customer.GoogleId = "google123";
        SetupCustomerLookup(customer);

        var claims = new GoogleClaimsDto("test@test.com", "google123", "John", "Doe", EmailVerified: true);
        var result = await _sut.GoogleSignInFromClaims(claims);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
    }

    // ── Account linking ──────────────────────────────────────────

    [Fact]
    public async Task GoogleSignInFromClaims_WithExistingPasswordAccount_LinksAndSignsIn()
    {
        var customer = CreateCustomer(authProvider: AuthProvider.Local, emailVerified: false);
        SetupCustomerLookup(customer);

        var claims = new GoogleClaimsDto("test@test.com", "google123", "John", "Doe", EmailVerified: true);
        var result = await _sut.GoogleSignInFromClaims(claims);

        Assert.True(result.IsSuccessful);
        Assert.Equal("google123", customer.GoogleId);
        // Google proved ownership of the mailbox.
        Assert.True(customer.EmailVerified);
        // The password is preserved so either method keeps working.
        Assert.Equal(TestPasswordHash, customer.PasswordHash);
    }

    [Fact]
    public async Task GoogleSignInFromClaims_WhenGoogleHasNotVerifiedEmail_RefusesToLink()
    {
        var customer = CreateCustomer(authProvider: AuthProvider.Local);
        SetupCustomerLookup(customer);

        var claims = new GoogleClaimsDto("test@test.com", "google123", "John", "Doe", EmailVerified: false);
        var result = await _sut.GoogleSignInFromClaims(claims);

        // Guards against account pre-hijacking via an unverified provider address.
        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.GoogleEmailNotVerified, result.Message);
        Assert.Null(customer.GoogleId);
    }

    [Fact]
    public async Task GoogleSignInFromClaims_WhenLinkedToDifferentGoogleAccount_ReturnsFailure()
    {
        var customer = CreateCustomer(authProvider: AuthProvider.Google);
        customer.GoogleId = "google-original";
        SetupCustomerLookup(customer);

        var claims = new GoogleClaimsDto("test@test.com", "google-other", "John", "Doe", EmailVerified: true);
        var result = await _sut.GoogleSignInFromClaims(claims);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.GoogleAccountMismatch, result.Message);
        Assert.Equal("google-original", customer.GoogleId);
    }

    [Fact]
    public async Task Login_WithGoogleAccountThatHasNoPassword_ReturnsGenericInvalidCredentials()
    {
        // Deliberately indistinguishable from a wrong-password failure — telling an
        // unauthenticated caller "this account has no password, it's Google-only"
        // would confirm the address is registered and leak its auth method.
        var customer = CreateCustomer(authProvider: AuthProvider.Google);
        customer.PasswordHash = string.Empty;
        customer.GoogleId = "google123";
        SetupCustomerLookup(customer);

        var result = await _sut.Login(new LoginCustomerDto("test@test.com", "Password123!"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InvalidCredentials, result.Message);
    }
}
