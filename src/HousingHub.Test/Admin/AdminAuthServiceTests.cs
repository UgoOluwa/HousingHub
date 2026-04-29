using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using HousingHub.Service.AdminService;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Dtos.Admin;
using Microsoft.Extensions.Configuration;
using Moq;
using AdminEntity = HousingHub.Model.Entities.Admin;

namespace HousingHub.Test.Admin;

public class AdminAuthServiceTests
{
    private readonly Mock<IDynamoDBContext> _dynamoDbMock;
    private readonly Mock<IPasswordHasher> _hasherMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly AdminAuthService _sut;

    public AdminAuthServiceTests()
    {
        _dynamoDbMock = new Mock<IDynamoDBContext>();
        _hasherMock = new Mock<IPasswordHasher>();
        _configMock = new Mock<IConfiguration>();

        _configMock.Setup(c => c["AdminJwt:Secret"]).Returns("super-secret-key-for-tests-minimum-length-256");
        _configMock.Setup(c => c["AdminJwt:Issuer"]).Returns("TestIssuer");
        _configMock.Setup(c => c["AdminJwt:Audience"]).Returns("TestAudience");
        _configMock.Setup(c => c["AdminJwt:ExpirationInMinutes"]).Returns("60");

        _sut = new AdminAuthService(_dynamoDbMock.Object, _hasherMock.Object, _configMock.Object);
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

    // ── LoginAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndName()
    {
        var admin = MakeAdmin();
        SetupQuery(new[] { admin });
        _hasherMock.Setup(h => h.Verify("pass", admin.PasswordHash)).Returns(true);

        var result = await _sut.LoginAsync(admin.Email, "pass");

        Assert.NotNull(result);
        Assert.Equal(admin.FirstName, result!.FirstName);
        Assert.Equal(admin.Email, result.Email);
        Assert.False(string.IsNullOrEmpty(result.Token));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        var admin = MakeAdmin();
        SetupQuery(new[] { admin });
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), admin.PasswordHash)).Returns(false);

        var result = await _sut.LoginAsync(admin.Email, "wrong");

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_InactiveAdmin_ReturnsNull()
    {
        var inactiveAdmin = MakeAdmin(isActive: false);
        SetupQuery(new[] { inactiveAdmin });

        var result = await _sut.LoginAsync(inactiveAdmin.Email, "pass");

        Assert.Null(result);
        _hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_EmailNotFound_ReturnsNull()
    {
        SetupQuery(Array.Empty<AdminEntity>());

        var result = await _sut.LoginAsync("unknown@test.com", "pass");

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
