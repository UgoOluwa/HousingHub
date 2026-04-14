using Mapster;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.CustomerAddressService;
using HousingHub.Service.CustomerService;
using HousingHub.Service.Dtos.CustomerAddress;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.CustomerAddress;

public class CustomerAddressServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly IMapper _mapper;
    private readonly CustomerAddressCommandService _commandSut;
    private readonly CustomerAddressQueryService _querySut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid AddressId = Guid.NewGuid();

    public CustomerAddressServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var commandLogger = NullLogger<CustomerAddressCommandService>.Instance;
        var queryLogger = NullLogger<CustomerAddressQueryService>.Instance;

        var config = new TypeAdapterConfig();
        new CustomerAddressMapper().Register(config);
        _mapper = new ObjectMapper(config);

        _unitOfWorkMock.Setup(u => u.CustomerAddressCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.CustomerAddress>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        _commandSut = new CustomerAddressCommandService(commandLogger, _unitOfWorkMock.Object, _mapper);
        _querySut = new CustomerAddressQueryService(_unitOfWorkMock.Object, _mapper, queryLogger);
    }

    private static HousingHub.Model.Entities.CustomerAddress CreateAddress(Guid? id = null, Guid? customerId = null) =>
        new("123 Main St", "Lagos", "Lagos", "Nigeria", "100001", customerId ?? CustomerId)
        {
            Id = id ?? AddressId
        };

    // ── CreateCustomerAddress ────────────────────────────────────

    [Fact]
    public async Task CreateCustomerAddress_WhenNoneExists_ReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.AnyAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.CustomerAddress, bool>>>()))
            .ReturnsAsync(false);

        var dto = new CreateCustomerAddressDto("123 Main St", "Lagos", "Lagos", "Nigeria", "100001", CustomerId);
        var result = await _commandSut.CreateCustomerAddress(dto);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("Lagos", result.Data.City);
    }

    [Fact]
    public async Task CreateCustomerAddress_WhenAlreadyExists_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.AnyAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.CustomerAddress, bool>>>()))
            .ReturnsAsync(true);

        var dto = new CreateCustomerAddressDto("123 Main St", "Lagos", "Lagos", "Nigeria", "100001", CustomerId);
        var result = await _commandSut.CreateCustomerAddress(dto);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task CreateCustomerAddress_WhenInsertFails_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.AnyAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.CustomerAddress, bool>>>()))
            .ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.CustomerAddressCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.CustomerAddress>())).ReturnsAsync(false);

        var dto = new CreateCustomerAddressDto("123 Main St", "Lagos", "Lagos", "Nigeria", "100001", CustomerId);
        var result = await _commandSut.CreateCustomerAddress(dto);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task CreateCustomerAddress_CallsSaveAsync()
    {
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.AnyAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.CustomerAddress, bool>>>()))
            .ReturnsAsync(false);

        var dto = new CreateCustomerAddressDto("123 Main St", "Lagos", "Lagos", "Nigeria", "100001", CustomerId);
        await _commandSut.CreateCustomerAddress(dto);

        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    // ── GetAddressAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetAddressAsync_WhenFound_ReturnsSuccess()
    {
        var address = CreateAddress();
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.CustomerAddress, bool>>>()))
            .ReturnsAsync(address);

        var result = await _querySut.GetAddressAsync(AddressId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(AddressId, result.Data!.Id);
    }

    [Fact]
    public async Task GetAddressAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.CustomerAddress, bool>>>()))
            .ReturnsAsync((HousingHub.Model.Entities.CustomerAddress?)null);

        var result = await _querySut.GetAddressAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }

    // ── GetCustomerAddressByCustomerIdAsync ──────────────────────

    [Fact]
    public async Task GetCustomerAddressByCustomerIdAsync_WhenFound_ReturnsSuccess()
    {
        var address = CreateAddress(customerId: CustomerId);
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.CustomerAddress, bool>>>()))
            .ReturnsAsync(address);

        var result = await _querySut.GetCustomerAddressByCustomerIdAsync(CustomerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(CustomerId, result.Data!.CustomerId);
    }

    [Fact]
    public async Task GetCustomerAddressByCustomerIdAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.CustomerAddress, bool>>>()))
            .ReturnsAsync((HousingHub.Model.Entities.CustomerAddress?)null);

        var result = await _querySut.GetCustomerAddressByCustomerIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }

    // ── GetAllCustomerAddressesAsync ─────────────────────────────

    [Fact]
    public async Task GetAllCustomerAddressesAsync_WhenAddressesExist_ReturnsSuccess()
    {
        var addresses = new List<HousingHub.Model.Entities.CustomerAddress> { CreateAddress(), CreateAddress() };
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.GetAllAsync())
            .ReturnsAsync(addresses);

        var result = await _querySut.GetAllCustomerAddressesAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task GetAllCustomerAddressesAsync_WhenEmpty_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerAddressQueries.GetAllAsync())
            .ReturnsAsync(Enumerable.Empty<HousingHub.Model.Entities.CustomerAddress>());

        var result = await _querySut.GetAllCustomerAddressesAsync();

        Assert.False(result.IsSuccessful);
    }
}
