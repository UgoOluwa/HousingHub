using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.CustomerService;
using HousingHub.Service.Dtos.Admin;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Admin;

public class AdminCustomerQueryServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly CustomerQueryService _sut;

    public AdminCustomerQueryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var config = new TypeAdapterConfig();
        new CustomerMapper().Register(config);
        var mapper = new ObjectMapper(config);
        _sut = new CustomerQueryService(_unitOfWorkMock.Object, mapper, NullLogger<CustomerQueryService>.Instance);
    }

    private static Customer MakeCustomer(CustomerType type, bool isKycVerified = false, DateTime? kycAt = null, bool isActive = true) =>
        new("Jane", "Doe", $"jane{Guid.NewGuid():N}@test.com", "08000000000", type, "hash")
        {
            Id = Guid.NewGuid(),
            IsActive = isActive,
            IsKycVerified = isKycVerified,
            KycSubmittedAt = kycAt,
            DateCreated = DateTime.UtcNow
        };

    private void SetupCustomers(IEnumerable<Customer> customers)
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customers);
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync())
            .ReturnsAsync(customers);
        _unitOfWorkMock
            .Setup(u => u.PropertyInspectionQueries.GetAllAsync(It.IsAny<Expression<Func<PropertyInspection, bool>>>()))
            .ReturnsAsync(new List<PropertyInspection>());
    }

    // ── GetCustomersFilteredAsync — type scoping ──────────────────────────────

    [Fact]
    public async Task GetCustomersFiltered_CustomerTypeScope_ReturnsOnlyCustomers()
    {
        var customers = new[]
        {
            MakeCustomer(CustomerType.Customer),
            MakeCustomer(CustomerType.HouseOwner),
            MakeCustomer(CustomerType.Agent),
        };
        SetupCustomers(customers);

        var result = await _sut.GetCustomersFilteredAsync(
            new AdminCustomerFilterDto(), CustomerType.Customer);

        Assert.True(result.IsSuccessful);
        Assert.All(result.Data!.Items, c => Assert.Equal((int)CustomerType.Customer, c.CustomerType));
        Assert.Equal(1, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetCustomersFiltered_AdminExcluded_NeverReturnsAdmins()
    {
        var customers = new[]
        {
            MakeCustomer(CustomerType.Customer),
            MakeCustomer(CustomerType.Admin),
        };
        SetupCustomers(customers);

        var result = await _sut.GetCustomersFilteredAsync(new AdminCustomerFilterDto());

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCustomersFiltered_Search_FiltersByName()
    {
        var customers = new[]
        {
            MakeCustomer(CustomerType.Customer),
            new Customer("Alice", "Smith", "alice@test.com", "08111111111", CustomerType.Customer, "hash")
                { Id = Guid.NewGuid(), DateCreated = DateTime.UtcNow }
        };
        SetupCustomers(customers);

        var result = await _sut.GetCustomersFilteredAsync(
            new AdminCustomerFilterDto(Search: "Alice"));

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Equal("Alice", result.Data.Items[0].FirstName);
    }

    // ── IsVerified filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetCustomersFiltered_IsVerifiedTrue_ReturnsOnlyVerified()
    {
        var customers = new[]
        {
            MakeCustomer(CustomerType.Customer, isKycVerified: true),
            MakeCustomer(CustomerType.Customer, isKycVerified: false),
        };
        SetupCustomers(customers);

        var result = await _sut.GetCustomersFilteredAsync(
            new AdminCustomerFilterDto(IsVerified: true));

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.True(result.Data.Items[0].IsKycVerified);
    }

    // ── IsActive filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetCustomersFiltered_IsActiveFalse_ReturnsOnlySuspended()
    {
        var customers = new[]
        {
            MakeCustomer(CustomerType.Customer, isActive: true),
            MakeCustomer(CustomerType.Customer, isActive: false),
        };
        SetupCustomers(customers);

        var result = await _sut.GetCustomersFilteredAsync(
            new AdminCustomerFilterDto(IsActive: false));

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.False(result.Data.Items[0].IsActive);
    }

    // ── KycPending ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCustomersFiltered_KycSubmittedButNotVerified_MarkedKycPending()
    {
        var customers = new[]
        {
            MakeCustomer(CustomerType.Customer, isKycVerified: false, kycAt: DateTime.UtcNow),
        };
        SetupCustomers(customers);

        var result = await _sut.GetCustomersFilteredAsync(new AdminCustomerFilterDto());

        Assert.True(result.Data!.Items[0].KycPending);
    }

    // ── Pagination ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCustomersFiltered_Pagination_RespectsPageSize()
    {
        var customers = Enumerable.Range(0, 10)
            .Select(_ => MakeCustomer(CustomerType.Customer))
            .ToArray();
        SetupCustomers(customers);

        var result = await _sut.GetCustomersFilteredAsync(
            new AdminCustomerFilterDto(PageNumber: 1, PageSize: 3));

        Assert.True(result.IsSuccessful);
        Assert.Equal(3, result.Data!.Items.Count);
        Assert.Equal(10, result.Data.TotalCount);
    }

    // ── Exception handling ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCustomersFiltered_RepositoryThrows_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetAllAsync())
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.GetCustomersFilteredAsync(new AdminCustomerFilterDto());

        Assert.False(result.IsSuccessful);
    }
}
