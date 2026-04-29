using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.InspectionService;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Admin;

public class AdminInspectionQueryServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly InspectionQueryService _sut;

    public AdminInspectionQueryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var config = new TypeAdapterConfig();
        new InspectionMapper().Register(config);
        var mapper = new ObjectMapper(config);
        _sut = new InspectionQueryService(_unitOfWorkMock.Object, mapper, NullLogger<InspectionQueryService>.Instance);
    }

    private static PropertyInspection MakeInspection(
        InspectionStatus status = InspectionStatus.Pending,
        DateTime? scheduledDate = null,
        Guid? customerId = null,
        Guid? propertyId = null) =>
        new(customerId ?? Guid.NewGuid(), propertyId ?? Guid.NewGuid(),
            scheduledDate ?? DateTime.UtcNow.AddDays(3), TimeSpan.FromHours(10), "Note")
        {
            Id = Guid.NewGuid(),
            Status = status,
            DateCreated = DateTime.UtcNow
        };

    private void SetupInspections(IEnumerable<PropertyInspection> inspections)
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetAllAsync())
            .ReturnsAsync(inspections);
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetAllAsync(It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(inspections);
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(new List<Property>());
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(new List<Customer>());
        _unitOfWorkMock
            .Setup(u => u.PropertyAddressQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.PropertyAddress>());
    }

    // ── GetAllInspectionsPaginatedAsync ───────────────────────────────────────

    [Fact]
    public async Task GetAllInspectionsPaginated_NoFilter_ReturnsAll()
    {
        var inspections = new[] { MakeInspection(), MakeInspection(), MakeInspection() };
        SetupInspections(inspections);

        var result = await _sut.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(1, 20));

        Assert.True(result.IsSuccessful);
        Assert.Equal(3, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetAllInspectionsPaginated_StatusFilter_ReturnsMatchingOnly()
    {
        var inspections = new[]
        {
            MakeInspection(InspectionStatus.Pending),
            MakeInspection(InspectionStatus.Confirmed),
            MakeInspection(InspectionStatus.Declined),
        };
        SetupInspections(inspections);

        var result = await _sut.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(Status: InspectionStatus.Pending));

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.All(result.Data.Items, i => Assert.Equal(InspectionStatus.Pending, i.Status));
    }

    [Fact]
    public async Task GetAllInspectionsPaginated_DateFilter_ReturnsOnlyThatDate()
    {
        var targetDate = DateTime.UtcNow.AddDays(5).Date;
        var inspections = new[]
        {
            MakeInspection(scheduledDate: targetDate),
            MakeInspection(scheduledDate: DateTime.UtcNow.AddDays(10)),
        };
        SetupInspections(inspections);

        var result = await _sut.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(Date: targetDate));

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetAllInspectionsPaginated_PropertyIdFilter_ReturnsMatchingOnly()
    {
        var propertyId = Guid.NewGuid();
        var inspections = new[]
        {
            MakeInspection(propertyId: propertyId),
            MakeInspection(),
        };
        SetupInspections(inspections);

        var result = await _sut.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(PropertyId: propertyId));

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Equal(propertyId, result.Data.Items[0].PropertyId);
    }

    [Fact]
    public async Task GetAllInspectionsPaginated_Pagination_RespectsPageSize()
    {
        var inspections = Enumerable.Range(0, 8).Select(_ => MakeInspection()).ToArray();
        SetupInspections(inspections);

        var result = await _sut.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(PageNumber: 1, PageSize: 5));

        Assert.True(result.IsSuccessful);
        Assert.Equal(5, result.Data!.Items.Count);
        Assert.Equal(8, result.Data.TotalCount);
    }

    // ── GetTodaysInspectionsPaginatedAsync ────────────────────────────────────

    [Fact]
    public async Task GetTodaysInspections_ReturnsOnlyTodaysInspections()
    {
        var today = DateTime.UtcNow.Date;
        var todayInspection = MakeInspection(scheduledDate: today);

        // The service passes a predicate to the repository; mock returns only what the repo would return
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetAllAsync(It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new[] { todayInspection });
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(new List<Property>());
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(new List<Customer>());
        _unitOfWorkMock
            .Setup(u => u.PropertyAddressQueries.GetAllAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync(new List<HousingHub.Model.Entities.PropertyAddress>());

        var result = await _sut.GetTodaysInspectionsPaginatedAsync(1, 20);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetTodaysInspections_OrderedByTime()
    {
        var today = DateTime.UtcNow.Date;
        var inspections = new[]
        {
            new PropertyInspection(Guid.NewGuid(), Guid.NewGuid(), today, TimeSpan.FromHours(14), null)
                { Id = Guid.NewGuid(), DateCreated = DateTime.UtcNow },
            new PropertyInspection(Guid.NewGuid(), Guid.NewGuid(), today, TimeSpan.FromHours(9), null)
                { Id = Guid.NewGuid(), DateCreated = DateTime.UtcNow },
        };
        SetupInspections(inspections);

        var result = await _sut.GetTodaysInspectionsPaginatedAsync(1, 20);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TimeSpan.FromHours(9), result.Data!.Items[0].ScheduledTime);
        Assert.Equal(TimeSpan.FromHours(14), result.Data.Items[1].ScheduledTime);
    }

    // ── GetRecentActivityAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetRecentActivity_MergesAllEventTypes()
    {
        var now = DateTime.UtcNow;
        var customer = new Customer("Bob", "Jones", "bob@test.com", "08000000001", CustomerType.Customer, "hash")
        {
            Id = Guid.NewGuid(),
            DateCreated = now.AddDays(-1),
            KycSubmittedAt = now.AddHours(-5)
        };

        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(new[] { customer });
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetAllAsync(It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new[] { MakeInspection() });
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(new List<Property>());

        var result = await _sut.GetRecentActivityAsync(50);

        Assert.True(result.IsSuccessful);
        var types = result.Data!.Select(a => a.Type).ToHashSet();
        Assert.Contains("CustomerJoined", types);
        Assert.Contains("KycSubmitted", types);
        Assert.Contains("InspectionScheduled", types);
    }

    [Fact]
    public async Task GetRecentActivity_RespectedCountLimit()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(new List<Customer>());
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetAllAsync(It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(Enumerable.Range(0, 30).Select(_ => MakeInspection()).ToList());
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(new List<Property>());

        var result = await _sut.GetRecentActivityAsync(count: 5);

        Assert.True(result.IsSuccessful);
        Assert.Equal(5, result.Data!.Count);
    }

    [Fact]
    public async Task GetRecentActivity_SortedDescendingByDate()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(new List<Customer>());
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetAllAsync(It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new[]
            {
                new PropertyInspection(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(2), TimeSpan.Zero, null)
                    { Id = Guid.NewGuid(), DateCreated = DateTime.UtcNow.AddDays(-5) },
                new PropertyInspection(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(3), TimeSpan.Zero, null)
                    { Id = Guid.NewGuid(), DateCreated = DateTime.UtcNow.AddDays(-1) },
            });
        _unitOfWorkMock
            .Setup(u => u.PropertyQueries.GetAllAsync(It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(new List<Property>());

        var result = await _sut.GetRecentActivityAsync();

        Assert.True(result.IsSuccessful);
        var dates = result.Data!.Select(a => a.OccurredAt).ToList();
        Assert.True(dates[0] >= dates[1]);
    }

    // ── Exception handling ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllInspectionsPaginated_RepositoryThrows_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetAllAsync())
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.GetAllInspectionsPaginatedAsync(new AdminInspectionFilterDto());

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task GetTodaysInspections_RepositoryThrows_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetAllAsync(It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.GetTodaysInspectionsPaginatedAsync(1, 20);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task GetRecentActivity_RepositoryThrows_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.GetRecentActivityAsync();

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task GetAllInspectionsPaginated_CustomerIdFilter_ReturnsMatchingOnly()
    {
        var customerId = Guid.NewGuid();
        var inspections = new[]
        {
            MakeInspection(customerId: customerId),
            MakeInspection(),
        };
        SetupInspections(inspections);

        var result = await _sut.GetAllInspectionsPaginatedAsync(
            new AdminInspectionFilterDto(CustomerId: customerId));

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
    }
}
