using Mapster;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Properties;

public class PropertyQueryServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly IMapper _mapper;
    private readonly PropertyQueryService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid PropertyGuid = Guid.NewGuid();

    public PropertyQueryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var config = new TypeAdapterConfig();
        new PropertyMapper().Register(config);
        _mapper = new ObjectMapper(config);
        var logger = NullLogger<PropertyQueryService>.Instance;

        // GetPropertyAsync increments ViewCount via UpdateAsync, so these must be set up
        _unitOfWorkMock.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<HousingHub.Model.Entities.Property>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        // Every read path now attaches PropertyFiles before mapping; default to none.
        // Single-property reads still go through the predicate overload...
        _unitOfWorkMock
            .Setup(u => u.PropertyFileQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.PropertyFile>());

        // ...while the bulk path uses GetManyByAsync, so that an index can be used
        // instead of scanning PropertyFiles. Without this setup Moq hands back a null
        // task result and the ToLookup in AttachFilesAsync throws.
        _unitOfWorkMock
            .Setup(u => u.PropertyFileQueries.GetManyByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, Guid>>>(),
                It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.PropertyFile>());

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
        OwnerId = OwnerId,
        IsPublished = true
    };

    // ??? GetPropertyAsync (by Guid) ??????????????????????????????????

    [Fact]
    public async Task GetPropertyAsync_WhenExists_ReturnsProperty()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("Sample", result.Data!.Title);
        Assert.Equal(PropertyGuid, result.Data.Id);
    }

    [Fact]
    public async Task GetPropertyAsync_EnrichesWithAddressAndOwnerName()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
            .ReturnsAsync(property);

        var address = new HousingHub.Model.Entities.PropertyAddress("Plot 4", "Lekki", "Lagos", "Nigeria", "100001")
        {
            PropertyId = property.Id
        };
        _unitOfWorkMock
            .Setup(u => u.PropertyAddressQueries.GetByAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync(address);

        var owner = new HousingHub.Model.Entities.Customer("Jane", "Doe", "jane@test.com", "08000000000", CustomerType.HouseOwner, "hash")
        {
            Id = OwnerId
        };
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByIdAsync(OwnerId))
            .ReturnsAsync(owner);

        var result = await _sut.GetPropertyAsync(PropertyGuid);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.PropertyAddress);
        Assert.Equal("Lekki", result.Data.PropertyAddress!.City);
        Assert.Equal("Jane Doe", result.Data.OwnerName);
    }

    [Fact]
    public async Task GetPropertyAsync_NoAddressOrOwnerFound_LeavesThemNull()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
            .ReturnsAsync(property);
        _unitOfWorkMock
            .Setup(u => u.PropertyAddressQueries.GetByAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync((HousingHub.Model.Entities.PropertyAddress?)null);
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((HousingHub.Model.Entities.Customer?)null);

        var result = await _sut.GetPropertyAsync(PropertyGuid);

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Data!.PropertyAddress);
        Assert.Null(result.Data.OwnerName);
    }

    [Fact]
    public async Task GetPropertyAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((HousingHub.Model.Entities.Property?)null);

        var result = await _sut.GetPropertyAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        Assert.Equal(ResponseMessages.SetNotFoundMessage("property"), result.Message);
    }

    [Fact]
    public async Task GetPropertyAsync_WhenUnpublishedAndNoRequester_ReturnsNotFound()
    {
        var property = CreateSampleProperty();
        property.IsPublished = false;
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid);

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        Assert.Equal(ResponseMessages.SetNotFoundMessage("property"), result.Message);
    }

    [Fact]
    public async Task GetPropertyAsync_WhenUnpublishedAndRequesterIsOwner_ReturnsProperty()
    {
        var property = CreateSampleProperty();
        property.IsPublished = false;
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid, requesterId: OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(PropertyGuid, result.Data!.Id);
    }

    [Fact]
    public async Task GetPropertyAsync_WhenUnpublishedAndRequesterIsNotOwner_ReturnsNotFound()
    {
        var property = CreateSampleProperty();
        property.IsPublished = false;
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid, requesterId: Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        Assert.Equal(ResponseMessages.SetNotFoundMessage("property"), result.Message);
    }

    [Fact]
    public async Task GetPropertyAsync_WhenUnpublishedAndIncludeUnpublished_ReturnsProperty()
    {
        var property = CreateSampleProperty();
        property.IsPublished = false;
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid, includeUnpublished: true);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(PropertyGuid, result.Data!.Id);
    }

    [Fact]
    public async Task GetPropertyAsync_MapsAllFields()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
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
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ReturnsAsync((HousingHub.Model.Entities.Property?)null);

        var result = await _sut.GetPropertyByPropertyIdAsync("PROP-NONEXISTENT");

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        Assert.Equal(ResponseMessages.SetNotFoundMessage("property"), result.Message);
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
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property> { property });

        var result = await _sut.GetAllPropertiesAsync();

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
        Assert.Equal(property.Title, result.Data[0].Title);
    }

    [Fact]
    public async Task GetAllPropertiesAsync_Default_FiltersToPublishedOnly()
    {
        Expression<Func<HousingHub.Model.Entities.Property, bool>>? capturedPredicate = null;
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .Callback<Expression<Func<HousingHub.Model.Entities.Property, bool>>>(p => capturedPredicate = p)
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property>());

        await _sut.GetAllPropertiesAsync();

        var published = CreateSampleProperty();
        var unpublished = CreateSampleProperty();
        unpublished.IsPublished = false;

        var predicate = capturedPredicate!.Compile();
        Assert.True(predicate(published));
        Assert.False(predicate(unpublished));
    }

    [Fact]
    public async Task GetAllPropertiesAsync_IncludeUnpublished_ReturnsUnpublishedToo()
    {
        // includeUnpublished bypasses the published-only predicate entirely (an OR
        // can't be narrowed to an index) and reads everything via the parameterless
        // GetAllAsync() instead.
        var unpublished = CreateSampleProperty();
        unpublished.IsPublished = false;
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync())
            .ReturnsAsync(new List<HousingHub.Model.Entities.Property> { unpublished });

        var result = await _sut.GetAllPropertiesAsync(includeUnpublished: true);

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
        Assert.Equal(unpublished.Id, result.Data![0].Id);
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
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
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
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
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
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
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
            .Setup(u => u.PropertyQueries.GetByIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _sut.GetPropertyAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        // The raw exception text is no longer returned to callers — it is logged
        // server-side and the client gets a generic message instead.
        Assert.Equal(ResponseMessages.UnexpectedError, result.Message);
    }

    [Fact]
    public async Task GetPropertyByPropertyIdAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var result = await _sut.GetPropertyByPropertyIdAsync("PROP-ERR");

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
        // The raw exception text is no longer returned to callers — it is logged
        // server-side and the client gets a generic message instead.
        Assert.Equal(ResponseMessages.UnexpectedError, result.Message);
    }

    [Fact]
    public async Task GetAllPropertiesAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("Timeout"));

        var result = await _sut.GetAllPropertiesAsync();

        Assert.False(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        // The raw exception text is no longer returned to callers — it is logged
        // server-side and the client gets a generic message instead.
        Assert.Equal(ResponseMessages.UnexpectedError, result.Message);
    }

    [Fact]
    public async Task GetPropertiesByOwnerAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var result = await _sut.GetPropertiesByOwnerAsync(OwnerId);

        Assert.False(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        // The raw exception text is no longer returned to callers — it is logged
        // server-side and the client gets a generic message instead.
        Assert.Equal(ResponseMessages.UnexpectedError, result.Message);
    }

    // ??? GetPropertiesByOwnerPaginatedAsync ? InspectionCount ??????????

    private static PropertyInspection CreateInspection(Guid propertyId, InspectionStatus status) =>
        new(Guid.NewGuid(), propertyId, DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), null)
        {
            Status = status
        };

    [Fact]
    public async Task GetPropertiesByOwnerPaginatedAsync_CountsOnlyOpenInspections()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetPagedAsync(1, 10, It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ReturnsAsync((new List<HousingHub.Model.Entities.Property> { property }, 1));

        // The mocked repository stands in for a DynamoDB query that already applies
        // the predicate server-side, so it returns only the open (Pending/Rescheduled)
        // inspections the real production predicate would match — Completed and
        // Cancelled inspections for this property are deliberately excluded here.
        var openInspections = new List<PropertyInspection>
        {
            CreateInspection(property.Id, InspectionStatus.Pending),
            CreateInspection(property.Id, InspectionStatus.Rescheduled),
        };
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetManyByAsync(
                It.IsAny<Expression<Func<PropertyInspection, Guid>>>(),
                It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(openInspections);

        var result = await _sut.GetPropertiesByOwnerPaginatedAsync(OwnerId, new GetMyPropertiesFilterDto { PageNumber = 1, PageSize = 10 });

        Assert.True(result.IsSuccessful);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal(2, dto.InspectionCount);
    }

    [Fact]
    public async Task GetPropertiesByOwnerPaginatedAsync_WithNoInspections_ReturnsZeroCount()
    {
        var property = CreateSampleProperty();
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetPagedAsync(1, 10, It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ReturnsAsync((new List<HousingHub.Model.Entities.Property> { property }, 1));
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetManyByAsync(
                It.IsAny<Expression<Func<PropertyInspection, Guid>>>(),
                It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new List<PropertyInspection>());

        var result = await _sut.GetPropertiesByOwnerPaginatedAsync(OwnerId, new GetMyPropertiesFilterDto { PageNumber = 1, PageSize = 10 });

        Assert.True(result.IsSuccessful);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal(0, dto.InspectionCount);
    }

    // ── Bedroom / bathroom filtering ──────────────────────────────────

    private void SetupPublishedProperties(params HousingHub.Model.Entities.Property[] properties)
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Property, bool>>>()))
            .ReturnsAsync(properties.ToList());
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetManyByAsync(
                It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, Guid>>>(),
                It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.Customer>());
    }

    private static HousingHub.Model.Entities.Property CreatePropertyWithRooms(
        string title, int? bedrooms, int? bathrooms = null)
    {
        var property = CreateSampleProperty(Guid.NewGuid(), $"PROP-{title}", title);
        property.Bedrooms = bedrooms;
        property.Bathrooms = bathrooms;
        return property;
    }

    [Fact]
    public async Task GetAllPropertiesPaginatedAsync_FiltersByBedrooms()
    {
        SetupPublishedProperties(
            CreatePropertyWithRooms("TwoBed", 2),
            CreatePropertyWithRooms("ThreeBed", 3),
            CreatePropertyWithRooms("SixBed", 6));

        var result = await _sut.GetAllPropertiesPaginatedAsync(
            new GetAllPropertiesFilterDto { PageNumber = 1, PageSize = 10, Bedrooms = 3 });

        Assert.True(result.IsSuccessful);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal("ThreeBed", dto.Title);
    }

    [Fact]
    public async Task GetAllPropertiesPaginatedAsync_FiltersByBathrooms()
    {
        SetupPublishedProperties(
            CreatePropertyWithRooms("OneBath", 3, bathrooms: 1),
            CreatePropertyWithRooms("TwoBath", 3, bathrooms: 2));

        var result = await _sut.GetAllPropertiesPaginatedAsync(
            new GetAllPropertiesFilterDto { PageNumber = 1, PageSize = 10, Bathrooms = 2 });

        Assert.True(result.IsSuccessful);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal("TwoBath", dto.Title);
    }

    /// <summary>
    /// A listing whose owner never stated a bedroom count must not answer a search for
    /// a specific one. Every listing created before the field existed reads as null, so
    /// the alternative would be answering "3 bedrooms" with listings that never claimed
    /// to have three.
    /// </summary>
    [Fact]
    public async Task GetAllPropertiesPaginatedAsync_ExcludesListingsWithNoStatedBedroomCount()
    {
        SetupPublishedProperties(
            CreatePropertyWithRooms("Unstated", null),
            CreatePropertyWithRooms("ThreeBed", 3));

        var result = await _sut.GetAllPropertiesPaginatedAsync(
            new GetAllPropertiesFilterDto { PageNumber = 1, PageSize = 10, Bedrooms = 3 });

        Assert.True(result.IsSuccessful);
        var dto = Assert.Single(result.Data!.Items);
        Assert.Equal("ThreeBed", dto.Title);
    }

    /// <summary>
    /// The inverse: with no bedroom filter asked for, a listing that never stated a
    /// count is still a listing and must still be returned.
    /// </summary>
    [Fact]
    public async Task GetAllPropertiesPaginatedAsync_WithoutBedroomFilter_ReturnsListingsWithNoStatedCount()
    {
        SetupPublishedProperties(
            CreatePropertyWithRooms("Unstated", null),
            CreatePropertyWithRooms("ThreeBed", 3));

        var result = await _sut.GetAllPropertiesPaginatedAsync(
            new GetAllPropertiesFilterDto { PageNumber = 1, PageSize = 10 });

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Items.Count);
    }

    [Fact]
    public async Task GetPropertyAsync_CarriesRoomCountsThrough()
    {
        var property = CreateSampleProperty();
        property.Bedrooms = 4;
        property.Bathrooms = 3;
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetByIdAsync(PropertyGuid))
            .ReturnsAsync(property);

        var result = await _sut.GetPropertyAsync(PropertyGuid);

        Assert.True(result.IsSuccessful);
        Assert.Equal(4, result.Data!.Bedrooms);
        Assert.Equal(3, result.Data.Bathrooms);
    }
}
