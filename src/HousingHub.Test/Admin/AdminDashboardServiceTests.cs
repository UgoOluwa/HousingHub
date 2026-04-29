using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.AdminService;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.PropertyService.Interfaces;
using Moq;

namespace HousingHub.Test.Admin;

public class AdminDashboardServiceTests
{
    private readonly Mock<ICustomerQueryService> _customersMock = new();
    private readonly Mock<IPropertyQueryService> _propertiesMock = new();
    private readonly Mock<IInspectionQueryService> _inspectionsMock = new();
    private readonly AdminDashboardService _sut;

    public AdminDashboardServiceTests()
    {
        _sut = new AdminDashboardService(_customersMock.Object, _propertiesMock.Object, _inspectionsMock.Object);
    }

    private static CustomerDto MakeCustomer(CustomerType type, DateTime? dateModified = null) =>
        new(Guid.NewGuid(), DateTime.UtcNow, dateModified ?? default, "First", "Last",
            $"{Guid.NewGuid():N}@test.com", "08000000000", (int)type, null);

    private static PropertyDto MakeProperty(bool isPublished = false,
        PropertyAvailability availability = PropertyAvailability.Available) =>
        new(Guid.NewGuid(), "PROP-1", DateTime.UtcNow, DateTime.UtcNow, "Title", "Desc",
            PropertyType.Apartment, 100_000m, availability, PropertyLeaseType.Rent,
            PropertyFeature.None, null, null, null, Guid.NewGuid(), Guid.NewGuid(),
            null, null, 0, isPublished, isPublished ? DateTime.UtcNow : null);

    private static AdminInspectionListDto MakeInspectionDto(
        InspectionStatus status = InspectionStatus.Pending,
        DateTime? scheduledDate = null) =>
        new(Guid.NewGuid(), "INS-1", "Property", "Address", Guid.NewGuid(), Guid.NewGuid(),
            "Customer", scheduledDate ?? DateTime.UtcNow.AddDays(3), TimeSpan.FromHours(10),
            DateTime.UtcNow, status, null, null);

    private void Setup(
        IEnumerable<CustomerDto>? customers = null,
        IEnumerable<PropertyDto>? properties = null,
        IEnumerable<AdminInspectionListDto>? inspections = null)
    {
        _customersMock
            .Setup(s => s.GetAllCustomersAsync())
            .ReturnsAsync(new BaseResponse<List<CustomerDto>>(
                (customers ?? []).ToList(), true, string.Empty, "OK"));

        _propertiesMock
            .Setup(s => s.GetAllPropertiesAsync())
            .ReturnsAsync(new BaseResponse<List<PropertyDto>>(
                (properties ?? []).ToList(), true, string.Empty, "OK"));

        var items = (inspections ?? []).ToList();
        _inspectionsMock
            .Setup(s => s.GetAllInspectionsPaginatedAsync(It.IsAny<AdminInspectionFilterDto>()))
            .ReturnsAsync(new BaseResponse<PaginatedResult<AdminInspectionListDto>>(
                new PaginatedResult<AdminInspectionListDto>(items, items.Count, 1, int.MaxValue),
                true, string.Empty, "OK"));
    }

    // ── Customer type counts ──────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_CountsRegularCustomersOnly()
    {
        Setup(customers: new[]
        {
            MakeCustomer(CustomerType.Customer),
            MakeCustomer(CustomerType.Customer),
            MakeCustomer(CustomerType.HouseOwner),
            MakeCustomer(CustomerType.Agent),
        });

        var stats = await _sut.GetStatsAsync();

        Assert.Equal(2, stats.TotalCustomers);
    }

    [Fact]
    public async Task GetStats_CountsOwnersAndAgentsSeparately()
    {
        Setup(customers: new[]
        {
            MakeCustomer(CustomerType.HouseOwner),
            MakeCustomer(CustomerType.HouseOwner),
            MakeCustomer(CustomerType.Agent),
            MakeCustomer(CustomerType.Customer),
        });

        var stats = await _sut.GetStatsAsync();

        Assert.Equal(2, stats.TotalOwners);
        Assert.Equal(1, stats.TotalAgents);
    }

    [Fact]
    public async Task GetStats_AdminCustomers_NotCountedInAnyCategory()
    {
        Setup(customers: new[]
        {
            MakeCustomer(CustomerType.Admin),
            MakeCustomer(CustomerType.Customer),
        });

        var stats = await _sut.GetStatsAsync();

        Assert.Equal(1, stats.TotalCustomers);
        Assert.Equal(0, stats.TotalOwners);
        Assert.Equal(0, stats.TotalAgents);
    }

    // ── PendingKyc ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_PendingKyc_CountsNonAdminsWithNonDefaultDateModified()
    {
        Setup(customers: new[]
        {
            MakeCustomer(CustomerType.Customer, dateModified: DateTime.UtcNow),   // KYC submitted
            MakeCustomer(CustomerType.Customer, dateModified: default),            // never touched
            MakeCustomer(CustomerType.HouseOwner, dateModified: DateTime.UtcNow), // KYC submitted
            MakeCustomer(CustomerType.Admin, dateModified: DateTime.UtcNow),      // admin — excluded
        });

        var stats = await _sut.GetStatsAsync();

        Assert.Equal(2, stats.PendingKyc);
    }

    // ── Property counts ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_ActiveListings_OnlyPublishedAndAvailable()
    {
        Setup(properties: new[]
        {
            MakeProperty(isPublished: true, availability: PropertyAvailability.Available),
            MakeProperty(isPublished: true, availability: PropertyAvailability.Rented),  // published but not available
            MakeProperty(isPublished: false, availability: PropertyAvailability.Available), // not published
        });

        var stats = await _sut.GetStatsAsync();

        Assert.Equal(1, stats.ActiveListings);
        Assert.Equal(3, stats.TotalProperties);
    }

    // ── Inspection counts ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_PendingInspections_OnlyPendingStatus()
    {
        Setup(inspections: new[]
        {
            MakeInspectionDto(InspectionStatus.Pending),
            MakeInspectionDto(InspectionStatus.Pending),
            MakeInspectionDto(InspectionStatus.Confirmed),
            MakeInspectionDto(InspectionStatus.Declined),
        });

        var stats = await _sut.GetStatsAsync();

        Assert.Equal(2, stats.PendingInspections);
    }

    [Fact]
    public async Task GetStats_TodaysInspections_OnlyToday()
    {
        var today = DateTime.UtcNow.Date;
        Setup(inspections: new[]
        {
            MakeInspectionDto(scheduledDate: today),
            MakeInspectionDto(scheduledDate: today),
            MakeInspectionDto(scheduledDate: today.AddDays(1)),
        });

        var stats = await _sut.GetStatsAsync();

        Assert.Equal(2, stats.TodaysInspections);
    }

    // ── Empty data ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_NoData_ReturnsAllZeros()
    {
        Setup();

        var stats = await _sut.GetStatsAsync();

        Assert.Equal(0, stats.TotalCustomers);
        Assert.Equal(0, stats.TotalOwners);
        Assert.Equal(0, stats.TotalAgents);
        Assert.Equal(0, stats.PendingKyc);
        Assert.Equal(0, stats.ActiveListings);
        Assert.Equal(0, stats.TotalProperties);
        Assert.Equal(0, stats.PendingInspections);
        Assert.Equal(0, stats.TodaysInspections);
    }
}
