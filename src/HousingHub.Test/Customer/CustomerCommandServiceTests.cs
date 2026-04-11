using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.CustomerService;
using HousingHub.Service.Dtos.Customer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Customers;

public class CustomerCommandServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenProvider> _tokenProviderMock;
    private readonly IMapper _mapper;
    private readonly CustomerCommandService _sut;

    private const string TestPasswordHash = "hashed_password";

    public CustomerCommandServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenProviderMock = new Mock<ITokenProvider>();
        var logger = NullLogger<CustomerCommandService>.Instance;

        var config = new MapperConfiguration(cfg => cfg.AddProfile<CustomerMapper>(), NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _unitOfWorkMock.Setup(u => u.CustomerCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Customer>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.CustomerCommands.UpdateAsync(It.IsAny<HousingHub.Model.Entities.Customer>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CustomerCommands.DeleteAsync(It.IsAny<HousingHub.Model.Entities.Customer>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        _passwordHasherMock.Setup(p => p.Hash(It.IsAny<string>())).Returns(TestPasswordHash);
        _passwordHasherMock.Setup(p => p.Verify(It.IsAny<string>(), TestPasswordHash)).Returns(true);
        _tokenProviderMock.Setup(t => t.Create(It.IsAny<HousingHub.Model.Entities.Customer>())).Returns("jwt_token");

        _sut = new CustomerCommandService(logger, _unitOfWorkMock.Object, _mapper, _passwordHasherMock.Object, _tokenProviderMock.Object);
    }

    // ── CreateCustomer ───────────────────────────────────────────

    [Fact]
    public async Task CreateCustomer_WithValidData_ReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(false);

        var dto = new CreateCustomerDto("John", "Doe", "john@test.com", "08012345678", CustomerType.Customer, null);
        var result = await _sut.CreateCustomer(dto);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("John", result.Data.FirstName);
    }

    [Fact]
    public async Task CreateCustomer_WithExistingCustomer_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(true);

        var dto = new CreateCustomerDto("John", "Doe", "existing@test.com", "08012345678", CustomerType.Customer, null);
        var result = await _sut.CreateCustomer(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.CustomerAlreadyExists, result.Message);
    }

    [Fact]
    public async Task CreateCustomer_WhenInsertFails_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.CustomerCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Customer>())).ReturnsAsync(false);

        var dto = new CreateCustomerDto("John", "Doe", "john@test.com", "08012345678", CustomerType.Customer, null);
        var result = await _sut.CreateCustomer(dto);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task CreateCustomer_CallsSaveAsync()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(false);

        var dto = new CreateCustomerDto("John", "Doe", "john@test.com", "08012345678", CustomerType.Customer, null);
        await _sut.CreateCustomer(dto);

        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    // ── RegisterCustomer ─────────────────────────────────────────

    [Fact]
    public async Task RegisterCustomer_WithValidData_ReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(false);

        var dto = new RegisterCustomerDto("Jane", "Doe", "jane@test.com", "08087654321", "Password123!", CustomerType.Customer);
        var result = await _sut.RegisterCustomer(dto);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("Jane", result.Data.FirstName);
    }

    [Fact]
    public async Task RegisterCustomer_HashesPassword()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(false);

        var dto = new RegisterCustomerDto("Jane", "Doe", "jane@test.com", "08087654321", "Password123!", CustomerType.Customer);
        await _sut.RegisterCustomer(dto);

        _passwordHasherMock.Verify(p => p.Hash("Password123!"), Times.Once);
    }

    [Fact]
    public async Task RegisterCustomer_WithExistingCustomer_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(true);

        var dto = new RegisterCustomerDto("Jane", "Doe", "existing@test.com", "08087654321", "Password123!", CustomerType.Customer);
        var result = await _sut.RegisterCustomer(dto);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.CustomerAlreadyExists, result.Message);
    }

    [Fact]
    public async Task RegisterCustomer_WhenInsertFails_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.AnyAsync(It.IsAny<Expression<Func<HousingHub.Model.Entities.Customer, bool>>>())).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.CustomerCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.Customer>())).ReturnsAsync(false);

        var dto = new RegisterCustomerDto("Jane", "Doe", "jane@test.com", "08087654321", "Password123!", CustomerType.Customer);
        var result = await _sut.RegisterCustomer(dto);

        Assert.False(result.IsSuccessful);
    }
}
