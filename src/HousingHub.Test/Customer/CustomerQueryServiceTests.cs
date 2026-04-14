using Mapster;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.CustomerService;
using HousingHub.Service.Dtos.Customer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Customers;

public class CustomerQueryServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly IMapper _mapper;
    private readonly CustomerQueryService _sut;

    public CustomerQueryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var logger = NullLogger<CustomerQueryService>.Instance;

        var config = new TypeAdapterConfig();
        new CustomerMapper().Register(config);
        _mapper = new ObjectMapper(config);

        _sut = new CustomerQueryService(_unitOfWorkMock.Object, _mapper, logger);
    }

    private static HousingHub.Model.Entities.Customer CreateCustomer(Guid? id = null) =>
        new("John", "Doe", "john@test.com", "08012345678", CustomerType.Customer, "hash")
        {
            Id = id ?? Guid.NewGuid(),
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };

    // ── GetCustomerAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetCustomerAsync_WhenExists_ReturnsSuccess()
    {
        var customerId = Guid.NewGuid();
        var customer = CreateCustomer(customerId);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(customer);

        var result = await _sut.GetCustomerAsync(customerId);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("John", result.Data.FirstName);
    }

    [Fact]
    public async Task GetCustomerAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync((HousingHub.Model.Entities.Customer?)null);

        var result = await _sut.GetCustomerAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Contains("Not Found", result.Message);
    }

    [Fact]
    public async Task GetCustomerAsync_MapsAllFields()
    {
        var customerId = Guid.NewGuid();
        var customer = CreateCustomer(customerId);
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(customer);

        var result = await _sut.GetCustomerAsync(customerId);

        Assert.Equal("John", result.Data!.FirstName);
        Assert.Equal("Doe", result.Data.LastName);
        Assert.Equal("john@test.com", result.Data.Email);
    }

    // ── GetAllCustomersAsync ─────────────────────────────────────

    [Fact]
    public async Task GetAllCustomersAsync_WithCustomers_ReturnsSuccess()
    {
        var customers = new List<HousingHub.Model.Entities.Customer> { CreateCustomer(), CreateCustomer() };
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetAllAsync()).ReturnsAsync(customers);

        var result = await _sut.GetAllCustomersAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task GetAllCustomersAsync_WhenEmpty_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetAllAsync()).ReturnsAsync(new List<HousingHub.Model.Entities.Customer>());

        var result = await _sut.GetAllCustomersAsync();

        Assert.False(result.IsSuccessful);
    }

    // ── GetAllCustomersPaginatedAsync ────────────────────────────

    [Fact]
    public async Task GetAllCustomersPaginatedAsync_ReturnsCorrectPage()
    {
        var customers = new List<HousingHub.Model.Entities.Customer> { CreateCustomer(), CreateCustomer(), CreateCustomer() };
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetPagedAsync(1, 10, null))
            .ReturnsAsync((customers.AsEnumerable(), 3));

        var result = await _sut.GetAllCustomersPaginatedAsync(1, 10);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.TotalCount);
        Assert.Equal(3, result.Data.Items.Count);
    }

    [Fact]
    public async Task GetAllCustomersPaginatedAsync_WhenEmpty_ReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetPagedAsync(1, 10, null))
            .ReturnsAsync((Enumerable.Empty<HousingHub.Model.Entities.Customer>(), 0));

        var result = await _sut.GetAllCustomersPaginatedAsync(1, 10);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetAllCustomersPaginatedAsync_ReturnsCorrectPageInfo()
    {
        var customers = new List<HousingHub.Model.Entities.Customer> { CreateCustomer() };
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetPagedAsync(2, 5, null))
            .ReturnsAsync((customers.AsEnumerable(), 6));

        var result = await _sut.GetAllCustomersPaginatedAsync(2, 5);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.PageNumber);
        Assert.Equal(5, result.Data.PageSize);
        Assert.Equal(6, result.Data.TotalCount);
    }
}
