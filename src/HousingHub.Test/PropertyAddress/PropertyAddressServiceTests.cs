using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.Dtos.PropertyAddress;
using HousingHub.Service.PropertyAddressService;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.PropertyAddress;

public class PropertyAddressServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly IMapper _mapper;
    private readonly PropertyAddressCommandService _commandSut;
    private readonly PropertyAddressQueryService _querySut;

    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid AddressId = Guid.NewGuid();

    public PropertyAddressServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        var commandLogger = NullLogger<PropertyAddressCommandService>.Instance;
        var queryLogger = NullLogger<PropertyAddressQueryService>.Instance;

        var config = new MapperConfiguration(cfg => cfg.AddProfile<PropertyAddressMapper>(), NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _unitOfWorkMock.Setup(u => u.PropertyAddressCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.PropertyAddress>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);

        _commandSut = new PropertyAddressCommandService(commandLogger, _unitOfWorkMock.Object, _mapper);
        _querySut = new PropertyAddressQueryService(_unitOfWorkMock.Object, _mapper, queryLogger);
    }

    private static HousingHub.Model.Entities.PropertyAddress CreateAddress(Guid? id = null, Guid? propertyId = null)
    {
        var addr = new HousingHub.Model.Entities.PropertyAddress("Lekki Phase 1", "Lagos", "Lagos", "Nigeria", "101001")
        {
            Id = id ?? AddressId,
            PropertyId = propertyId ?? PropertyId
        };
        return addr;
    }

    // ── CreatePropertyAddress ────────────────────────────────────

    [Fact]
    public async Task CreatePropertyAddress_WhenNoneExists_ReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.AnyAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync(false);

        var dto = new CreatePropertyAddressDto("Lekki Phase 1", "Lagos", "Lagos", "Nigeria", "101001", PropertyId);
        var result = await _commandSut.CreatePropertyAddress(dto);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Equal("Lagos", result.Data.City);
    }

    [Fact]
    public async Task CreatePropertyAddress_WhenAlreadyExists_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.AnyAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync(true);

        var dto = new CreatePropertyAddressDto("Lekki Phase 1", "Lagos", "Lagos", "Nigeria", "101001", PropertyId);
        var result = await _commandSut.CreatePropertyAddress(dto);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task CreatePropertyAddress_WhenInsertFails_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.AnyAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.PropertyAddressCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.PropertyAddress>())).ReturnsAsync(false);

        var dto = new CreatePropertyAddressDto("Lekki Phase 1", "Lagos", "Lagos", "Nigeria", "101001", PropertyId);
        var result = await _commandSut.CreatePropertyAddress(dto);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task CreatePropertyAddress_CallsSaveAsync()
    {
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.AnyAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync(false);

        var dto = new CreatePropertyAddressDto("Lekki Phase 1", "Lagos", "Lagos", "Nigeria", "101001", PropertyId);
        await _commandSut.CreatePropertyAddress(dto);

        _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
    }

    // ── GetPropertyAddressAsync ──────────────────────────────────

    [Fact]
    public async Task GetPropertyAddressAsync_WhenFound_ReturnsSuccess()
    {
        var address = CreateAddress();
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync(address);

        var result = await _querySut.GetPropertyAddressAsync(AddressId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(AddressId, result.Data!.Id);
    }

    [Fact]
    public async Task GetPropertyAddressAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync((HousingHub.Model.Entities.PropertyAddress?)null);

        var result = await _querySut.GetPropertyAddressAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }

    // ── GetPropertyAddressByPropertyIdAsync ──────────────────────

    [Fact]
    public async Task GetPropertyAddressByPropertyIdAsync_WhenFound_ReturnsSuccess()
    {
        var address = CreateAddress(propertyId: PropertyId);
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync(address);

        var result = await _querySut.GetPropertyAddressByPropertyIdAsync(PropertyId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(PropertyId, result.Data!.PropertyId);
    }

    [Fact]
    public async Task GetPropertyAddressByPropertyIdAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.PropertyAddressQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyAddress, bool>>>()))
            .ReturnsAsync((HousingHub.Model.Entities.PropertyAddress?)null);

        var result = await _querySut.GetPropertyAddressByPropertyIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }
}
