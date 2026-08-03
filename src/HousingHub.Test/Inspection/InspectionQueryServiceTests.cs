using Mapster;
using HousingHub.Service.AdminService;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Inspection;

public class InspectionQueryServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly Mock<IAdminAuthService> _adminAuthServiceMock;
    private readonly IMapper _mapper;
    private readonly InspectionQueryService _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid InspectionId = Guid.NewGuid();

    public InspectionQueryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _adminAuthServiceMock = new Mock<IAdminAuthService>();
        _adminAuthServiceMock.Setup(a => a.GetAllStaffAsync()).ReturnsAsync(new List<AdminStaffDto>());
        var logger = NullLogger<InspectionQueryService>.Instance;

        var config = new TypeAdapterConfig();
        new InspectionMapper().Register(config);
        _mapper = new ObjectMapper(config);

        _sut = new InspectionQueryService(_unitOfWorkMock.Object, _mapper, _adminAuthServiceMock.Object, logger);
    }

    private static Customer CreateCustomer(Guid id, string firstName, string lastName) =>
        new(firstName, lastName, $"{firstName.ToLower()}@test.com", "08012345678", CustomerType.Customer, "hash")
        {
            Id = id
        };

    private static Property CreateProperty(Guid? id = null, Guid? ownerId = null) => new()
    {
        Id = id ?? PropertyId,
        Title = "Test Property",
        OwnerId = ownerId ?? OwnerId,
        Latitude = 6.5,
        Longitude = 3.3
    };

    private static PropertyInspection CreateInspection(
        Guid? id = null, Guid? customerId = null, Guid? propertyId = null,
        InspectionStatus status = InspectionStatus.Pending) =>
        new(customerId ?? CustomerId, propertyId ?? PropertyId,
            DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(10), "Note")
        {
            Id = id ?? InspectionId,
            Status = status
        };

    // ── GetInspectionAsync ───────────────────────────────────────

    [Fact]
    public async Task GetInspectionAsync_WhenFound_ReturnsSuccess()
    {
        var inspection = CreateInspection();
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetByAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(inspection);
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty());

        var result = await _sut.GetInspectionAsync(InspectionId, CustomerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(InspectionId, result.Data!.Id);
        Assert.Equal(OwnerId, result.Data.PropertyOwnerId);
    }

    [Fact]
    public async Task GetInspectionAsync_PopulatesCustomerAndOwnerNames()
    {
        var inspection = CreateInspection();
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetByAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(inspection);
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty());
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(CustomerId))
            .ReturnsAsync(CreateCustomer(CustomerId, "Jane", "Doe"));
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByIdAsync(OwnerId))
            .ReturnsAsync(CreateCustomer(OwnerId, "John", "Smith"));

        var result = await _sut.GetInspectionAsync(InspectionId, CustomerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Jane Doe", result.Data!.CustomerName);
        Assert.Equal("John Smith", result.Data.PropertyOwnerName);
    }

    [Fact]
    public async Task GetInspectionAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetByAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync((PropertyInspection?)null);

        var result = await _sut.GetInspectionAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetInspectionAsync_WhenRequesterIsNeitherCustomerNorOwner_ReturnsFailure()
    {
        var inspection = CreateInspection();
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetByAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(inspection);
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty());

        var result = await _sut.GetInspectionAsync(InspectionId, Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetInspectionAsync_WhenRequesterIsOwner_ReturnsSuccess()
    {
        var inspection = CreateInspection();
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetByAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(inspection);
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty());

        var result = await _sut.GetInspectionAsync(InspectionId, OwnerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetInspectionAsync_PopulatesEarliestUploadedPropertyImage()
    {
        var inspection = CreateInspection();
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetByAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(inspection);
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty());

        var olderFile = new HousingHub.Model.Entities.PropertyFile("https://files/first.jpg", PropertyFileType.Image, 1024)
        {
            PropertyId = PropertyId,
            DateUploaded = DateTime.UtcNow.AddDays(-2)
        };
        var newerFile = new HousingHub.Model.Entities.PropertyFile("https://files/second.jpg", PropertyFileType.Image, 1024)
        {
            PropertyId = PropertyId,
            DateUploaded = DateTime.UtcNow.AddDays(-1)
        };
        _unitOfWorkMock.Setup(u => u.PropertyFileQueries.GetAllAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.PropertyFile> { newerFile, olderFile });

        var result = await _sut.GetInspectionAsync(InspectionId, CustomerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("https://files/first.jpg", result.Data!.PropertyImageUrl);
    }

    // ── GetInspectionsByPropertyAsync ────────────────────────────

    [Fact]
    public async Task GetInspectionsByPropertyAsync_ReturnsPagedResults()
    {
        var inspections = new List<PropertyInspection> { CreateInspection(), CreateInspection() };
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync((inspections.AsEnumerable(), 2));

        var result = await _sut.GetInspectionsByPropertyAsync(PropertyId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
    }

    [Fact]
    public async Task GetInspectionsByPropertyAsync_WithStatusFilter_PassesPredicate()
    {
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync((Enumerable.Empty<PropertyInspection>(), 0));

        var result = await _sut.GetInspectionsByPropertyAsync(PropertyId, 1, 10, InspectionStatus.Pending);

        Assert.True(result.IsSuccessful);
        _unitOfWorkMock.Verify(u => u.PropertyInspectionQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<PropertyInspection, bool>>>()), Times.Once);
    }

    [Fact]
    public async Task GetInspectionsByPropertyAsync_WhenEmpty_ReturnsEmptyResult()
    {
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync((Enumerable.Empty<PropertyInspection>(), 0));

        var result = await _sut.GetInspectionsByPropertyAsync(PropertyId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Data!.TotalCount);
    }

    // ── GetInspectionsByCustomerAsync ────────────────────────────

    [Fact]
    public async Task GetInspectionsByCustomerAsync_ReturnsPagedResults()
    {
        var inspections = new List<PropertyInspection> { CreateInspection() };
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync((inspections.AsEnumerable(), 1));

        var result = await _sut.GetInspectionsByCustomerAsync(CustomerId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetInspectionsByCustomerAsync_WithStatusFilter_PassesPredicate()
    {
        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync((Enumerable.Empty<PropertyInspection>(), 0));

        var result = await _sut.GetInspectionsByCustomerAsync(CustomerId, 1, 10, InspectionStatus.Confirmed);

        Assert.True(result.IsSuccessful);
        _unitOfWorkMock.Verify(u => u.PropertyInspectionQueries.GetPagedAsync(
            1, 10, It.IsAny<Expression<Func<PropertyInspection, bool>>>()), Times.Once);
    }

    // ── GetInspectionsByOwnerAsync ───────────────────────────────

    [Fact]
    public async Task GetInspectionsByOwnerAsync_WhenNoProperties_ReturnsEmptyResult()
    {
        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetAllAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<Property>());

        var result = await _sut.GetInspectionsByOwnerAsync(OwnerId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetInspectionsByOwnerAsync_WithProperties_ReturnsInspections()
    {
        var property = CreateProperty(ownerId: OwnerId);
        var inspection = CreateInspection(propertyId: property.Id);

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetAllAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(new[] { property });

        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetAllAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new[] { inspection });

        var result = await _sut.GetInspectionsByOwnerAsync(OwnerId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Equal(property.Title, result.Data.Items[0].PropertyName);
    }

    [Fact]
    public async Task GetInspectionsByOwnerAsync_PopulatesPropertyImageUrl()
    {
        var property = CreateProperty(ownerId: OwnerId);
        var inspection = CreateInspection(propertyId: property.Id);

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetAllAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(new[] { property });

        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetAllAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new[] { inspection });

        var file = new HousingHub.Model.Entities.PropertyFile("https://files/owner-view.jpg", PropertyFileType.Image, 1024)
        {
            PropertyId = property.Id,
            DateUploaded = DateTime.UtcNow
        };
        _unitOfWorkMock.Setup(u => u.PropertyFileQueries.GetAllAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.PropertyFile> { file });

        var result = await _sut.GetInspectionsByOwnerAsync(OwnerId, 1, 10);

        Assert.True(result.IsSuccessful);
        Assert.Equal("https://files/owner-view.jpg", result.Data!.Items[0].PropertyImageUrl);
    }

    [Fact]
    public async Task GetInspectionsByOwnerAsync_WithStatusFilter_FiltersCorrectly()
    {
        var property = CreateProperty(ownerId: OwnerId);
        var pendingInspection = CreateInspection(propertyId: property.Id, status: InspectionStatus.Pending);

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetAllAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(new[] { property });

        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetAllAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new[] { pendingInspection });

        var result = await _sut.GetInspectionsByOwnerAsync(OwnerId, 1, 10, InspectionStatus.Pending);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetInspectionsByOwnerAsync_PaginatesCorrectly()
    {
        var property = CreateProperty(ownerId: OwnerId);
        var inspections = Enumerable.Range(0, 5)
            .Select(_ => CreateInspection(propertyId: property.Id))
            .ToList();

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetAllAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(new[] { property });

        _unitOfWorkMock.Setup(u => u.PropertyInspectionQueries.GetAllAsync(
            It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(inspections);

        var result = await _sut.GetInspectionsByOwnerAsync(OwnerId, 1, 2);

        Assert.True(result.IsSuccessful);
        Assert.Equal(5, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
    }
}
