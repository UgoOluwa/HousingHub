using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Property;

public class PropertyQueryServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly IMapper _mapper;
    private readonly PropertyQueryService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid PropertyGuid = Guid.NewGuid();

    public PropertyQueryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk>();
        var configExpression = new MapperConfigurationExpression();
        configExpression.AddProfile<PropertyMapper>();
        var config = new MapperConfiguration(configExpression);
        _mapper = config.CreateMapper();
        var logger = NullLogger<PropertyQueryService>.Instance;
        _sut = new PropertyQueryService(_unitOfWorkMock.Object, _mapper, logger);
    }

    private static HousingHub.Model.Entities.Property CreateSampleProperty(
        Guid? id = null, string propertyId = "PROP-TEST", string title = "Sample") => new()
    {
        Id = id ?? PropertyGuid,
        PropertyId = propertyId,
        Title = title,
        Description = "A sample property",
        PropertyType = PropertyType.Apartment,
        Price = 250000m,
        Availability = PropertyAvailability.Available,
        PropertyLeaseType = PropertyLeaseType.Sale,
        Features = PropertyFeature.Parking,
        OwnerId = OwnerId
    };

    // ??? GetPropertyAsync (by Guid) ??????????????????????????????????

    [Fact]
    public async Task GetPropertyAsync_WhenExists_ReturnsProperty()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("Sample", result.Data!.Title);
        Assert.Equal(PropertyGuid, result.Data.Id);
    }

    [Fact]
    public async Task GetPropertyAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync((HousingHub.Model.Entities.Property?)null);

        var result = await _sut.GetPropertyAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task GetPropertyAsync_MapsAllFields()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid);

        Assert.True(result.IsSuccessful);
        var data = result.Data!;
        Assert.Equal(PropertyType.Apartment, data.PropertyType);
        Assert.Equal(250000m, data.Price);
        Assert.Equal(PropertyAvailability.Available, data.Availability);
        Assert.Equal(PropertyLeaseType.Sale, data.PropertyLeaseType);
        Assert.True(data.Features.HasFlag(PropertyFeature.Parking));
        Assert.Equal(OwnerId, data.OwnerId);
    }

    // ??? GetPropertyByPropertyIdAsync (by string id) ?????????????????

    [Fact]
    public async Task GetPropertyByPropertyIdAsync_WhenExists_ReturnsProperty()
    {
        var property = CreateSampleProperty(propertyId: "PROP-20250101-ABC123");
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyByPropertyIdAsync("PROP-20250101-ABC123");

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("PROP-20250101-ABC123", result.Data!.PropertyId);
    }

    [Fact]
    public async Task GetPropertyByPropertyIdAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync((HousingHub.Model.Entities.Property?)null);

        var result = await _sut.GetPropertyByPropertyIdAsync("PROP-NONEXISTENT");

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        Assert.Contains("Not Found", result.Message);
    }

    // ??? GetAllPropertiesAsync ????????????????????????????????????????

    [Fact]
    public async Task GetAllPropertiesAsync_ReturnsAllProperties()
    {
        var properties = new List<HousingHub.Model.Entities.Property>
        {
            CreateSampleProperty(Guid.NewGuid(), "PROP-001", "First"),
            CreateSampleProperty(Guid.NewGuid(), "PROP-002", "Second"),
            CreateSampleProperty(Guid.NewGuid(), "PROP-003", "Third")
        };
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<FindOptions>()))
            .ReturnsAsync(properties);

        var result = await _sut.GetAllPropertiesAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(3, result.Data!.Count);
        Assert.Equal("First", result.Data[0].Title);
        Assert.Equal("Second", result.Data[1].Title);
        Assert.Equal("Third", result.Data[2].Title);
    }

    [Fact]
    public async Task GetAllPropertiesAsync_WhenEmpty_ReturnsEmptyList()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<FindOptions>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property>());

        var result = await _sut.GetAllPropertiesAsync();

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetAllPropertiesAsync_ReturnsMappedDtos()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<FindOptions>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property> { property });

        var result = await _sut.GetAllPropertiesAsync();

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
        var dto = result.Data[0];
        Assert.Equal(property.Title, dto.Title);
        Assert.Equal(property.Price, dto.Price);
        Assert.Equal(property.PropertyType, dto.PropertyType);
    }

    // ??? GetPropertiesByOwnerAsync ????????????????????????????????????

    [Fact]
    public async Task GetPropertiesByOwnerAsync_ReturnsOwnersProperties()
    {
        var properties = new List<HousingHub.Model.Entities.Property>
        {
            CreateSampleProperty(Guid.NewGuid(), "PROP-A", "Owner Prop 1"),
            CreateSampleProperty(Guid.NewGuid(), "PROP-B", "Owner Prop 2")
        };
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(properties);

        var result = await _sut.GetPropertiesByOwnerAsync(OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task GetPropertiesByOwnerAsync_WhenOwnerHasNoProperties_ReturnsEmptyList()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property>());

        var result = await _sut.GetPropertiesByOwnerAsync(Guid.NewGuid());

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetPropertiesByOwnerAsync_SuccessMessageIsSet()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property>());

        var result = await _sut.GetPropertiesByOwnerAsync(OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.Successful, result.Message);
    }

    // ??? GetPropertyAsync ?? additional coverage ??????????????????????

    [Fact]
    public async Task GetPropertyAsync_SuccessMessageIsSet()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.Successful, result.Message);
    }

    [Fact]
    public async Task GetPropertyAsync_ReturnsCorrectPropertyId()
    {
        var property = CreateSampleProperty(propertyId: "PROP-20250101-XYZ789");
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid);

        Assert.True(result.IsSuccessful);
        Assert.Equal("PROP-20250101-XYZ789", result.Data!.PropertyId);
    }

    // ??? GetPropertyByPropertyIdAsync ?? additional coverage ??????????

    [Fact]
    public async Task GetPropertyByPropertyIdAsync_MapsAllFields()
    {
        var property = CreateSampleProperty(propertyId: "PROP-FULL");
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyByPropertyIdAsync("PROP-FULL");

        Assert.True(result.IsSuccessful);
        var data = result.Data!;
        Assert.Equal("Sample", data.Title);
        Assert.Equal(PropertyType.Apartment, data.PropertyType);
        Assert.Equal(250000m, data.Price);
        Assert.Equal(PropertyAvailability.Available, data.Availability);
        Assert.Equal(PropertyLeaseType.Sale, data.PropertyLeaseType);
    }

    [Fact]
    public async Task GetPropertyByPropertyIdAsync_SuccessMessageIsSet()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyByPropertyIdAsync("PROP-TEST");

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.Successful, result.Message);
    }

    // ??? GetAllPropertiesAsync ?? additional coverage ?????????????????

    [Fact]
    public async Task GetAllPropertiesAsync_PreservesPropertyOrder()
    {
        var properties = new List<HousingHub.Model.Entities.Property>
        {
            CreateSampleProperty(Guid.NewGuid(), "PROP-C", "Charlie"),
            CreateSampleProperty(Guid.NewGuid(), "PROP-A", "Alpha"),
            CreateSampleProperty(Guid.NewGuid(), "PROP-B", "Bravo")
        };
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<FindOptions>()))
            .ReturnsAsync(properties);

        var result = await _sut.GetAllPropertiesAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal("Charlie", result.Data![0].Title);
        Assert.Equal("Alpha", result.Data[1].Title);
        Assert.Equal("Bravo", result.Data[2].Title);
    }

    [Fact]
    public async Task GetAllPropertiesAsync_SuccessMessageIsSet()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<FindOptions>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property>());

        var result = await _sut.GetAllPropertiesAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(ResponseMessages.Successful, result.Message);
    }

    [Fact]
    public async Task GetAllPropertiesAsync_WithDifferentPropertyTypes_ReturnsMixed()
    {
        var apt = CreateSampleProperty(Guid.NewGuid(), "PROP-1", "Apt");
        apt.PropertyType = PropertyType.Apartment;
        var villa = CreateSampleProperty(Guid.NewGuid(), "PROP-2", "Villa");
        villa.PropertyType = PropertyType.Villa;
        var land = CreateSampleProperty(Guid.NewGuid(), "PROP-3", "Land");
        land.PropertyType = PropertyType.Land;

        var properties = new List<HousingHub.Model.Entities.Property> { apt, villa, land };
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<FindOptions>()))
            .ReturnsAsync(properties);

        var result = await _sut.GetAllPropertiesAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(3, result.Data!.Count);
        Assert.Equal(PropertyType.Apartment, result.Data[0].PropertyType);
        Assert.Equal(PropertyType.Villa, result.Data[1].PropertyType);
        Assert.Equal(PropertyType.Land, result.Data[2].PropertyType);
    }

    // ??? GetPropertiesByOwnerAsync ?? additional coverage ?????????????

    [Fact]
    public async Task GetPropertiesByOwnerAsync_ReturnsMappedDtos()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property> { property });

        var result = await _sut.GetPropertiesByOwnerAsync(OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
        var dto = result.Data[0];
        Assert.Equal(property.Title, dto.Title);
        Assert.Equal(property.Price, dto.Price);
        Assert.Equal(property.PropertyType, dto.PropertyType);
        Assert.Equal(property.Features, dto.Features);
    }

    // ??? Exception handling ???????????????????????????????????????????

    [Fact]
    public async Task GetPropertyAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.GetPropertyAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        Assert.Contains("DB error", result.Message);
    }

    [Fact]
    public async Task GetPropertyByPropertyIdAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var result = await _sut.GetPropertyByPropertyIdAsync("PROP-ERR");

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        Assert.Contains("Connection failed", result.Message);
    }

    [Fact]
    public async Task GetAllPropertiesAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<FindOptions>()))
            .ThrowsAsync(new InvalidOperationException("Timeout"));

        var result = await _sut.GetAllPropertiesAsync();

        Assert.False(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Contains("Timeout", result.Message);
    }

    [Fact]
    public async Task GetPropertiesByOwnerAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(),
                It.IsAny<FindOptions>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var result = await _sut.GetPropertiesByOwnerAsync(OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Contains("Network error", result.Message);
    }
}
