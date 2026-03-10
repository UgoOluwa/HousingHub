using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyAddress;
using HousingHub.Service.PropertyService;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Property;

public class PropertyCommandServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly IMapper _mapper;
    private readonly PropertyCommandService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();

    public PropertyCommandServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        var configExpression = new MapperConfigurationExpression();
        configExpression.AddProfile<PropertyMapper>();
        var config = new MapperConfiguration(configExpression);
        _mapper = config.CreateMapper();
        var logger = NullLogger<PropertyCommandService>.Instance;
        _sut = new PropertyCommandService(logger, _unitOfWorkMock.Object, _mapper, _fileStorageServiceMock.Object);
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
        PropertyAddress: new CreatePropertyAddressDto("10 Main St", "Lagos", "Lagos", "Nigeria", "100001", Guid.Empty));

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
        Assert.Equal("Nice Apartment", result.Data.Title);
        Assert.StartsWith("PROP-", result.Data.PropertyId);
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
        Assert.True(result.Data!.Features.HasFlag(PropertyFeature.Parking));
        Assert.True(result.Data.Features.HasFlag(PropertyFeature.Security));
    }

    [Fact]
    public async Task CreateProperty_SetsContactPerson()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Agent));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Agent Smith", result.Data!.ContactPersonName);
        Assert.Equal("smith@agency.com", result.Data.ContactPersonEmail);
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

        var dto = new UpdatePropertyDto(PropertyId, "New Title", null, null, 600000m, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("New Title", result.Data!.Title);
        Assert.Equal(600000m, result.Data.Price);
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

        var dto = new UpdatePropertyDto(PropertyId, "Hacked", null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PropertyNotOwnedByUser, result.Message);
    }

    [Fact]
    public async Task UpdateProperty_AsCustomer_Fails()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Customer));

        var dto = new UpdatePropertyDto(PropertyId, "Title", null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.UnauthorizedPropertyAction, result.Message);
    }

    [Fact]
    public async Task UpdateProperty_PropertyNotFound_Fails()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupPropertyLookup(null);

        var dto = new UpdatePropertyDto(PropertyId, "Title", null, null, null, null, null, null, null, null, null, null);
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
            PropertyAddress: null);

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
        var data = result.Data!;
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

        var dto = new UpdatePropertyDto(PropertyId, null, "New Desc", null, null, null, null, null, null, null, null, null);
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
            PropertyFeature.Parking | PropertyFeature.Security, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.Features.HasFlag(PropertyFeature.Parking));
        Assert.True(result.Data.Features.HasFlag(PropertyFeature.Security));
    }

    [Fact]
    public async Task UpdateProperty_WithUnknownUser_Fails()
    {
        SetupOwnerLookup(null);

        var dto = new UpdatePropertyDto(PropertyId, "Title", null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    // ??? Delete ???????????????????????????????????????????????????????

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

        _unitOfWorkMock.Verify(u => u.PropertyCommands.Delete(property), Times.Once);
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
        Assert.NotEqual(Guid.Empty, result.Data!.AddressId);
    }

    [Fact]
    public async Task CreateProperty_SetsOwnerId()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.HouseOwner));
        SetupInsertSuccess();

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(OwnerId, result.Data!.OwnerId);
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

        var dto = new UpdatePropertyDto(PropertyId, "Updated", null, null, null, null, null, null, null, null, null, null);
        await _sut.UpdateProperty(dto, OwnerId);

        _unitOfWorkMock.Verify(u => u.PropertyCommands.Update(It.IsAny<HousingHub.Model.Entities.Property>()), Times.Once);
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

        var dto = new UpdatePropertyDto(PropertyId, "Agent Updated", null, null, null, null, null, null, null, null, null, null);
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

        var dto = new UpdatePropertyDto(PropertyId, "Updated", null, null, null, null, null, null, null, null, null, null);
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
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>(), It.IsAny<FindOptions>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.CreateProperty(CreateValidDto(), OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("DB error", result.Message);
    }

    [Fact]
    public async Task UpdateProperty_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>(), It.IsAny<FindOptions>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var dto = new UpdatePropertyDto(PropertyId, "Title", null, null, null, null, null, null, null, null, null, null);
        var result = await _sut.UpdateProperty(dto, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("DB error", result.Message);
    }

    [Fact]
    public async Task DeleteProperty_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>(), It.IsAny<FindOptions>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.Contains("DB error", result.Message);
    }

    [Fact]
    public async Task DeleteProperty_ReturnsFalseData_OnFailure()
    {
        SetupOwnerLookup(CreateOwner(CustomerType.Customer));

        var result = await _sut.DeleteProperty(PropertyId, OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.False(result.Data);
    }

    // ??? Helpers ??????????????????????????????????????????????????????

    private void SetupOwnerLookup(Customer? customer)
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>(), It.IsAny<FindOptions>()))
            .ReturnsAsync(customer);
    }

    private void SetupPropertyLookup(HousingHub.Model.Entities.Property? property)
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(), It.IsAny<FindOptions>()))
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
