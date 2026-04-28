using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.CustomerService;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.Admin;

public class AdminCustomerCommandServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly CustomerCommandService _sut;

    public AdminCustomerCommandServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var config = new TypeAdapterConfig();
        new CustomerMapper().Register(config);
        var mapper = new ObjectMapper(config);
        var passwordHasher = new Mock<IPasswordHasher>();
        var tokenProvider = new Mock<ITokenProvider>();
        _sut = new CustomerCommandService(
            NullLogger<CustomerCommandService>.Instance,
            _unitOfWorkMock.Object,
            mapper,
            passwordHasher.Object,
            tokenProvider.Object);
    }

    private static Customer MakeCustomer(bool isActive = true) =>
        new("Tom", "Brown", "tom@test.com", "08000000002", CustomerType.Customer, "hash")
        {
            Id = Guid.NewGuid(),
            IsActive = isActive,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };

    // ── SuspendCustomer ───────────────────────────────────────────────────────

    [Fact]
    public async Task SuspendCustomer_ExistingActive_SetsIsActiveFalse()
    {
        var customer = MakeCustomer(true);
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customer);
        _unitOfWorkMock.Setup(u => u.CustomerCommands.UpdateAsync(It.IsAny<Customer>())).Returns(Task.CompletedTask);

        var result = await _sut.SuspendCustomer(customer.Id);

        Assert.True(result.IsSuccessful);
        Assert.False(customer.IsActive);
    }

    [Fact]
    public async Task SuspendCustomer_NotFound_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync((Customer?)null);

        var result = await _sut.SuspendCustomer(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task SuspendCustomer_RepositoryThrows_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.SuspendCustomer(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }

    // ── ReactivateCustomer ────────────────────────────────────────────────────

    [Fact]
    public async Task ReactivateCustomer_ExistingSuspended_SetsIsActiveTrue()
    {
        var customer = MakeCustomer(false);
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customer);
        _unitOfWorkMock.Setup(u => u.CustomerCommands.UpdateAsync(It.IsAny<Customer>())).Returns(Task.CompletedTask);

        var result = await _sut.ReactivateCustomer(customer.Id);

        Assert.True(result.IsSuccessful);
        Assert.True(customer.IsActive);
    }

    [Fact]
    public async Task ReactivateCustomer_NotFound_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync((Customer?)null);

        var result = await _sut.ReactivateCustomer(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task ReactivateCustomer_RepositoryThrows_ReturnsFailure()
    {
        _unitOfWorkMock
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _sut.ReactivateCustomer(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }
}
