using System.Linq.Expressions;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.PropertyFileService;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PropertyEntity = HousingHub.Model.Entities.Property;
using PropertyFileEntity = HousingHub.Model.Entities.PropertyFile;

namespace HousingHub.Test.Authorization;

/// <summary>
/// Who can add photos and video to a listing, and who can remove them.
/// </summary>
/// <remarks>
/// A listing's images are the listing, commercially speaking. Uploading to someone
/// else's property is defacement and deleting from it is sabotage — both are cheap
/// to attempt (the ids are in the public API response) and neither leaves the
/// attacker's own account touched. Worth its own coverage rather than being folded
/// into the general property tests.
/// </remarks>
public class PropertyFileAuthorizationTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWork;
    private readonly Mock<IFileStorageService> _fileStorage;
    private readonly PropertyFileCommandService _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid FileId = Guid.NewGuid();

    public PropertyFileAuthorizationTests()
    {
        _unitOfWork = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _fileStorage = new Mock<IFileStorageService>();

        var config = new TypeAdapterConfig();
        new PropertyMapper().Register(config);

        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(u => u.PropertyFileCommands.InsertRangeAsync(It.IsAny<IEnumerable<PropertyFileEntity>>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(u => u.PropertyFileCommands.DeleteAsync(It.IsAny<PropertyFileEntity>()))
            .Returns(Task.CompletedTask);

        _sut = new PropertyFileCommandService(
            NullLogger<PropertyFileCommandService>.Instance,
            _unitOfWork.Object,
            new ObjectMapper(config),
            _fileStorage.Object);
    }

    private void GivenCaller(Guid id, CustomerType type = CustomerType.HouseOwner)
    {
        var customer = new Customer("Ada", "Obi", "ada@test.com", "08012345678", type, "hash") { Id = id };

        _unitOfWork
            .Setup(u => u.CustomerQueries.GetByAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customer);
        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(id)).ReturnsAsync(customer);
    }

    private void GivenPropertyOwnedBy(Guid ownerId)
    {
        var property = new PropertyEntity("Flat", "Desc", PropertyType.Apartment, 250000m,
            PropertyAvailability.Available, PropertyLeaseType.Rent)
        {
            Id = PropertyId,
            OwnerId = ownerId,
        };

        _unitOfWork
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<PropertyEntity, bool>>>()))
            .ReturnsAsync(property);
        _unitOfWork.Setup(u => u.PropertyQueries.GetByIdAsync(PropertyId)).ReturnsAsync(property);
    }

    private void GivenExistingFileOnThatProperty()
    {
        var file = new PropertyFileEntity
        {
            Id = FileId,
            PropertyId = PropertyId,
            FileUrl = "https://example.invalid/photo.jpg",
            Type = PropertyFileType.Image,
        };

        _unitOfWork
            .Setup(u => u.PropertyFileQueries.GetByAsync(It.IsAny<Expression<Func<PropertyFileEntity, bool>>>()))
            .ReturnsAsync(file);
        _unitOfWork.Setup(u => u.PropertyFileQueries.GetByIdAsync(FileId)).ReturnsAsync(file);
    }

    private static IFormFile AnImage()
    {
        // A real JPEG magic number, because UploadedFileValidator checks the bytes
        // rather than trusting the extension or the declared content type.
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];

        return new FormFile(new MemoryStream(jpeg), 0, jpeg.Length, "File", "photo.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg",
        };
    }

    // ── Upload ───────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ToSomeoneElsesProperty_IsRefusedAndNothingIsStored()
    {
        GivenCaller(StrangerId);
        GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.UploadPropertyFiles(PropertyId, StrangerId, [AnImage()]);

        Assert.False(result.IsSuccessful);

        // Both halves matter. Not writing a database row is the visible half; not
        // pushing bytes to S3 is the half that would otherwise let an unauthorised
        // caller use the bucket as free storage and run up the bill.
        _unitOfWork.Verify(
            u => u.PropertyFileCommands.InsertRangeAsync(It.IsAny<IEnumerable<PropertyFileEntity>>()),
            Times.Never);
        _fileStorage.Verify(
            f => f.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Upload_ByAnAccountTypeThatCannotManageProperties_IsRefused()
    {
        GivenCaller(OwnerId, CustomerType.Customer);
        GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.UploadPropertyFiles(PropertyId, OwnerId, [AnImage()]);

        Assert.False(result.IsSuccessful);
        _fileStorage.Verify(
            f => f.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Upload_ToAPropertyThatDoesNotExist_IsRefused()
    {
        GivenCaller(OwnerId);
        _unitOfWork
            .Setup(u => u.PropertyQueries.GetByAsync(It.IsAny<Expression<Func<PropertyEntity, bool>>>()))
            .ReturnsAsync((PropertyEntity?)null);
        _unitOfWork.Setup(u => u.PropertyQueries.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((PropertyEntity?)null);

        var result = await _sut.UploadPropertyFiles(Guid.NewGuid(), OwnerId, [AnImage()]);

        Assert.False(result.IsSuccessful);
        _fileStorage.Verify(
            f => f.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ── Delete ───────────────────────────────────────────────────

    [Fact]
    public async Task Delete_AFileOnSomeoneElsesProperty_IsRefused()
    {
        // The file id comes back in the public listing response, so this is not an
        // attack that requires guessing anything.
        GivenCaller(StrangerId);
        GivenPropertyOwnedBy(OwnerId);
        GivenExistingFileOnThatProperty();

        var result = await _sut.DeletePropertyFile(FileId, StrangerId);

        Assert.False(result.IsSuccessful);
        _unitOfWork.Verify(u => u.PropertyFileCommands.DeleteAsync(It.IsAny<PropertyFileEntity>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ByTheOwner_Succeeds()
    {
        GivenCaller(OwnerId);
        GivenPropertyOwnedBy(OwnerId);
        GivenExistingFileOnThatProperty();

        var result = await _sut.DeletePropertyFile(FileId, OwnerId);

        Assert.True(result.IsSuccessful);
        _unitOfWork.Verify(u => u.PropertyFileCommands.DeleteAsync(It.IsAny<PropertyFileEntity>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ByAnAccountTypeThatCannotManageProperties_IsRefused()
    {
        GivenCaller(OwnerId, CustomerType.Customer);
        GivenPropertyOwnedBy(OwnerId);
        GivenExistingFileOnThatProperty();

        var result = await _sut.DeletePropertyFile(FileId, OwnerId);

        Assert.False(result.IsSuccessful);
        _unitOfWork.Verify(u => u.PropertyFileCommands.DeleteAsync(It.IsAny<PropertyFileEntity>()), Times.Never);
    }
}
