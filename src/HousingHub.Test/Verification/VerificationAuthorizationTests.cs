using System.Linq.Expressions;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Dtos.Verification;
using HousingHub.Service.NotificationService.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PropertyEntity = HousingHub.Model.Entities.Property;
using HousingHub.Service.VerificationService;
using VerificationServiceImpl = HousingHub.Service.VerificationService.VerificationService;

namespace HousingHub.Test.Verification;

/// <summary>
/// Who may touch a verification case, and what leaks if the answer is wrong.
/// </summary>
/// <remarks>
/// This pipeline holds the most sensitive data in the product — certificates of
/// occupancy, deeds, company registrations. An ownership bug here is not a bug
/// report, it is a disclosure of somebody's title documents, and under NDPA that
/// is reportable. So the checks are tested individually rather than trusted to
/// read correctly.
/// </remarks>
public class VerificationAuthorizationTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWork;
    private readonly Mock<IFileStorageService> _fileStorage;
    private readonly VerificationServiceImpl _sut;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();

    public VerificationAuthorizationTests()
    {
        _unitOfWork = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };
        _fileStorage = new Mock<IFileStorageService>();

        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.VerificationCaseCommands.InsertAsync(It.IsAny<VerificationCase>()))
            .ReturnsAsync(true);
        _unitOfWork.Setup(u => u.VerificationCaseCommands.UpdateAsync(It.IsAny<VerificationCase>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.VerificationDocumentCommands.InsertAsync(It.IsAny<VerificationDocument>()))
            .ReturnsAsync(true);
        _unitOfWork.Setup(u => u.VerificationDocumentCommands.DeleteAsync(It.IsAny<VerificationDocument>()))
            .Returns(Task.CompletedTask);

        // No pre-existing drafts unless a test says otherwise.
        _unitOfWork
            .Setup(u => u.VerificationCaseQueries.GetAllAsync(
                It.IsAny<Expression<Func<VerificationCase, bool>>>()))
            .ReturnsAsync(new List<VerificationCase>());
        _unitOfWork
            .Setup(u => u.VerificationDocumentQueries.GetAllAsync(
                It.IsAny<Expression<Func<VerificationDocument, bool>>>()))
            .ReturnsAsync(new List<VerificationDocument>());

        _fileStorage
            .Setup(f => f.UploadPrivateFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("private/verification/x/doc.pdf");

        _sut = new VerificationServiceImpl(
            _unitOfWork.Object,
            _fileStorage.Object,
            new Mock<IEmailService>().Object,
            new Mock<IRealtimeNotifier>().Object,
            // The real no-provider implementation rather than a mock: it reports
            // "not performed" rather than inventing a result, which is exactly the
            // behaviour these tests should run against.
            new DeferToReviewerCacLookupService(
                NullLogger<DeferToReviewerCacLookupService>.Instance),
            NullLogger<VerificationServiceImpl>.Instance);
    }

    private VerificationCase GivenCaseOwnedBy(
        Guid submitterId, VerificationCaseStatus status = VerificationCaseStatus.Draft)
    {
        var verificationCase = new VerificationCase(
            submitterId, VerificationSubjectType.Business, submitterId, VerificationTier.BusinessVerified)
        {
            Id = CaseId,
            Status = status,
        };

        _unitOfWork.Setup(u => u.VerificationCaseQueries.GetByIdAsync(CaseId)).ReturnsAsync(verificationCase);
        return verificationCase;
    }

    private void GivenPropertyOwnedBy(Guid ownerId)
    {
        var property = new PropertyEntity("Flat", "Desc", PropertyType.Apartment, 100m,
            PropertyAvailability.Available, PropertyLeaseType.Rent)
        {
            Id = PropertyId,
            OwnerId = ownerId,
        };

        _unitOfWork.Setup(u => u.PropertyQueries.GetByIdAsync(PropertyId)).ReturnsAsync(property);
    }

    private void GivenDocumentOnCase(Guid caseId)
    {
        var document = new VerificationDocument(
            caseId, VerificationDocumentType.CacCertificate,
            "private/verification/x/doc.pdf", "cac.pdf", "application/pdf", 1024)
        {
            Id = DocumentId,
        };

        _unitOfWork.Setup(u => u.VerificationDocumentQueries.GetByIdAsync(DocumentId)).ReturnsAsync(document);
    }

    private static IFormFile APdf()
    {
        byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37];   // %PDF-1.7

        return new FormFile(new MemoryStream(pdf), 0, pdf.Length, "File", "cac.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
    }

    // ── Opening a case ───────────────────────────────────────────

    [Fact]
    public async Task StartingATitleCase_ForSomeoneElsesProperty_IsRefused()
    {
        // Otherwise anyone could open a verification case against another person's
        // listing and attach their own documents to it — which, if approved, would
        // put a title-verified badge on a listing they have no connection to.
        GivenPropertyOwnedBy(OwnerId);

        var result = await _sut.StartCaseAsync(StrangerId,
            new StartVerificationCaseDto(VerificationSubjectType.Property, PropertyId));

        Assert.False(result.IsSuccessful);
        _unitOfWork.Verify(u => u.VerificationCaseCommands.InsertAsync(It.IsAny<VerificationCase>()), Times.Never);
    }

    [Fact]
    public async Task StartingATitleCase_WithoutNamingAProperty_IsRefused()
    {
        var result = await _sut.StartCaseAsync(OwnerId,
            new StartVerificationCaseDto(VerificationSubjectType.Property, SubjectId: null));

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task StartingABusinessCase_AlwaysSubjectsTheCaller()
    {
        // The body carries a SubjectId, and for a business case it must be ignored.
        // Honouring it would let somebody open a case whose approval lands a badge on
        // a different account.
        VerificationCase? captured = null;
        _unitOfWork.Setup(u => u.VerificationCaseCommands.InsertAsync(It.IsAny<VerificationCase>()))
            .Callback<VerificationCase>(c => captured = c)
            .ReturnsAsync(true);

        var result = await _sut.StartCaseAsync(OwnerId,
            new StartVerificationCaseDto(VerificationSubjectType.Business, SubjectId: StrangerId));

        Assert.True(result.IsSuccessful);
        Assert.NotNull(captured);
        Assert.Equal(OwnerId, captured!.SubjectId);
        Assert.Equal(OwnerId, captured.SubmittedByCustomerId);
    }

    // ── Reading someone else's case ──────────────────────────────

    [Fact]
    public async Task ReadingACaseYouDidNotSubmit_IsRefused()
    {
        GivenCaseOwnedBy(OwnerId);

        var result = await _sut.GetMyCaseAsync(StrangerId, CaseId);

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task ACaseThatIsNotYours_LooksIdenticalToOneThatDoesNotExist()
    {
        // A distinct "forbidden" would confirm the id is real, turning GUID guessing
        // into a way to discover who is applying for what.
        GivenCaseOwnedBy(OwnerId);
        var notMine = await _sut.GetMyCaseAsync(StrangerId, CaseId);

        _unitOfWork.Setup(u => u.VerificationCaseQueries.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((VerificationCase?)null);
        var absent = await _sut.GetMyCaseAsync(StrangerId, Guid.NewGuid());

        Assert.Equal(absent.Message, notMine.Message);
    }

    [Fact]
    public async Task GettingADocumentLinkForSomeoneElsesCase_IsRefused()
    {
        // The most sensitive call in the pipeline: a presigned URL is a bearer
        // credential for a title deed.
        GivenCaseOwnedBy(OwnerId);
        GivenDocumentOnCase(CaseId);

        var result = await _sut.GetMyDocumentUrlAsync(StrangerId, CaseId, DocumentId);

        Assert.False(result.IsSuccessful);
        _fileStorage.Verify(f => f.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task ADocumentBelongingToADifferentCase_CannotBeReadThroughYourOwnCase()
    {
        // Your own case id plus somebody else's document id must not resolve. Without
        // the cross-check, a valid case id becomes a key to any document.
        GivenCaseOwnedBy(OwnerId);
        GivenDocumentOnCase(Guid.NewGuid());

        var result = await _sut.GetMyDocumentUrlAsync(OwnerId, CaseId, DocumentId);

        Assert.False(result.IsSuccessful);
        _fileStorage.Verify(f => f.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    // ── Modifying someone else's case ────────────────────────────

    [Fact]
    public async Task AddingADocumentToSomeoneElsesCase_IsRefusedAndStoresNothing()
    {
        GivenCaseOwnedBy(OwnerId);

        var result = await _sut.AddDocumentAsync(StrangerId, CaseId,
            new AddVerificationDocumentDto(VerificationDocumentType.CacCertificate), APdf());

        Assert.False(result.IsSuccessful);

        // Both halves: no row, and no bytes pushed to S3. The second matters on its
        // own — an unauthorised caller must not be able to use the private bucket as
        // free storage.
        _unitOfWork.Verify(u => u.VerificationDocumentCommands.InsertAsync(It.IsAny<VerificationDocument>()), Times.Never);
        _fileStorage.Verify(
            f => f.UploadPrivateFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RemovingADocumentFromSomeoneElsesCase_IsRefused()
    {
        GivenCaseOwnedBy(OwnerId);
        GivenDocumentOnCase(CaseId);

        var result = await _sut.RemoveDocumentAsync(StrangerId, CaseId, DocumentId);

        Assert.False(result.IsSuccessful);
        _unitOfWork.Verify(u => u.VerificationDocumentCommands.DeleteAsync(It.IsAny<VerificationDocument>()), Times.Never);
    }

    [Fact]
    public async Task SubmittingSomeoneElsesCase_IsRefused()
    {
        GivenCaseOwnedBy(OwnerId);

        var result = await _sut.SubmitCaseAsync(StrangerId, CaseId);

        Assert.False(result.IsSuccessful);
        _unitOfWork.Verify(u => u.VerificationCaseCommands.UpdateAsync(It.IsAny<VerificationCase>()), Times.Never);
    }

    // ── The submitted/draft boundary ─────────────────────────────

    [Fact]
    public async Task AddingADocumentAfterSubmission_IsRefusedEvenByTheOwner()
    {
        // The set of documents is what the reviewer is looking at. Letting it change
        // underneath them means approving a case whose contents differ from the one
        // they read.
        GivenCaseOwnedBy(OwnerId, VerificationCaseStatus.Submitted);

        var result = await _sut.AddDocumentAsync(OwnerId, CaseId,
            new AddVerificationDocumentDto(VerificationDocumentType.CacCertificate), APdf());

        Assert.False(result.IsSuccessful);
        _fileStorage.Verify(
            f => f.UploadPrivateFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RemovingADocumentAfterSubmission_IsRefusedEvenByTheOwner()
    {
        GivenCaseOwnedBy(OwnerId, VerificationCaseStatus.Submitted);
        GivenDocumentOnCase(CaseId);

        var result = await _sut.RemoveDocumentAsync(OwnerId, CaseId, DocumentId);

        Assert.False(result.IsSuccessful);
        _unitOfWork.Verify(u => u.VerificationDocumentCommands.DeleteAsync(It.IsAny<VerificationDocument>()), Times.Never);
    }

    // ── Submission completeness ──────────────────────────────────

    [Fact]
    public async Task SubmittingWithoutTheRequiredDocuments_IsRefusedAndSaysWhatIsMissing()
    {
        GivenCaseOwnedBy(OwnerId);

        var result = await _sut.SubmitCaseAsync(OwnerId, CaseId);

        Assert.False(result.IsSuccessful);

        // The message must name the document. "Submission failed" sends the applicant
        // to support; naming the certificate sends them to the upload button.
        Assert.Contains("CAC", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmittingWithTheRequiredDocument_Succeeds()
    {
        var verificationCase = GivenCaseOwnedBy(OwnerId);

        _unitOfWork
            .Setup(u => u.VerificationDocumentQueries.GetAllAsync(
                It.IsAny<Expression<Func<VerificationDocument, bool>>>()))
            .ReturnsAsync(new List<VerificationDocument>
            {
                new(CaseId, VerificationDocumentType.CacCertificate, "k", "cac.pdf", "application/pdf", 10),
            });

        var result = await _sut.SubmitCaseAsync(OwnerId, CaseId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(VerificationCaseStatus.Submitted, verificationCase.Status);
    }

    // ── Reviewer decisions ───────────────────────────────────────

    [Fact]
    public async Task ApprovingWhileADocumentIsStillPending_IsRefused()
    {
        // A badge resting on evidence nobody looked at is worse than no badge,
        // because a renter will rely on it.
        GivenCaseOwnedBy(OwnerId, VerificationCaseStatus.UnderReview);

        _unitOfWork
            .Setup(u => u.VerificationDocumentQueries.GetAllAsync(
                It.IsAny<Expression<Func<VerificationDocument, bool>>>()))
            .ReturnsAsync(new List<VerificationDocument>
            {
                new(CaseId, VerificationDocumentType.CacCertificate, "k", "cac.pdf", "application/pdf", 10),
            });

        var result = await _sut.DecideCaseAsync(Guid.NewGuid(), CaseId,
            new DecideCaseDto(VerificationCaseStatus.Approved));

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task ApprovingWhileADocumentWasRejected_IsRefused()
    {
        GivenCaseOwnedBy(OwnerId, VerificationCaseStatus.UnderReview);

        var rejected = new VerificationDocument(
            CaseId, VerificationDocumentType.CacCertificate, "k", "cac.pdf", "application/pdf", 10);
        rejected.TryReview(DocumentReviewStatus.Rejected, Guid.NewGuid(), "Illegible.");

        _unitOfWork
            .Setup(u => u.VerificationDocumentQueries.GetAllAsync(
                It.IsAny<Expression<Func<VerificationDocument, bool>>>()))
            .ReturnsAsync(new List<VerificationDocument> { rejected });

        var result = await _sut.DecideCaseAsync(Guid.NewGuid(), CaseId,
            new DecideCaseDto(VerificationCaseStatus.Approved));

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task RejectingADocumentWithoutAReason_IsRefused()
    {
        GivenDocumentOnCase(CaseId);

        var result = await _sut.ReviewDocumentAsync(Guid.NewGuid(), DocumentId,
            new ReviewDocumentDto(Approved: false, RejectionReason: null));

        Assert.False(result.IsSuccessful);
    }
}
