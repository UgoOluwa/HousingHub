using Mapster;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Commons.Geocoding;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyAddress;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.PropertyAlertService.Interfaces;
using HousingHub.Service.PropertyService;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Properties;

public class PropertyCommandServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<IGeocodingService> _geocodingServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IPropertyAlertPreferenceQueryService> _propertyAlertPreferenceQueryServiceMock;
    private readonly Mock<IRealtimeNotifier> _realtimeNotifierMock;
    private readonly IMapper _mapper;
    private readonly PropertyCommandService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();

    public PropertyCommandServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _geocodingServiceMock = new Mock<IGeocodingService>();
        _emailServiceMock = new Mock<IEmailService>();
        _propertyAlertPreferenceQueryServiceMock = new Mock<IPropertyAlertPreferenceQueryService>();
        _realtimeNotifierMock = new Mock<IRealtimeNotifier>();
        var config = new TypeAdapterConfig();
        new PropertyMapper().Register(config);
        _mapper = new ObjectMapper(config);
        var logger = NullLogger<PropertyCommandService>.Instance;

        // Set up default returns for command methods used in Update/Delete flows
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<HousingHub.Model.Entities.Property>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.DeleteAsync(It.IsAny<HousingHub.Model.Entities.Property>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.PropertyAddressCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.PropertyAddress>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        // UpdateProperty reloads PropertyFiles before mapping the response; default to none.
        _unitOfWorkMock
            .Setup(u => u.PropertyFileQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.PropertyFile>());
        _unitOfWorkMock
            .Setup(u => u.PropertyFileCommands.InsertRangeAsync(It.IsAny<IEnumerable<HousingHub.Model.Entities.PropertyFile>>()))
            .Returns(Task.CompletedTask);

        // The new duplicate-address check in CreateProperty scans PropertyQueries.GetAllAsync() —
        // default to "no existing properties" so tests that don't care about duplicates don't
        // accidentally trip the duplicate-detection path (DefaultValue.Mock would otherwise hand
        // back a bare mocked IEnumerable, which is unsafe to enumerate).
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync())
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property>());

        // Geocoding is best-effort and network-dependent — default to "couldn't resolve" in tests.
        _geocodingServiceMock
            .Setup(g => g.GeocodeAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(((double, double)?)null);

        // No active alert preferences by default — SetPropertyPublishedInternalAsync's
        // publish hook is a no-op unless a test explicitly sets up a matching preference.
        _propertyAlertPreferenceQueryServiceMock
            .Setup(p => p.GetAllActiveAsync())
            .ReturnsAsync(new List<PropertyAlertPreference>());

        _unitOfWorkMock
            .Setup(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>()))
            .ReturnsAsync(true);

        _sut = new PropertyCommandService(
            logger,
            _unitOfWorkMock.Object,
            _mapper,
            _fileStorageServiceMock.Object,
            _geocodingServiceMock.Object,
            _emailServiceMock.Object,
            _propertyAlertPreferenceQueryServiceMock.Object,
            _realtimeNotifierMock.Object);
    }

    private Customer CreateOwner(CustomerType type) => new("John", "Doe", "john@test.com", "08012345678", type, "hash")
    {
        Id = OwnerId
    };

    private CreatePropertyDto CreateValidDto() => new(
        Title: "Nice Apartment",
        Description: "A lovely 3-bed apartment",
        PropertyType: PropertyType.Apartment,
        Price: 500000m,
        Availability: PropertyAvailability.Available,
        PropertyLeaseType: PropertyLeaseType.Sale,
        Features: PropertyFeature.Parking | PropertyFeature.Security,
        ContactPersonName: "Agent Smith",
        ContactPersonEmail: "smith@agency.com",
        ContactPersonPhoneNumber: "08099887766",
        OwnerId: OwnerId,
        PropertyAddress: new UpdatePropertyAddressDto("10 Main St", "Lagos", "Lagos", "Nigeria", "100001"),
        Latitude: null,
        Longitude: null);

    // ??? Create ???????????????????????????????????????????????????????

    [Fact]
    public async Task CreateProperty_AsHouseOwner_Succeeds()
    {
        // Arrange
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        // Act
        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Property);
        Assert.Equal("Nice Apartment", result.Data.Property!.Title);
        Assert.StartsWith("PROP-", result.Data.Property.PropertyId);
    }

    [Fact]
    public async Task CreateProperty_AsAgent_Succeeds()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Agent));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CreateProperty_AsCustomer_Fails()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Customer));

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.UnauthorizedPropertyAction, result.Message);
    }

    [Fact]
    public async Task CreateProperty_WithUnknownUser_Fails()
    {
        SetupOwnerLookup(null);

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task CreateProperty_SetsFeatureFlags()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        Assert.True(result.Data.Property!.Features.HasFlag(PropertyFeature.Parking));
        Assert.True(result.Data.Property.Features.HasFlag(PropertyFeature.Security));
    }

    [Fact]
    public async Task CreateProperty_SetsContactPerson()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Agent));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        Assert.Equal("Agent Smith", result.Data.Property!.ContactPersonName);
        Assert.Equal("smith@agency.com", result.Data.Property.ContactPersonEmail);
    }

    /// <summary>
    /// A mock upload carrying real JPEG magic bytes. UploadedFileValidator reads the
    /// file signature, so a mock without a readable stream no longer passes validation.
    /// </summary>
    private static Mock<IFormFile> CreateFormFile(string fileName = "photo.jpg", long length = 1024)
    {
        // SOI + APP0, enough for the JPEG signature check.
        byte[] jpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00];

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(jpegHeader));
        return fileMock;
    }

    [Fact]
    public async Task CreateProperty_WithFiles_PersistsPropertyFileRecords()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();
        _fileStorageServiceMock
            .Setup(f => f.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://s3.example.com/properties/photo.jpg");

        var dto = CreateValidDto() with { Files = new List<IFormFile> { CreateFormFile().Object } };
        var result = await _sut.CreateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        Assert.Single(result.Data.Property!.Files!);
        _unitOfWorkMock.Verify(
            u => u.PropertyFileCommands.InsertRangeAsync(It.Is<IEnumerable<HousingHub.Model.Entities.PropertyFile>>(files => files.Count() == 1)),
            Times.Once);
    }

    [Fact]
    public async Task CreateProperty_WithoutFiles_DoesNotCallInsertRange()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        await _sut.CreateProperty(CreateValidDto(), OwnerId);

        _unitOfWorkMock.Verify(
            u => u.PropertyFileCommands.InsertRangeAsync(It.IsAny<IEnumerable<HousingHub.Model.Entities.PropertyFile>>()),
            Times.Never);
    }

    // ??? Update ???????????????????????????????????????????????????????

    [Fact]
    public async Task UpdateProperty_ByOwner_Succeeds()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Old Title",
            Description = "Old Desc",
            OwnerId = OwnerId
        });

        var dto = new UpdatePropertyDto(PropertyId, "New Title", null, null, 600000m, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("New Title", result.Data!.Title);
        Assert.Equal(600000m, result.Data.Price);
    }

    [Fact]
    public async Task UpdateProperty_WhenAddressChanges_ReGeocodes()
    {
        var addressId = Guid.NewGuid();
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Title",
            Description = "Desc",
            OwnerId = OwnerId,
            AddressId = addressId
        });
        var existingAddress = new HousingHub.Model.Entities.PropertyAddress("Old St", "Ikeja", "Lagos", "Nigeria", "100001") { Id = addressId };
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.GetByIdAsync(addressId)).ReturnsAsync(existingAddress);
        _unitOfWorkMock.Setup(u => u.PropertyAddressCommands.UpdateAsync(It.IsAny<HousingHub.Model.Entities.PropertyAddress>())).Returns(Task.CompletedTask);
        _geocodingServiceMock
            .Setup(g => g.GeocodeAsync("New St", "Ikeja", "Lagos", "Nigeria"))
            .ReturnsAsync((6.6, 3.3));

        var dto = new UpdatePropertyDto(PropertyId, null, null, null, null, null, null, null, null, null, null,
            new UpdatePropertyAddressDto("New St", null, null, null, null), null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(6.6, result.Data!.Latitude);
        Assert.Equal(3.3, result.Data.Longitude);
    }

    [Fact]
    public async Task UpdateProperty_ByDifferentUser_Fails()
    {
        var differentUserId = Guid.NewGuid();
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Title",
            Description = "Desc",
            OwnerId = Guid.NewGuid() // different owner
        });

        var dto = new UpdatePropertyDto(PropertyId, "Hacked", null, null, null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PropertyNotOwnedByUser, result.Message);
    }

    [Fact]
    public async Task UpdateProperty_AsCustomer_Fails()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Customer));

        var dto = new UpdatePropertyDto(PropertyId, "Title", null, null, null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.UnauthorizedPropertyAction, result.Message);
    }

    [Fact]
    public async Task UpdateProperty_PropertyNotFound_Fails()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(null);

        var dto = new UpdatePropertyDto(PropertyId, "Title", null, null, null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task CreateProperty_WhenInsertFails_ReturnsFailure()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        _unitOfWorkMock
            .Setup(u => u.PropertyCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Property>()))
            .ReturnsAsync(false);

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Failed to create", result.Message);
    }

    [Fact]
    public async Task CreateProperty_WithoutAddress_Succeeds()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        var dto = new CreatePropertyDto(
            Title: "No Address Apt",
            Description: "Desc",
            PropertyType: PropertyType.Apartment,
            Price: 100000m,
            Availability: PropertyAvailability.Available,
            PropertyLeaseType: PropertyLeaseType.Sale,
            Features: PropertyFeature.None,
            ContactPersonName: null,
            ContactPersonEmail: null,
            ContactPersonPhoneNumber: null,
            OwnerId: OwnerId,
            PropertyAddress: null,
            Latitude: null,
            Longitude: null);

        var result = await _sut.CreateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CreateProperty_MapsAllDtoFields()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        var data = result.Data.Property!;
        Assert.Equal("Nice Apartment", data.Title);
        Assert.Equal("A lovely 3-bed apartment", data.Description);
        Assert.Equal(PropertyType.Apartment, data.PropertyType);
        Assert.Equal(500000m, data.Price);
        Assert.Equal(PropertyAvailability.Available, data.Availability);
        Assert.Equal(PropertyLeaseType.Sale, data.PropertyLeaseType);
        Assert.Equal(OwnerId, data.OwnerId);
    }

    // ??? Update ?? partial field updates ?????????????????????????????

    [Fact]
    public async Task UpdateProperty_OnlyDescription_LeavesOtherFieldsUnchanged()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Original",
            Description = "Old Desc",
            Price = 300000m,
            OwnerId = OwnerId
        });

        var dto = new UpdatePropertyDto(PropertyId, null, "New Desc", null, null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Original", result.Data!.Title);
        Assert.Equal("New Desc", result.Data.Description);
        Assert.Equal(300000m, result.Data.Price);
    }

    [Fact]
    public async Task UpdateProperty_Features_UpdatesFlags()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Agent));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Title",
            Description = "Desc",
            Features = PropertyFeature.None,
            OwnerId = OwnerId
        });

        var dto = new UpdatePropertyDto(PropertyId, null, null, null, null, null, null,
            PropertyFeature.Parking | PropertyFeature.Security, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.Features.HasFlag(PropertyFeature.Parking));
        Assert.True(result.Data.Features.HasFlag(PropertyFeature.Security));
    }

    [Fact]
    public async Task UpdateProperty_WithUnknownUser_Fails()
    {
        SetupOwnerLookup(null);

        var dto = new UpdatePropertyDto(PropertyId, "Title", null, null, null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    // ??? Delete

    [Fact]
    public async Task DeleteProperty_ByOwner_Succeeds()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "To Delete",
            Description = "Desc",
            OwnerId = OwnerId
        });

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task DeleteProperty_ByAgent_Succeeds()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Agent));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Agent Delete",
            Description = "Desc",
            OwnerId = OwnerId
        });

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task DeleteProperty_ByDifferentUser_Fails()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Agent));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Title",
            Description = "Desc",
            OwnerId = Guid.NewGuid() // different owner
        });

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PropertyNotOwnedByUser, result.Message);
    }

    [Fact]
    public async Task DeleteProperty_AsCustomer_Fails()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Customer));

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.UnauthorizedPropertyAction, result.Message);
    }

    [Fact]
    public async Task DeleteProperty_PropertyNotFound_Fails()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(null);

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task DeleteProperty_WithUnknownUser_Fails()
    {
        SetupOwnerLookup(null);

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task DeleteProperty_CallsDeleteAndSave()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        var property = new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Title",
            Description = "Desc",
            OwnerId = OwnerId
        };
        SetupPropertyLookup(property);

        await _sut.DeleteProperty(PropertyId, OwnerId);

        _unitOfWorkMock.Verify(u => u.PropertyCommands.DeleteAsync(property), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    // ??? Create ?? interaction verification ??????????????????????????

    [Fact]
    public async Task CreateProperty_CallsInsertAndSave()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        await _sut.CreateProperty(CreateValidDto(), OwnerId);

        _unitOfWorkMock.Verify(u => u.PropertyCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Property>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateProperty_WithAddress_SetsAddressOnProperty()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        Assert.NotEqual(Guid.Empty, result.Data.Property!.AddressId);
    }

    [Fact]
    public async Task CreateProperty_GeocodesAddress_SetsCoordinates()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();
        _geocodingServiceMock
            .Setup(g => g.GeocodeAsync("10 Main St", "Lagos", "Lagos", "Nigeria"))
            .ReturnsAsync((6.5244, 3.3792));

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        Assert.Equal(6.5244, result.Data.Property!.Latitude);
        Assert.Equal(3.3792, result.Data.Property.Longitude);
    }

    [Fact]
    public async Task CreateProperty_WhenGeocodingFails_StillSucceedsWithNullCoordinates()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        Assert.Null(result.Data.Property!.Latitude);
        Assert.Null(result.Data.Property.Longitude);
    }

    [Fact]
    public async Task CreateProperty_SetsOwnerId()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        Assert.Equal(OwnerId, result.Data.Property!.OwnerId);
    }

    [Fact]
    public async Task CreateProperty_SuccessMessage_ContainsProperty()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Contains("property", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ??? Update ?? full field updates ????????????????????????????????

    [Fact]
    public async Task UpdateProperty_AllFields_UpdatesEveryField()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Old",
            Description = "Old Desc",
            PropertyType = PropertyType.Apartment,
            Price = 100000m,
            Availability = PropertyAvailability.Available,
            PropertyLeaseType = PropertyLeaseType.Rent,
            Features = PropertyFeature.None,
            ContactPersonName = "Old Name",
            ContactPersonEmail = "old@test.com",
            ContactPersonPhoneNumber = "000",
            OwnerId = OwnerId
        });

        var dto = new UpdatePropertyDto(
            PropertyId,
            "New Title",
            "New Description",
            PropertyType.Villa,
            900000m,
            PropertyAvailability.Sold,
            PropertyLeaseType.Sale,
            PropertyFeature.SwimmingPool | PropertyFeature.Gym,
            "New Agent",
            "new@agency.com",
            "08011112222",
            null,
            null,
            null);

        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        var data = result.Data!;
        Assert.Equal("New Title", data.Title);
        Assert.Equal("New Description", data.Description);
        Assert.Equal(PropertyType.Villa, data.PropertyType);
        Assert.Equal(900000m, data.Price);
        Assert.Equal(PropertyAvailability.Sold, data.Availability);
        Assert.Equal(PropertyLeaseType.Sale, data.PropertyLeaseType);
        Assert.True(data.Features.HasFlag(PropertyFeature.SwimmingPool));
        Assert.True(data.Features.HasFlag(PropertyFeature.Gym));
        Assert.Equal("New Agent", data.ContactPersonName);
        Assert.Equal("new@agency.com", data.ContactPersonEmail);
        Assert.Equal("08011112222", data.ContactPersonPhoneNumber);
    }

    [Fact]
    public async Task UpdateProperty_CallsUpdateAndSave()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Title",
            Description = "Desc",
            OwnerId = OwnerId
        });

        var dto = new UpdatePropertyDto(PropertyId, "Updated", null, null, null, null, null, null, null, null, null, null, null, null);
        await _sut.UpdateProperty(dto, OwnerId);

        _unitOfWorkMock.Verify(u => u.PropertyCommands.UpdateAsync(It.IsAny<HousingHub.Model.Entities.Property>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateProperty_ByAgent_Succeeds()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Agent));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Agent Property",
            Description = "Desc",
            OwnerId = OwnerId
        });

        var dto = new UpdatePropertyDto(PropertyId, "Agent Updated", null, null, null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Agent Updated", result.Data!.Title);
    }

    [Fact]
    public async Task UpdateProperty_SuccessMessage_ContainsProperty()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Title",
            Description = "Desc",
            OwnerId = OwnerId
        });

        var dto = new UpdatePropertyDto(PropertyId, "Updated", null, null, null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Contains("property", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ??? Delete ?? additional verification ???????????????????????????

    [Fact]
    public async Task DeleteProperty_SuccessMessage_ContainsProperty()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(new HousingHub.Model.Entities.Property
        {
            Id = PropertyId,
            PropertyId = "PROP-TEST",
            Title = "Title",
            Description = "Desc",
            OwnerId = OwnerId
        });

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Contains("property", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ??? Exception handling ??????????????????????????????????????????

    [Fact]
    public async Task CreateProperty_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.False(result.IsSuccessful);
        // The raw exception text is no longer returned to callers — it is logged
        // server-side and the client gets a generic message instead.
        Assert.Equal(ResponseMessages.UnexpectedError, result.Message);
    }

    [Fact]
    public async Task UpdateProperty_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var dto = new UpdatePropertyDto(PropertyId, "Title", null, null, null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        // The raw exception text is no longer returned to callers — it is logged
        // server-side and the client gets a generic message instead.
        Assert.Equal(ResponseMessages.UnexpectedError, result.Message);
    }

    [Fact]
    public async Task DeleteProperty_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.False(result.IsSuccessful);
        // The raw exception text is no longer returned to callers — it is logged
        // server-side and the client gets a generic message instead.
        Assert.Equal(ResponseMessages.UnexpectedError, result.Message);
    }

    [Fact]
    public async Task DeleteProperty_ReturnsFalseData_OnFailure()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Customer));

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.False(result.Data);
    }

    // ??? Create ?? on behalf of a managed owner ??????????????????????

    [Fact]
    public async Task CreateProperty_OnBehalfOfManagedOwner_Succeeds_SetsOwnerIdToTargetOwner()
    {
        var adminId = Guid.NewGuid();
        var targetOwnerId = Guid.NewGuid();
        var managedOwner = new Customer("Target", "Owner", "target@test.com", "08000000001", CustomerType.HouseOwner, "hash")
        {
            Id = targetOwnerId,
            IsManagedByHousingHub = true
        };
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(targetOwnerId)).ReturnsAsync(managedOwner);
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), adminId, targetOwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        Assert.Equal(targetOwnerId, result.Data.Property!.OwnerId);
    }

    [Fact]
    public async Task CreateProperty_OnBehalfOfOwner_NotManagedByHousingHub_Fails()
    {
        var adminId = Guid.NewGuid();
        var targetOwnerId = Guid.NewGuid();
        var unmanagedOwner = new Customer("Target", "Owner", "target@test.com", "08000000002", CustomerType.HouseOwner, "hash")
        {
            Id = targetOwnerId,
            IsManagedByHousingHub = false
        };
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(targetOwnerId)).ReturnsAsync(unmanagedOwner);

        var result = await _sut.CreateProperty(CreateValidDto(), adminId, targetOwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.OwnerNotManagedByHousingHub, result.Message);
    }

    [Fact]
    public async Task CreateProperty_OnBehalfOfOwner_WrongCustomerType_Fails()
    {
        var adminId = Guid.NewGuid();
        var targetOwnerId = Guid.NewGuid();
        var wrongTypeOwner = new Customer("Target", "Owner", "target@test.com", "08000000003", CustomerType.Customer, "hash")
        {
            Id = targetOwnerId,
            IsManagedByHousingHub = true
        };
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(targetOwnerId)).ReturnsAsync(wrongTypeOwner);

        var result = await _sut.CreateProperty(CreateValidDto(), adminId, targetOwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.UnauthorizedPropertyAction, result.Message);
    }

    [Fact]
    public async Task CreateProperty_OnBehalfOfNonexistentOwner_Fails()
    {
        var adminId = Guid.NewGuid();
        var targetOwnerId = Guid.NewGuid();
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(targetOwnerId)).ReturnsAsync((Customer?)null);

        var result = await _sut.CreateProperty(CreateValidDto(), adminId, targetOwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    // ??? Create ?? possible-duplicate detection ??????????????????????

    private HousingHub.Model.Entities.Property CreateExistingPropertyAt(double lat, double lng, Guid addressId) =>
        new("Existing Listing", "Existing description", PropertyType.Apartment, 250000m,
            PropertyAvailability.Available, PropertyLeaseType.Sale)
        {
            Id = Guid.NewGuid(),
            Latitude = lat,
            Longitude = lng,
            AddressId = addressId
        };

    [Fact]
    public async Task CreateProperty_DuplicateFoundByCoordinates_ConfirmDuplicateFalse_ReturnsWarningWithoutPersisting()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        _geocodingServiceMock
            .Setup(g => g.GeocodeAsync("10 Main St", "Lagos", "Lagos", "Nigeria"))
            .ReturnsAsync((6.5244, 3.3792));

        var existingAddressId = Guid.NewGuid();
        var existingProperty = CreateExistingPropertyAt(6.5244, 3.3792, existingAddressId);
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetAllAsync()).ReturnsAsync(new List<HousingHub.Model.Entities.Property> { existingProperty });
        _unitOfWorkMock
            .Setup(u => u.PropertyAddressQueries.GetByIdAsync(existingAddressId))
            .ReturnsAsync(new HousingHub.Model.Entities.PropertyAddress("10 Main St", "Lagos", "Lagos", "Nigeria", "100001") { Id = existingAddressId });

        var dto = CreateValidDto() with { ConfirmDuplicate = false };
        var result = await _sut.CreateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Data!.Property);
        Assert.NotNull(result.Data.PossibleDuplicate);
        Assert.Equal(existingProperty.Id, result.Data.PossibleDuplicate!.PropertyId);
        _unitOfWorkMock.Verify(u => u.PropertyCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Property>()), Times.Never);
    }

    [Fact]
    public async Task CreateProperty_DuplicateFoundByCoordinates_ConfirmDuplicateTrue_CreatesAndFlagsAsDuplicate()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();
        _geocodingServiceMock
            .Setup(g => g.GeocodeAsync("10 Main St", "Lagos", "Lagos", "Nigeria"))
            .ReturnsAsync((6.5244, 3.3792));

        var existingAddressId = Guid.NewGuid();
        var existingProperty = CreateExistingPropertyAt(6.5244, 3.3792, existingAddressId);
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetAllAsync()).ReturnsAsync(new List<HousingHub.Model.Entities.Property> { existingProperty });

        HousingHub.Model.Entities.Property? insertedProperty = null;
        _unitOfWorkMock
            .Setup(u => u.PropertyCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Property>()))
            .Callback<HousingHub.Model.Entities.Property>(p => insertedProperty = p)
            .ReturnsAsync(true);

        var dto = CreateValidDto() with { ConfirmDuplicate = true };
        var result = await _sut.CreateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        _unitOfWorkMock.Verify(u => u.PropertyCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Property>()), Times.Once);
        Assert.NotNull(insertedProperty);
        Assert.True(insertedProperty!.IsFlaggedDuplicate);
        Assert.Equal(existingProperty.Id, insertedProperty.PossibleDuplicateOfPropertyId);
    }

    [Fact]
    public async Task CreateProperty_NoMatchingDuplicate_CreatesNormally_NotFlagged()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();
        // Default mock setup already returns an empty list from PropertyQueries.GetAllAsync(),
        // so there's nothing for FindPossibleDuplicateAsync to match against.

        HousingHub.Model.Entities.Property? insertedProperty = null;
        _unitOfWorkMock
            .Setup(u => u.PropertyCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Property>()))
            .Callback<HousingHub.Model.Entities.Property>(p => insertedProperty = p)
            .ReturnsAsync(true);

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Property);
        Assert.NotNull(insertedProperty);
        Assert.False(insertedProperty!.IsFlaggedDuplicate);
        Assert.Null(insertedProperty.PossibleDuplicateOfPropertyId);
    }

    // ??? DismissDuplicateFlagAsync ????????????????????????????????????

    [Fact]
    public async Task DismissDuplicateFlagAsync_ExistingProperty_ClearsFlagAndSucceeds()
    {
        var property = new HousingHub.Model.Entities.Property("Flagged Listing", "Desc", PropertyType.Apartment,
            150000m, PropertyAvailability.Available, PropertyLeaseType.Rent)
        {
            Id = PropertyId,
            IsFlaggedDuplicate = true,
            PossibleDuplicateOfPropertyId = Guid.NewGuid()
        };
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByIdAsync(PropertyId)).ReturnsAsync(property);

        var result = await _sut.DismissDuplicateFlagAsync(PropertyId);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data);
        Assert.False(property.IsFlaggedDuplicate);
        Assert.Null(property.PossibleDuplicateOfPropertyId);
        _unitOfWorkMock.Verify(u => u.PropertyCommands.UpdateAsync(property), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task DismissDuplicateFlagAsync_PropertyNotFound_Fails()
    {
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByIdAsync(PropertyId)).ReturnsAsync((HousingHub.Model.Entities.Property?)null);

        var result = await _sut.DismissDuplicateFlagAsync(PropertyId);

        Assert.False(result.IsSuccessful);
        Assert.False(result.Data);
        Assert.Contains("Not Found", result.Message);
    }

    // ??? SetPropertyPublishedInternalAsync ?? saved-search alert matching ????

    [Fact]
    public async Task SetPropertyPublished_FirstTimePublish_WithMatchingPreference_NotifiesMatchingCustomer()
    {
        var property = new HousingHub.Model.Entities.Property("Alert Match", "Desc", PropertyType.Apartment,
            200000m, PropertyAvailability.Available, PropertyLeaseType.Rent)
        {
            Id = Guid.NewGuid(),
            IsPublished = false,
            OwnerId = Guid.NewGuid(),
            AddressId = Guid.NewGuid()
        };
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<HousingHub.Model.Entities.Property>())).Returns(Task.CompletedTask);

        var address = new HousingHub.Model.Entities.PropertyAddress("1 Match Rd", "Ikeja", "Lagos", "Nigeria", "100001") { Id = property.AddressId };
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.GetByIdAsync(property.AddressId)).ReturnsAsync(address);

        var matchingCustomer = new Customer("Alice", "Buyer", "alice@test.com", "08033334444", CustomerType.Customer, "hash")
        {
            Id = Guid.NewGuid()
        };
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(matchingCustomer.Id)).ReturnsAsync(matchingCustomer);

        // All filter fields null == "any" per PropertyAlertPreference.Matches, so this always matches.
        var preference = new PropertyAlertPreference(matchingCustomer.Id, null, null, null, null, null, null);
        _propertyAlertPreferenceQueryServiceMock.Setup(p => p.GetAllActiveAsync()).ReturnsAsync(new List<PropertyAlertPreference> { preference });

        var result = await _sut.SetPropertyPublishedAsync(property.Id, true);

        Assert.True(result.IsSuccessful);
        _unitOfWorkMock.Verify(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>()), Times.Once);
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(matchingCustomer.Id, It.IsAny<NotificationDto>()), Times.Once);
        _emailServiceMock.Verify(
            e => e.SendPropertyAlertMatchAsync(matchingCustomer.Email, matchingCustomer.FirstName, property.Title, It.IsAny<string>(), property.Price),
            Times.Once);
    }

    [Fact]
    public async Task SetPropertyPublished_AlreadyPublished_DoesNotReNotify()
    {
        var property = new HousingHub.Model.Entities.Property("Already Live", "Desc", PropertyType.Apartment,
            200000m, PropertyAvailability.Available, PropertyLeaseType.Rent)
        {
            Id = Guid.NewGuid(),
            IsPublished = true,
            OwnerId = Guid.NewGuid(),
            AddressId = Guid.NewGuid()
        };
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<HousingHub.Model.Entities.Property>())).Returns(Task.CompletedTask);

        // Even with a matching preference present, re-publishing an already-published
        // property is a no-op toggle and must not re-fire the alert.
        var preference = new PropertyAlertPreference(Guid.NewGuid(), null, null, null, null, null, null);
        _propertyAlertPreferenceQueryServiceMock.Setup(p => p.GetAllActiveAsync()).ReturnsAsync(new List<PropertyAlertPreference> { preference });

        var result = await _sut.SetPropertyPublishedAsync(property.Id, true);

        Assert.True(result.IsSuccessful);
        _unitOfWorkMock.Verify(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>()), Times.Never);
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>()), Times.Never);
    }

    // ??? Helpers ??????????????????????????????????????????????????????

    private void SetupOwnerLookup(Customer? customer)
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customer);
    }

    private void SetupPropertyLookup(HousingHub.Model.Entities.Property? property)
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ReturnsAsync(property);
    }

    private void SetupInsertSuccess()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Property>()))
            .ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
    }
}
