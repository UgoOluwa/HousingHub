using Mapster;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Dtos.PropertyFile;
using HousingHub.Service.PropertyFileService;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace HousingHub.Test.PropertyFile;

public class PropertyFileServiceTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWorkMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly IMapper _mapper;
    private readonly PropertyFileCommandService _commandSut;
    private readonly PropertyFileQueryService _querySut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid FileId = Guid.NewGuid();
    private const string TestFileUrl = "https://s3.example.com/properties/test.jpg";

    public PropertyFileServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _fileStorageMock = new Mock<IFileStorageService>();
        var commandLogger = NullLogger<PropertyFileCommandService>.Instance;
        var queryLogger = NullLogger<PropertyFileQueryService>.Instance;

        var config = new TypeAdapterConfig();
        new PropertyFileMapper().Register(config);
        _mapper = new ObjectMapper(config);

        _unitOfWorkMock.Setup(u => u.PropertyFileCommands.InsertAsync(It.IsAny<HousingHub.Model.Entities.PropertyFile>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.PropertyFileCommands.InsertRangeAsync(It.IsAny<IEnumerable<HousingHub.Model.Entities.PropertyFile>>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.PropertyFileCommands.DeleteAsync(It.IsAny<HousingHub.Model.Entities.PropertyFile>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
        _fileStorageMock.Setup(f => f.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync(TestFileUrl);
        _fileStorageMock.Setup(f => f.DeleteFileAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _commandSut = new PropertyFileCommandService(commandLogger, _unitOfWorkMock.Object, _mapper, _fileStorageMock.Object);
        _querySut = new PropertyFileQueryService(_unitOfWorkMock.Object, _mapper, queryLogger);
    }

    private static HousingHub.Model.Entities.PropertyFile CreateFile(Guid? id = null, Guid? propertyId = null) =>
        new("https://s3.example.com/img.jpg", PropertyFileType.Image, 1024)
        {
            Id = id ?? FileId,
            PropertyId = propertyId ?? PropertyId
        };

    private static Customer CreateOwner(Guid? id = null) =>
        new("Owner", "User", "owner@test.com", "08012345678", CustomerType.HouseOwner, "hash")
        {
            Id = id ?? OwnerId
        };

    private static Property CreateProperty(Guid? id = null, Guid? ownerId = null) => new()
    {
        Id = id ?? PropertyId,
        Title = "Test Property",
        OwnerId = ownerId ?? OwnerId
    };

    private static Mock<IFormFile> CreateFormFile(string fileName = "test.jpg", long length = 1024)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(length);
        return fileMock;
    }

    // ── UploadPropertyFiles ──────────────────────────────────────

    [Fact]
    public async Task UploadPropertyFiles_WithValidOwnerAndFile_ReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(CreateOwner());

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty());

        var files = new List<IFormFile> { CreateFormFile().Object };
        var result = await _commandSut.UploadPropertyFiles(PropertyId, OwnerId, files);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data!.Count);
    }

    [Fact]
    public async Task UploadPropertyFiles_WhenCustomerNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync((Customer?)null);

        var result = await _commandSut.UploadPropertyFiles(PropertyId, OwnerId, new List<IFormFile>());

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task UploadPropertyFiles_WhenNotOwner_ReturnsFailure()
    {
        var customer = new Customer("Customer", "User", "c@test.com", "08012345678", CustomerType.Customer, "hash") { Id = Guid.NewGuid() };
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customer);

        var result = await _commandSut.UploadPropertyFiles(PropertyId, customer.Id, new List<IFormFile>());

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.UnauthorizedPropertyAction, result.Message);
    }

    [Fact]
    public async Task UploadPropertyFiles_WhenPropertyNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(CreateOwner());

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync((Property?)null);

        var result = await _commandSut.UploadPropertyFiles(PropertyId, OwnerId, new List<IFormFile>());

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task UploadPropertyFiles_WhenPropertyNotOwnedByUser_ReturnsFailure()
    {
        var otherId = Guid.NewGuid();
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(CreateOwner(otherId));

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty(ownerId: OwnerId)); // different owner

        var result = await _commandSut.UploadPropertyFiles(PropertyId, otherId, new List<IFormFile>());

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PropertyNotOwnedByUser, result.Message);
    }

    [Fact]
    public async Task UploadPropertyFiles_WithFileTooLarge_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(CreateOwner());

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty());

        var oversizedFile = CreateFormFile("big.jpg", 11 * 1024 * 1024); // 11 MB
        var result = await _commandSut.UploadPropertyFiles(PropertyId, OwnerId, new List<IFormFile> { oversizedFile.Object });

        Assert.False(result.IsSuccessful);
        Assert.Contains("10MB", result.Message);
    }

    [Fact]
    public async Task UploadPropertyFiles_WithInvalidExtension_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(CreateOwner());

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty());

        var badFile = CreateFormFile("document.pdf");
        var result = await _commandSut.UploadPropertyFiles(PropertyId, OwnerId, new List<IFormFile> { badFile.Object });

        Assert.False(result.IsSuccessful);
        Assert.Contains(ResponseMessages.InvalidFileType, result.Message);
    }

    // ── DeletePropertyFile ───────────────────────────────────────

    [Fact]
    public async Task DeletePropertyFile_WithValidOwner_ReturnsSuccess()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(CreateOwner());

        _unitOfWorkMock.Setup(u => u.PropertyFileQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync(CreateFile());

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty());

        var result = await _commandSut.DeletePropertyFile(FileId, OwnerId);

        Assert.True(result.IsSuccessful);
        _fileStorageMock.Verify(f => f.DeleteFileAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeletePropertyFile_WhenFileNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(CreateOwner());

        _unitOfWorkMock.Setup(u => u.PropertyFileQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync((HousingHub.Model.Entities.PropertyFile?)null);

        var result = await _commandSut.DeletePropertyFile(FileId, OwnerId);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task DeletePropertyFile_WhenNotPropertyOwner_ReturnsFailure()
    {
        var otherId = Guid.NewGuid();
        _unitOfWorkMock.Setup(u => u.CustomerQueries.GetByAsync(
            It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(CreateOwner(otherId));

        _unitOfWorkMock.Setup(u => u.PropertyFileQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync(CreateFile());

        _unitOfWorkMock.Setup(u => u.PropertyQueries.GetByAsync(
            It.IsAny<Expression<Func<Property, bool>>>()))
            .ReturnsAsync(CreateProperty(ownerId: OwnerId)); // different owner

        var result = await _commandSut.DeletePropertyFile(FileId, otherId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ResponseMessages.PropertyNotOwnedByUser, result.Message);
    }

    // ── GetPropertyFileAsync ─────────────────────────────────────

    [Fact]
    public async Task GetPropertyFileAsync_WhenFound_ReturnsSuccess()
    {
        var file = CreateFile();
        _unitOfWorkMock.Setup(u => u.PropertyFileQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync(file);

        var result = await _querySut.GetPropertyFileAsync(FileId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(FileId, result.Data!.Id);
    }

    [Fact]
    public async Task GetPropertyFileAsync_WhenNotFound_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.PropertyFileQueries.GetByAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync((HousingHub.Model.Entities.PropertyFile?)null);

        var result = await _querySut.GetPropertyFileAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
    }

    // ── GetAllPropertyFilesAsync ─────────────────────────────────

    [Fact]
    public async Task GetAllPropertyFilesAsync_WhenFilesExist_ReturnsSuccess()
    {
        var files = new List<HousingHub.Model.Entities.PropertyFile> { CreateFile(), CreateFile() };
        _unitOfWorkMock.Setup(u => u.PropertyFileQueries.GetAllAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync(files);

        var result = await _querySut.GetAllPropertyFilesAsync(PropertyId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task GetAllPropertyFilesAsync_WhenNoFiles_ReturnsFailure()
    {
        _unitOfWorkMock.Setup(u => u.PropertyFileQueries.GetAllAsync(
            It.IsAny<Expression<Func<HousingHub.Model.Entities.PropertyFile, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<HousingHub.Model.Entities.PropertyFile>());

        var result = await _querySut.GetAllPropertyFilesAsync(PropertyId);

        Assert.False(result.IsSuccessful);
    }
}
