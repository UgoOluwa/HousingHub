using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.AdminService;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Admin;
using Microsoft.Extensions.Configuration;
using Moq;
using AdminEntity = HousingHub.Model.Entities.Admin;
using AdminRefreshTokenEntity = HousingHub.Model.Entities.AdminRefreshToken;

namespace HousingHub.Test.Admin;

public class AdminAuthServiceTests
{
    private readonly Mock<IDynamoDBContext> _dynamoDbMock;
    private readonly Mock<IPasswordHasher> _hasherMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly AdminAuthService _sut;

    public AdminAuthServiceTests()
    {
        _dynamoDbMock = new Mock<IDynamoDBContext>();
        _hasherMock = new Mock<IPasswordHasher>();
        _emailServiceMock = new Mock<IEmailService>();
        _configMock = new Mock<IConfiguration>();

        _configMock.Setup(c => c["AdminJwt:Secret"]).Returns("super-secret-key-for-tests-minimum-length-256");
        _configMock.Setup(c => c["AdminJwt:Issuer"]).Returns("TestIssuer");
        _configMock.Setup(c => c["AdminJwt:Audience"]).Returns("TestAudience");
        _configMock.Setup(c => c["AdminJwt:ExpirationInMinutes"]).Returns("60");

        _emailServiceMock.Setup(e => e.SendAdminOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        _dynamoDbMock.Setup(d => d.SaveAsync(It.IsAny<AdminEntity>(), It.IsAny<System.Threading.CancellationToken>())).Returns(Task.CompletedTask);
        _dynamoDbMock.Setup(d => d.SaveAsync(It.IsAny<AdminRefreshTokenEntity>(), It.IsAny<System.Threading.CancellationToken>())).Returns(Task.CompletedTask);
        SetupQueryRefreshToken("TokenHash-index", Array.Empty<AdminRefreshTokenEntity>());
        SetupQueryRefreshToken("AdminId-index", Array.Empty<AdminRefreshTokenEntity>());

        _sut = new AdminAuthService(_dynamoDbMock.Object, _hasherMock.Object, _emailServiceMock.Object, _configMock.Object);
    }

    private static AdminEntity MakeAdmin(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = "admin@test.com",
        PasswordHash = "hashed",
        FirstName = "Super",
        LastName = "Admin",
        IsActive = isActive,
        DateCreated = DateTime.UtcNow,
        DateModified = DateTime.UtcNow
    };

    private void SetupQuery(IEnumerable<AdminEntity> results)
    {
        var mockSearch = new Mock<AsyncSearch<AdminEntity>>();
        mockSearch
            .Setup(s => s.GetRemainingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(results.ToList());
        _dynamoDbMock
            .Setup(d => d.QueryAsync<AdminEntity>(It.IsAny<object>(), It.IsAny<DynamoDBOperationConfig>()))
            .Returns(mockSearch.Object);
    }

    private void SetupLoad(AdminEntity? result) =>
        _dynamoDbMock
            .Setup(d => d.LoadAsync<AdminEntity>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void SetupQueryRefreshToken(string indexName, IEnumerable<AdminRefreshTokenEntity> results)
    {
        var mockSearch = new Mock<AsyncSearch<AdminRefreshTokenEntity>>();
        mockSearch
            .Setup(s => s.GetRemainingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(results.ToList());
        _dynamoDbMock
            .Setup(d => d.QueryAsync<AdminRefreshTokenEntity>(It.IsAny<object>(), It.Is<DynamoDBOperationConfig>(c => c.IndexName == indexName)))
            .Returns(mockSearch.Object);
    }

    private void SetupScan(IEnumerable<AdminEntity> results)
    {
        var mockSearch = new Mock<AsyncSearch<AdminEntity>>();
        mockSearch
            .Setup(s => s.GetRemainingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(results.ToList());
        _dynamoDbMock
            .Setup(d => d.ScanAsync<AdminEntity>(It.IsAny<IEnumerable<ScanCondition>>()))
            .Returns(mockSearch.Object);
    }

    // ── RequestOtpAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task RequestOtp_ActiveAdmin_SavesCodeAndSendsEmail()
    {
        var admin = MakeAdmin();
        SetupQuery(new[] { admin });

        var result = await _sut.RequestOtpAsync(admin.Email);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OtpSent, result.Message);
        Assert.False(string.IsNullOrEmpty(admin.OtpCode));
        Assert.Equal(6, admin.OtpCode!.Length);
        Assert.NotNull(admin.OtpExpiresAt);
        Assert.NotNull(admin.OtpRequestedAt);
        Assert.Equal(0, admin.OtpAttempts);
        _dynamoDbMock.Verify(d => d.SaveAsync(admin, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _emailServiceMock.Verify(e => e.SendAdminOtpAsync(admin.Email, admin.FirstName, admin.OtpCode!), Times.Once);
    }

    [Fact]
    public async Task RequestOtp_InactiveAdmin_DoesNotSendEmail()
    {
        var inactiveAdmin = MakeAdmin(isActive: false);
        SetupQuery(new[] { inactiveAdmin });

        var result = await _sut.RequestOtpAsync(inactiveAdmin.Email);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OtpSent, result.Message);
        _emailServiceMock.Verify(e => e.SendAdminOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RequestOtp_EmailNotFound_DoesNotThrowOrSendEmail()
    {
        SetupQuery(Array.Empty<AdminEntity>());

        var result = await _sut.RequestOtpAsync("unknown@test.com");

        // Identical outward response to every other branch — no account-existence signal.
        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OtpSent, result.Message);
        _emailServiceMock.Verify(e => e.SendAdminOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RequestOtp_WithinCooldown_DoesNotResendOrRegenerateCode()
    {
        var admin = MakeAdmin();
        admin.OtpCode = "111111";
        admin.OtpRequestedAt = DateTime.UtcNow.AddSeconds(-10); // 10s ago — well inside the 60s cooldown
        SetupQuery(new[] { admin });

        var result = await _sut.RequestOtpAsync(admin.Email);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OtpSent, result.Message);
        Assert.Equal("111111", admin.OtpCode); // unchanged
        _emailServiceMock.Verify(e => e.SendAdminOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RequestOtp_AfterCooldownExpires_SendsNewCode()
    {
        var admin = MakeAdmin();
        admin.OtpCode = "111111";
        admin.OtpRequestedAt = DateTime.UtcNow.AddSeconds(-61); // just past the 60s cooldown
        SetupQuery(new[] { admin });

        var result = await _sut.RequestOtpAsync(admin.Email);

        Assert.True(result.IsSuccessful);
        _emailServiceMock.Verify(e => e.SendAdminOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ── VerifyOtpAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyOtp_ValidCode_ReturnsTokenAndClearsCode()
    {
        var admin = MakeAdmin();
        admin.OtpCode = "123456";
        admin.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
        admin.OtpRequestedAt = DateTime.UtcNow;
        SetupQuery(new[] { admin });

        var result = await _sut.VerifyOtpAsync(admin.Email, "123456");

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.LoginSuccess, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(admin.FirstName, result.Data!.FirstName);
        Assert.Equal(admin.Email, result.Data.Email);
        Assert.False(string.IsNullOrEmpty(result.Data.Token));
        Assert.False(string.IsNullOrEmpty(result.Data.RefreshToken));
        Assert.Null(admin.OtpCode);
        Assert.Null(admin.OtpExpiresAt);
        Assert.Null(admin.OtpRequestedAt);
        Assert.Equal(0, admin.OtpAttempts);
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_ReturnsFailureAndIncrementsAttempts()
    {
        var admin = MakeAdmin();
        admin.OtpCode = "123456";
        admin.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
        SetupQuery(new[] { admin });

        var result = await _sut.VerifyOtpAsync(admin.Email, "000000");

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        Assert.Equal(ResponseMessages.OtpInvalidOrExpired, result.Message);
        Assert.Equal("123456", admin.OtpCode);
        Assert.Equal(1, admin.OtpAttempts);
    }

    [Fact]
    public async Task VerifyOtp_TooManyWrongAttempts_LocksOutCodeWithDistinctMessage()
    {
        var admin = MakeAdmin();
        admin.OtpCode = "123456";
        admin.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
        admin.OtpAttempts = 4; // one more wrong guess hits the 5-attempt limit
        SetupQuery(new[] { admin });

        var result = await _sut.VerifyOtpAsync(admin.Email, "000000");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OtpTooManyAttempts, result.Message);
        Assert.Null(admin.OtpCode);
        Assert.Null(admin.OtpExpiresAt);
        Assert.Equal(0, admin.OtpAttempts);
    }

    [Fact]
    public async Task VerifyOtp_ExpiredCode_ReturnsFailure()
    {
        var admin = MakeAdmin();
        admin.OtpCode = "123456";
        admin.OtpExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        SetupQuery(new[] { admin });

        var result = await _sut.VerifyOtpAsync(admin.Email, "123456");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OtpInvalidOrExpired, result.Message);
    }

    [Fact]
    public async Task VerifyOtp_NoCodeRequested_ReturnsFailure()
    {
        var admin = MakeAdmin();
        SetupQuery(new[] { admin });

        var result = await _sut.VerifyOtpAsync(admin.Email, "123456");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OtpInvalidOrExpired, result.Message);
    }

    [Fact]
    public async Task VerifyOtp_InactiveAdmin_ReturnsFailure()
    {
        var inactiveAdmin = MakeAdmin(isActive: false);
        inactiveAdmin.OtpCode = "123456";
        inactiveAdmin.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
        SetupQuery(new[] { inactiveAdmin });

        var result = await _sut.VerifyOtpAsync(inactiveAdmin.Email, "123456");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OtpInvalidOrExpired, result.Message);
    }

    [Fact]
    public async Task VerifyOtp_EmailNotFound_ReturnsFailure()
    {
        SetupQuery(Array.Empty<AdminEntity>());

        var result = await _sut.VerifyOtpAsync("unknown@test.com", "123456");

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OtpInvalidOrExpired, result.Message);
    }

    // ── RefreshTokenAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshToken_ValidToken_RotatesAndReturnsNewTokens()
    {
        var admin = MakeAdmin();
        SetupLoad(admin);
        var existing = new AdminRefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            AdminId = admin.Id,
            TokenHash = "irrelevant-in-this-test",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            IsRevoked = false
        };
        SetupQueryRefreshToken("TokenHash-index", new[] { existing });

        var result = await _sut.RefreshTokenAsync("some-raw-refresh-token");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.Token));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.True(existing.IsRevoked);
        _dynamoDbMock.Verify(d => d.SaveAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _dynamoDbMock.Verify(d => d.SaveAsync(It.IsAny<AdminRefreshTokenEntity>(), It.IsAny<CancellationToken>()), Times.Exactly(2)); // revoke existing + insert new
    }

    [Fact]
    public async Task RefreshToken_UnknownToken_ReturnsNull()
    {
        var result = await _sut.RefreshTokenAsync("unknown-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshToken_ExpiredToken_ReturnsNull()
    {
        var admin = MakeAdmin();
        var existing = new AdminRefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            AdminId = admin.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            IsRevoked = false
        };
        SetupQueryRefreshToken("TokenHash-index", new[] { existing });

        var result = await _sut.RefreshTokenAsync("expired-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshToken_AlreadyRevokedToken_RevokesAllSessionsAndReturnsNull()
    {
        var admin = MakeAdmin();
        var reusedToken = new AdminRefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            AdminId = admin.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            IsRevoked = true // already used once — this presentation is a replay
        };
        var otherActiveToken = new AdminRefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            AdminId = admin.Id,
            TokenHash = "other-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            IsRevoked = false
        };
        SetupQueryRefreshToken("TokenHash-index", new[] { reusedToken });
        SetupQueryRefreshToken("AdminId-index", new[] { otherActiveToken });

        var result = await _sut.RefreshTokenAsync("stolen-token");

        Assert.Null(result);
        Assert.True(otherActiveToken.IsRevoked);
        _dynamoDbMock.Verify(d => d.SaveAsync(otherActiveToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshToken_InactiveAdmin_ReturnsNull()
    {
        var inactiveAdmin = MakeAdmin(isActive: false);
        SetupLoad(inactiveAdmin);
        var existing = new AdminRefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            AdminId = inactiveAdmin.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            IsRevoked = false
        };
        SetupQueryRefreshToken("TokenHash-index", new[] { existing });

        var result = await _sut.RefreshTokenAsync("token-for-deactivated-admin");

        Assert.Null(result);
    }

    // ── CreateAdminAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAdmin_HashesPasswordAndSaves()
    {
        _hasherMock.Setup(h => h.Hash("pass")).Returns("hashed-pass");
        AdminEntity? saved = null;
        _dynamoDbMock
            .Setup(d => d.SaveAsync(It.IsAny<AdminEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEntity, CancellationToken>((a, _) => saved = a)
            .Returns(Task.CompletedTask);

        await _sut.CreateAdminAsync("new@test.com", "pass", "First", "Last");

        Assert.NotNull(saved);
        Assert.Equal("hashed-pass", saved!.PasswordHash);
        Assert.Equal("new@test.com", saved.Email);
        Assert.Equal("First", saved.FirstName);
        Assert.True(saved.IsActive);
    }

    // ── CreateStaffAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStaff_SavesActiveAdminWithoutRequiringACallerSuppliedPassword()
    {
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(p => $"hashed-{p}");
        AdminEntity? saved = null;
        _dynamoDbMock
            .Setup(d => d.SaveAsync(It.IsAny<AdminEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEntity, CancellationToken>((a, _) => saved = a)
            .Returns(Task.CompletedTask);

        await _sut.CreateStaffAsync("staff@test.com", "Staff", "Member");

        Assert.NotNull(saved);
        Assert.Equal("staff@test.com", saved!.Email);
        Assert.Equal("Staff", saved.FirstName);
        Assert.Equal("Member", saved.LastName);
        Assert.True(saved.IsActive);
        Assert.False(string.IsNullOrEmpty(saved.PasswordHash));
    }

    // ── GetAdminProfileAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminProfile_ExistingAdmin_ReturnsDto()
    {
        var admin = MakeAdmin();
        SetupLoad(admin);

        var result = await _sut.GetAdminProfileAsync(admin.Id);

        Assert.NotNull(result);
        Assert.Equal(admin.Id, result!.Id);
        Assert.Equal(admin.Email, result.Email);
    }

    [Fact]
    public async Task GetAdminProfile_NotFound_ReturnsNull()
    {
        SetupLoad(null);

        var result = await _sut.GetAdminProfileAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ── UpdateAdminProfileAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateAdminProfile_ProvidedFields_UpdatesAndReturnsTrue()
    {
        var admin = MakeAdmin();
        SetupLoad(admin);
        _dynamoDbMock.Setup(d => d.SaveAsync(It.IsAny<AdminEntity>(), default)).Returns(Task.CompletedTask);

        var result = await _sut.UpdateAdminProfileAsync(admin.Id, new UpdateAdminProfileDto("NewFirst", "NewLast"));

        Assert.True(result);
        Assert.Equal("NewFirst", admin.FirstName);
        Assert.Equal("NewLast", admin.LastName);
    }

    [Fact]
    public async Task UpdateAdminProfile_NullFields_PreservesExistingValues()
    {
        var admin = MakeAdmin();
        SetupLoad(admin);
        _dynamoDbMock.Setup(d => d.SaveAsync(It.IsAny<AdminEntity>(), default)).Returns(Task.CompletedTask);

        await _sut.UpdateAdminProfileAsync(admin.Id, new UpdateAdminProfileDto(null, null));

        Assert.Equal("Super", admin.FirstName);
        Assert.Equal("Admin", admin.LastName);
    }

    [Fact]
    public async Task UpdateAdminProfile_NotFound_ReturnsFalse()
    {
        SetupLoad(null);

        var result = await _sut.UpdateAdminProfileAsync(Guid.NewGuid(), new UpdateAdminProfileDto("X", null));

        Assert.False(result);
        _dynamoDbMock.Verify(d => d.SaveAsync(It.IsAny<AdminEntity>(), default), Times.Never);
    }

    // ── ChangeAdminPasswordAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_CorrectCurrentPassword_UpdatesHashAndReturnsTrue()
    {
        var admin = MakeAdmin();
        SetupLoad(admin);
        _hasherMock.Setup(h => h.Verify("current", admin.PasswordHash)).Returns(true);
        _hasherMock.Setup(h => h.Hash("new")).Returns("new-hash");
        _dynamoDbMock.Setup(d => d.SaveAsync(It.IsAny<AdminEntity>(), default)).Returns(Task.CompletedTask);

        var result = await _sut.ChangeAdminPasswordAsync(admin.Id, "current", "new");

        Assert.True(result);
        Assert.Equal("new-hash", admin.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsFalse()
    {
        var admin = MakeAdmin();
        SetupLoad(admin);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), admin.PasswordHash)).Returns(false);

        var result = await _sut.ChangeAdminPasswordAsync(admin.Id, "wrong", "new");

        Assert.False(result);
        _hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_NotFound_ReturnsFalse()
    {
        SetupLoad(null);

        var result = await _sut.ChangeAdminPasswordAsync(Guid.NewGuid(), "current", "new");

        Assert.False(result);
    }

    // ── GetAllStaffAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllStaff_ReturnsSortedByDateCreatedDescending()
    {
        var older = MakeAdmin(); older.DateCreated = DateTime.UtcNow.AddDays(-5);
        var newer = MakeAdmin(); newer.DateCreated = DateTime.UtcNow.AddDays(-1);
        SetupScan(new[] { older, newer });

        var result = await _sut.GetAllStaffAsync();

        Assert.Equal(2, result.Count);
        Assert.True(result[0].DateJoined >= result[1].DateJoined);
    }

    [Fact]
    public async Task GetAllStaff_EmptyTable_ReturnsEmptyList()
    {
        SetupScan(Array.Empty<AdminEntity>());

        var result = await _sut.GetAllStaffAsync();

        Assert.Empty(result);
    }

    // ── DeactivateAdminAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateAdmin_ExistingAdmin_SetsIsActiveFalse()
    {
        var admin = MakeAdmin(isActive: true);
        SetupLoad(admin);
        _dynamoDbMock.Setup(d => d.SaveAsync(It.IsAny<AdminEntity>(), default)).Returns(Task.CompletedTask);

        var result = await _sut.DeactivateAdminAsync(admin.Id);

        Assert.True(result);
        Assert.False(admin.IsActive);
    }

    [Fact]
    public async Task DeactivateAdmin_NotFound_ReturnsFalse()
    {
        SetupLoad(null);

        var result = await _sut.DeactivateAdminAsync(Guid.NewGuid());

        Assert.False(result);
    }

    // ── ReactivateAdminAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ReactivateAdmin_ExistingAdmin_SetsIsActiveTrue()
    {
        var admin = MakeAdmin(isActive: false);
        SetupLoad(admin);
        _dynamoDbMock.Setup(d => d.SaveAsync(It.IsAny<AdminEntity>(), default)).Returns(Task.CompletedTask);

        var result = await _sut.ReactivateAdminAsync(admin.Id);

        Assert.True(result);
        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task ReactivateAdmin_NotFound_ReturnsFalse()
    {
        SetupLoad(null);

        var result = await _sut.ReactivateAdminAsync(Guid.NewGuid());

        Assert.False(result);
    }
}
