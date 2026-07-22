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

    // ── Register ─────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_ReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(false);

        var dto = new RegisterCustomerDto("John", "Doe", "john@test.com", "08012345678", "Password123!", CustomerType.Customer);
        var result = await _sut.Register(dto);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("John", result.Data.FirstName);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(true);

        var dto = new RegisterCustomerDto("John", "Doe", "existing@test.com", "08012345678", "Password123!", CustomerType.Customer);
        var result = await _sut.Register(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.CustomerAlreadyExists, result.Message);
    }

    [Fact]
    public async Task Register_WhenInsertFails_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.CustomerCommands.InsertAsync(It.IsAny<Customer>())).ReturnsAsync(false);

        var dto = new RegisterCustomerDto("John", "Doe", "john@test.com", "08012345678", "Password123!", CustomerType.Customer);
        var result = await _sut.Register(dto);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task Register_SendsVerificationEmail()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(false);

        var dto = new RegisterCustomerDto("John", "Doe", "john@test.com", "08012345678", "Password123!", CustomerType.Customer);
        await _sut.Register(dto);

        _emailServiceMock.Verify(e => e.SendEmailVerificationAsync("john@test.com", "John", It.IsAny<string>()), Times.Once);
    }

    // ── Login ────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccess()
    {
        var customer = CreateCustomer();
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var dto = new LoginCustomerDto("test@test.com", "Password123!");
        var result = await _sut.Login(dto);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(TestToken, result.Data.token);
        Assert.Equal(ResponseMessages.LoginSuccess, result.Message);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsFailure()
    {
        var customer = CreateCustomer();
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);
        _passwordHasherMock.Setup(p => p.Verify("wrong_password", TestPasswordHash)).Returns(false);

        var dto = new LoginCustomerDto("test@test.com", "wrong_password");
        var result = await _sut.Login(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.InvalidCredentials, result.Message);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync((Customer?)null);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var dto = new LoginCustomerDto("test@test.com", "Password123!");
        var result = await _sut.Login(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.EmailNotVerified, result.Message);
    }

    // ── VerifyEmail ──────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_WithValidToken_ReturnsSuccess()
    {
        var customer = CreateCustomer(emailVerified: false);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var dto = new VerifyEmailRequestDto("test@test.com", "verify_token");
        var result = await _sut.VerifyEmail(dto);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.EmailVerificationSuccess, result.Message);
    }

    [Fact]
    public async Task VerifyEmail_WhenAlreadyVerified_ReturnsSuccess()
    {
        var customer = CreateCustomer(emailVerified: true);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var dto = new VerifyEmailRequestDto("test@test.com", "verify_token");
        var result = await _sut.VerifyEmail(dto);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.EmailAlreadyVerified, result.Message);
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ReturnsFailure()
    {
        var customer = CreateCustomer(emailVerified: false);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var dto = new VerifyEmailRequestDto("test@test.com", "wrong_token");
        var result = await _sut.VerifyEmail(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.EmailVerificationFailed, result.Message);
    }

    [Fact]
    public async Task VerifyEmail_WithNonExistentUser_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync((Customer?)null);

        var dto = new VerifyEmailRequestDto("none@test.com", "token");
        var result = await _sut.VerifyEmail(dto);

        Assert.False(result.IsSuccessful);
    }

    // ── ForgotPassword ───────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_WithExistingUser_SendsResetEmail()
    {
        var customer = CreateCustomer();
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var dto = new ForgotPasswordRequestDto("test@test.com");
        var result = await _sut.ForgotPassword(dto);

        Assert.True(result.IsSuccessful);
        _emailServiceMock.Verify(e => e.SendPasswordResetAsync("test@test.com", "John", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentUser_StillReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync((Customer?)null);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var dto = new ForgotPasswordRequestDto("test@test.com");
        var result = await _sut.ForgotPassword(dto);

        // This is how a Google customer adds a password so either method works.
        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PasswordResetTokenSent, result.Message);
        Assert.NotNull(customer.PasswordResetToken);
    }

    // ── ResetPassword ────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_WithValidToken_ReturnsSuccess()
    {
        var customer = CreateCustomer();
        customer.PasswordResetToken = "reset_token";
        customer.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var dto = new ResetPasswordRequestDto("test@test.com", "reset_token", "NewPassword123!");
        var result = await _sut.ResetPassword(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PasswordResetFailed, result.Message);
    }

    [Fact]
    public async Task ResetPassword_WithNonExistentUser_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync((Customer?)null);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);
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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var dto = new ChangePasswordRequestDto(customerId, "Password123!", "NewPassword123!");
        var result = await _sut.ChangePassword(dto);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task ChangePassword_WithNonExistentUser_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync((Customer?)null);

        var dto = new ChangePasswordRequestDto(Guid.NewGuid(), "Password123!", "NewPassword123!");
        var result = await _sut.ChangePassword(dto);

        Assert.False(result.IsSuccessful);
    }

    // ── GoogleSignInFromClaims ───────────────────────────────────

    [Fact]
    public async Task GoogleSignInFromClaims_WithNewUser_CreatesAndReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync((Customer?)null);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

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
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var claims = new GoogleClaimsDto("test@test.com", "google-other", "John", "Doe", EmailVerified: true);
        var result = await _sut.GoogleSignInFromClaims(claims);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.GoogleAccountMismatch, result.Message);
        Assert.Equal("google-original", customer.GoogleId);
    }

    [Fact]
    public async Task Login_WithGoogleAccountThatHasNoPassword_TellsUserHowToSignIn()
    {
        var customer = CreateCustomer(authProvider: AuthProvider.Google);
        customer.PasswordHash = string.Empty;
        customer.GoogleId = "google123";
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(customer);

        var result = await _sut.Login(new LoginCustomerDto("test@test.com", "Password123!"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.AccountHasNoPassword, result.Message);
    }
}
