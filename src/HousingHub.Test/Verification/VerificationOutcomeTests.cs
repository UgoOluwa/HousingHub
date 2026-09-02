using System.Linq.Expressions;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Dtos.Verification;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.VerificationService;
using Microsoft.Extensions.Logging.Abstractions;
using HousingHub.Service.PaymentService.Interfaces;
using Moq;
using PropertyEntity = HousingHub.Model.Entities.Property;
using VerificationServiceImpl = HousingHub.Service.VerificationService.VerificationService;

namespace HousingHub.Test.Verification;

/// <summary>
/// What actually changes when a case is approved.
/// </summary>
/// <remarks>
/// Without this step the pipeline is a filing cabinet — cases get decided and
/// nothing anywhere reflects it. These tests exist because that failure is
/// completely silent: the reviewer sees success, the applicant gets their email,
/// and no badge appears.
/// </remarks>
public class VerificationOutcomeTests
{
    private readonly Mock<IUnitOfWOrk> _unitOfWork;
    private readonly VerificationServiceImpl _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PropertyId = Guid.NewGuid();
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    public VerificationOutcomeTests()
    {
        _unitOfWork = new Mock<IUnitOfWOrk> { DefaultValue = DefaultValue.Mock };

        _unitOfWork.Setup(u => u.SaveAsync()).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.VerificationCaseCommands.UpdateAsync(It.IsAny<VerificationCase>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CustomerCommands.UpdateAsync(It.IsAny<Customer>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.PropertyCommands.UpdateAsync(It.IsAny<PropertyEntity>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.NotificationCommands.InsertAsync(It.IsAny<Notification>()))
            .ReturnsAsync(true);

        _sut = new VerificationServiceImpl(
            _unitOfWork.Object,
            new Mock<IFileStorageService>().Object,
            new Mock<IEmailService>().Object,
            new Mock<IRealtimeNotifier>().Object,
            new DeferToReviewerCacLookupService(NullLogger<DeferToReviewerCacLookupService>.Instance),
            PaymentsSatisfied(),
            NullLogger<VerificationServiceImpl>.Instance);
    }

    /// <summary>
    /// A payment service that reports everything as paid for.
    /// </summary>
    /// <remarks>
    /// Which is what the real one does when <c>Payments:Enabled</c> is off — the
    /// state these tests were written against. The payment gate has its own tests;
    /// these are about the verification state machine and should not have to know
    /// that payments exist.
    /// </remarks>
    private static IPaymentService PaymentsSatisfied()
    {
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.IsSubjectPaidForAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        return payments.Object;
    }

    private VerificationCase GivenCaseUnderReview(VerificationSubjectType subjectType, Guid subjectId)
    {
        var verificationCase = new VerificationCase(
            subjectId, subjectType, CustomerId, VerificationRequirements.TierFor(subjectType))
        {
            Id = CaseId,
            Status = VerificationCaseStatus.UnderReview,
        };

        _unitOfWork.Setup(u => u.VerificationCaseQueries.GetByIdAsync(CaseId)).ReturnsAsync(verificationCase);
        return verificationCase;
    }

    private void GivenApprovedDocuments(params VerificationDocument[] documents)
    {
        foreach (var document in documents)
        {
            if (document.Status == DocumentReviewStatus.Pending)
                document.TryReview(DocumentReviewStatus.Approved, AdminId, null);
        }

        _unitOfWork
            .Setup(u => u.VerificationDocumentQueries.GetAllAsync(
                It.IsAny<Expression<Func<VerificationDocument, bool>>>()))
            .ReturnsAsync(documents.ToList());
    }

    private static VerificationDocument Document(
        VerificationDocumentType type, string? number = null, string? nameOnDocument = null,
        DateTime? expiresAt = null) =>
        new(CaseId, type, "key", "file.pdf", "application/pdf", 100)
        {
            DocumentNumber = number,
            NameOnDocument = nameOnDocument,
            ExpiresAt = expiresAt,
        };

    private Customer GivenCustomer()
    {
        var customer = new Customer("Ada", "Obi", "ada@test.com", "08012345678",
            CustomerType.Agent, "hash")
        {
            Id = CustomerId,
        };

        _unitOfWork.Setup(u => u.CustomerQueries.GetByIdAsync(CustomerId)).ReturnsAsync(customer);
        return customer;
    }

    private PropertyEntity GivenProperty()
    {
        var property = new PropertyEntity("Flat", "Desc", PropertyType.Apartment, 100m,
            PropertyAvailability.Available, PropertyLeaseType.Rent)
        {
            Id = PropertyId,
            OwnerId = CustomerId,
        };

        _unitOfWork.Setup(u => u.PropertyQueries.GetByIdAsync(PropertyId)).ReturnsAsync(property);
        return property;
    }

    // ── Business ─────────────────────────────────────────────────

    [Fact]
    public async Task ApprovingABusinessCase_GrantsTheTierAndRecordsTheNumbers()
    {
        var customer = GivenCustomer();
        GivenCaseUnderReview(VerificationSubjectType.Business, CustomerId);
        GivenApprovedDocuments(
            Document(VerificationDocumentType.CacCertificate, number: "RC1234567"),
            Document(VerificationDocumentType.LasreraPermit, number: "LAS-9988"));

        var result = await _sut.DecideCaseAsync(AdminId, CaseId,
            new DecideCaseDto(VerificationCaseStatus.Approved));

        Assert.True(result.IsSuccessful);
        Assert.Equal(VerificationTier.BusinessVerified, customer.BusinessVerificationTier);
        Assert.NotNull(customer.BusinessVerifiedAt);

        // Numbers are lifted from what the reviewer actually saw, not asked for
        // separately, so the profile cannot claim a number nobody checked.
        Assert.Equal("RC1234567", customer.CacNumber);
        Assert.Equal("LAS-9988", customer.LasreraPermitNumber);
        Assert.True(customer.IsBusinessVerified);
    }

    [Fact]
    public async Task ApprovingABusinessCase_CopiesTheEarliestDocumentExpiry()
    {
        // A verification is only as current as its shortest-lived evidence. LASRERA
        // permits are annual, so a Lagos agent's badge has a shelf life.
        var customer = GivenCustomer();
        GivenCaseUnderReview(VerificationSubjectType.Business, CustomerId);

        var soon = DateTime.UtcNow.AddMonths(3);
        GivenApprovedDocuments(
            Document(VerificationDocumentType.CacCertificate, "RC1", expiresAt: DateTime.UtcNow.AddYears(5)),
            Document(VerificationDocumentType.LasreraPermit, "LAS1", expiresAt: soon));

        await _sut.DecideCaseAsync(AdminId, CaseId, new DecideCaseDto(VerificationCaseStatus.Approved));

        Assert.Equal(soon, customer.BusinessVerificationExpiresAt);
    }

    [Fact]
    public void ALapsedBusinessVerification_DoesNotCountAsVerified()
    {
        // The tier stays set until a sweep moves it, so the convenience property has
        // to be the thing callers ask — a badge on a lapsed LASRERA permit is a
        // claim we cannot support.
        var customer = new Customer("Ada", "Obi", "a@t.com", "080", CustomerType.Agent, "h")
        {
            BusinessVerificationTier = VerificationTier.BusinessVerified,
            BusinessVerificationExpiresAt = DateTime.UtcNow.AddDays(-1),
        };

        Assert.False(customer.IsBusinessVerified);
    }

    [Fact]
    public async Task RejectingACase_LeavesTheSubjectUntouched()
    {
        // Somebody whose second submission is rejected must not lose the badge their
        // first one earned.
        var customer = GivenCustomer();
        customer.BusinessVerificationTier = VerificationTier.BusinessVerified;

        GivenCaseUnderReview(VerificationSubjectType.Business, CustomerId);
        GivenApprovedDocuments(Document(VerificationDocumentType.CacCertificate, "RC1"));

        await _sut.DecideCaseAsync(AdminId, CaseId,
            new DecideCaseDto(VerificationCaseStatus.Rejected, "Certificate is illegible."));

        Assert.Equal(VerificationTier.BusinessVerified, customer.BusinessVerificationTier);
    }

    // ── Property title ───────────────────────────────────────────

    [Fact]
    public async Task ApprovingATitleCase_GrantsTheTierAndRecordsTheTitleHolder()
    {
        var property = GivenProperty();
        GivenCustomer();
        GivenCaseUnderReview(VerificationSubjectType.Property, PropertyId);
        GivenApprovedDocuments(
            Document(VerificationDocumentType.CertificateOfOccupancy, nameOnDocument: "Ada Obi"));

        await _sut.DecideCaseAsync(AdminId, CaseId, new DecideCaseDto(VerificationCaseStatus.Approved));

        Assert.Equal(VerificationTier.TitleVerified, property.TitleVerificationTier);
        Assert.Equal("Ada Obi", property.TitleHolderName);
        Assert.True(property.IsTitleVerified);
        Assert.True(property.ListerIsTitleHolder);
    }

    [Fact]
    public async Task ALetterOfAuthority_MarksTheListerAsNotTheTitleHolder()
    {
        // A legitimate and common arrangement — an agent acting for an owner — but
        // the badge must not imply the lister owns the property.
        var property = GivenProperty();
        GivenCustomer();
        GivenCaseUnderReview(VerificationSubjectType.Property, PropertyId);
        GivenApprovedDocuments(
            Document(VerificationDocumentType.CertificateOfOccupancy, nameOnDocument: "Fatima Abubakar"),
            Document(VerificationDocumentType.LetterOfAuthorityToLet, nameOnDocument: "Fatima Abubakar"));

        await _sut.DecideCaseAsync(AdminId, CaseId, new DecideCaseDto(VerificationCaseStatus.Approved));

        Assert.False(property.ListerIsTitleHolder);
        Assert.Equal("Fatima Abubakar", property.TitleHolderName);
    }

    [Fact]
    public async Task ADeedOfAssignment_IsAcceptedInPlaceOfACertificateOfOccupancy()
    {
        var property = GivenProperty();
        GivenCustomer();
        GivenCaseUnderReview(VerificationSubjectType.Property, PropertyId);
        GivenApprovedDocuments(
            Document(VerificationDocumentType.DeedOfAssignment, nameOnDocument: "Ada Obi"));

        await _sut.DecideCaseAsync(AdminId, CaseId, new DecideCaseDto(VerificationCaseStatus.Approved));

        Assert.Equal(VerificationTier.TitleVerified, property.TitleVerificationTier);
        Assert.Equal("Ada Obi", property.TitleHolderName);
    }

    [Fact]
    public async Task TitleVerification_DoesNotTouchTheModerationFlag()
    {
        // Property.IsVerified is "an admin looked at this listing". Title
        // verification is a claim about ownership. Conflating them would let a
        // moderation tick read as a title guarantee.
        var property = GivenProperty();
        GivenCustomer();
        GivenCaseUnderReview(VerificationSubjectType.Property, PropertyId);
        GivenApprovedDocuments(Document(VerificationDocumentType.CertificateOfOccupancy));

        await _sut.DecideCaseAsync(AdminId, CaseId, new DecideCaseDto(VerificationCaseStatus.Approved));

        Assert.False(property.IsVerified);
        Assert.True(property.IsTitleVerified);
    }

    // ── Identity ─────────────────────────────────────────────────

    [Fact]
    public async Task ApprovingAnIdentityCase_SetsTheExistingKycFlag()
    {
        // Reuses IsKycVerified deliberately, so the badge already rendered in both
        // frontends keeps working when the identity flow migrates onto this pipeline.
        var customer = GivenCustomer();
        GivenCaseUnderReview(VerificationSubjectType.Identity, CustomerId);
        GivenApprovedDocuments(Document(VerificationDocumentType.GovernmentIssuedId));

        await _sut.DecideCaseAsync(AdminId, CaseId, new DecideCaseDto(VerificationCaseStatus.Approved));

        Assert.True(customer.IsKycVerified);
    }

    // ── CAC lookup, no provider configured ───────────────────────

    [Fact]
    public async Task WithNoCacProvider_TheReviewerIsToldTheLookupDidNotRun()
    {
        // "We did not look" must never render as "we looked and it was fine". A stub
        // returning a pass would manufacture assurance nobody checked.
        GivenCustomer();
        GivenCaseUnderReview(VerificationSubjectType.Business, CustomerId);
        GivenApprovedDocuments(Document(VerificationDocumentType.CacCertificate, number: "RC1234567"));

        var result = await _sut.GetCaseForReviewAsync(CaseId);

        Assert.True(result.IsSuccessful);
        var context = Assert.Single(result.Data!.ReviewContext!);
        Assert.False(context.CacLookupPerformed);
        Assert.Null(context.CacFound);
    }

    [Fact]
    public async Task TheReviewContextIsWithheldFromTheSubmittersOwnView()
    {
        // Telling an applicant their name did not match tells a would-be
        // impersonator exactly which check to defeat next time.
        GivenCustomer();
        GivenCaseUnderReview(VerificationSubjectType.Business, CustomerId);
        GivenApprovedDocuments(
            Document(VerificationDocumentType.CacCertificate, nameOnDocument: "Someone Else Entirely"));

        var mine = await _sut.GetMyCaseAsync(CustomerId, CaseId);
        var review = await _sut.GetCaseForReviewAsync(CaseId);

        Assert.Null(mine.Data!.ReviewContext);
        Assert.NotNull(review.Data!.ReviewContext);
        Assert.True(review.Data.ReviewContext!.Single().ShouldEscalate);
    }
}
