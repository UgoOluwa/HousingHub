using HousingHub.Model.Entities;
using HousingHub.Model.Enums;

namespace HousingHub.Test.Tenancies;

/// <summary>
/// The loop between an owner asking for a document and accepting it.
/// </summary>
public class TenancyDocumentTests
{
    private const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private static TenancyDocument Agreement(TenancyDocumentMode mode = TenancyDocumentMode.SignInApp) =>
        new(Guid.NewGuid(), "Tenancy agreement", mode, isAgreement: true, null,
            sourceFileKey: "private/tenancies/x/source/a.pdf", sourceFileHash: Hash);

    private static TenancyDocument UploadRequest() =>
        new(Guid.NewGuid(), "Employment letter", TenancyDocumentMode.Upload, isAgreement: false, null,
            sourceFileKey: null, sourceFileHash: null);

    [Fact]
    public void ANewDocument_IsWaitingOnTheTenant()
    {
        var document = UploadRequest();

        Assert.Equal(TenancyDocumentStatus.Requested, document.Status);
        Assert.True(document.IsWithTenant);
        Assert.False(document.IsSettled);
    }

    // ── submitting a file ────────────────────────────────────────

    [Fact]
    public void SubmittingAFile_SendsItToTheOwner()
    {
        var document = UploadRequest();

        Assert.True(document.TrySubmitFile("private/tenancies/x/submitted/letter.pdf"));
        Assert.Equal(TenancyDocumentStatus.Submitted, document.Status);
        Assert.NotNull(document.SubmittedAt);
        Assert.False(document.IsWithTenant);
    }

    /// <summary>A sign-in-app document is signed, not uploaded.</summary>
    [Fact]
    public void SubmittingAFile_AgainstASignInAppDocument_IsRefused()
    {
        var document = Agreement();

        Assert.False(document.TrySubmitFile("whatever.pdf"));
        Assert.Equal(TenancyDocumentStatus.Requested, document.Status);
    }

    /// <summary>A document the owner has accepted must not change underneath them.</summary>
    [Fact]
    public void SubmittingAFile_AfterAcceptance_IsRefused()
    {
        var document = UploadRequest();
        document.TrySubmitFile("first.pdf");
        document.TryAccept();

        Assert.False(document.TrySubmitFile("second.pdf"));
        Assert.Equal("first.pdf", document.SubmittedFileKey);
    }

    // ── signing ──────────────────────────────────────────────────

    [Fact]
    public void Signing_RecordsTheAuditTrail()
    {
        var document = Agreement();

        Assert.True(document.TrySign(Hash, "203.0.113.9", "Mozilla/5.0"));

        Assert.Equal(TenancyDocumentStatus.Submitted, document.Status);
        Assert.Equal(Hash, document.SignedDocumentHash);
        Assert.Equal("203.0.113.9", document.SignerIpAddress);
        Assert.Equal("Mozilla/5.0", document.SignerUserAgent);
        Assert.NotNull(document.SignedAt);
    }

    [Fact]
    public void Signing_AnUploadRequest_IsRefused()
    {
        var document = UploadRequest();

        Assert.False(document.TrySign(Hash, null, null));
        Assert.Equal(TenancyDocumentStatus.Requested, document.Status);
    }

    /// <summary>
    /// Nothing to sign means nothing to attest to. A signature over an absent
    /// document is a record of agreement to an unknown thing.
    /// </summary>
    [Fact]
    public void Signing_WithNoSourceDocument_IsRefused()
    {
        var document = new TenancyDocument(
            Guid.NewGuid(), "Agreement", TenancyDocumentMode.SignInApp, true, null, null, null);

        Assert.False(document.TrySign(Hash, null, null));
    }

    [Fact]
    public void Signing_WithNoHash_IsRefused()
    {
        var document = Agreement();

        Assert.False(document.TrySign("", null, null));
        Assert.False(document.TrySign("   ", null, null));
        Assert.Equal(TenancyDocumentStatus.Requested, document.Status);
    }

    // ── reviewing ────────────────────────────────────────────────

    [Fact]
    public void Accepting_SettlesTheDocument()
    {
        var document = UploadRequest();
        document.TrySubmitFile("letter.pdf");

        Assert.True(document.TryAccept());
        Assert.Equal(TenancyDocumentStatus.Accepted, document.Status);
        Assert.True(document.IsSettled);
        Assert.False(document.IsWithTenant);
        Assert.NotNull(document.ReviewedAt);
    }

    [Fact]
    public void Accepting_SomethingNotYetSubmitted_IsRefused()
    {
        var document = UploadRequest();

        Assert.False(document.TryAccept());
        Assert.Equal(TenancyDocumentStatus.Requested, document.Status);
    }

    [Fact]
    public void Rejecting_SendsItBackWithTheReason()
    {
        var document = UploadRequest();
        document.TrySubmitFile("letter.pdf");

        Assert.True(document.TryReject("  The scan is cut off at the bottom  "));
        Assert.Equal(TenancyDocumentStatus.Rejected, document.Status);
        Assert.Equal("The scan is cut off at the bottom", document.RejectionReason);
        Assert.True(document.IsWithTenant);
    }

    /// <summary>
    /// The reason is the only thing telling the tenant what to change. Without it the
    /// loop is guesswork for the person least able to guess.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejecting_WithoutAReason_IsRefused(string reason)
    {
        var document = UploadRequest();
        document.TrySubmitFile("letter.pdf");

        Assert.False(document.TryReject(reason));
        Assert.Equal(TenancyDocumentStatus.Submitted, document.Status);
    }

    /// <summary>
    /// A signature attested to a submission the owner has refused. Leaving it would
    /// leave a signed-but-rejected document that reads as executed.
    /// </summary>
    [Fact]
    public void Rejecting_ClearsTheSignature()
    {
        var document = Agreement();
        document.TrySign(Hash, "203.0.113.9", "Mozilla/5.0");

        document.TryReject("Wrong version of the agreement");

        Assert.Null(document.SignedAt);
        Assert.Null(document.SignedDocumentHash);
        Assert.Null(document.SignerIpAddress);
        Assert.Null(document.SignerUserAgent);

        // The source and its hash survive — the document itself has not changed.
        Assert.Equal(Hash, document.SourceFileHash);
    }

    // ── the correction loop ──────────────────────────────────────

    [Fact]
    public void Resubmitting_AfterARejection_ClearsTheReason()
    {
        var document = UploadRequest();
        document.TrySubmitFile("first.pdf");
        document.TryReject("Cut off at the bottom");

        Assert.True(document.TrySubmitFile("second.pdf"));
        Assert.Equal(TenancyDocumentStatus.Submitted, document.Status);
        Assert.Null(document.RejectionReason);
        Assert.Equal("second.pdf", document.SubmittedFileKey);
    }

    [Fact]
    public void ResigningAfterARejection_IsAllowed()
    {
        var document = Agreement();
        document.TrySign(Hash, "203.0.113.9", null);
        document.TryReject("Please sign the second page too");

        Assert.True(document.TrySign(Hash, "203.0.113.10", null));
        Assert.Equal(TenancyDocumentStatus.Submitted, document.Status);
        Assert.Equal("203.0.113.10", document.SignerIpAddress);
        Assert.Null(document.RejectionReason);
    }

    /// <summary>The loop runs until the owner is satisfied, however many rounds that takes.</summary>
    [Fact]
    public void TheLoop_RunsUntilAccepted()
    {
        var document = UploadRequest();

        for (int round = 1; round <= 3; round++)
        {
            Assert.True(document.TrySubmitFile($"attempt{round}.pdf"));
            Assert.True(document.TryReject($"Still wrong, round {round}"));
        }

        Assert.True(document.TrySubmitFile("final.pdf"));
        Assert.True(document.TryAccept());
        Assert.True(document.IsSettled);
    }

    // ── the stamped PDF ──────────────────────────────────────────

    [Fact]
    public void AStampedPdf_AttachesToASignatureAwaitingReview()
    {
        var document = Agreement();
        document.TrySign(Hash, "102.89.3.11", "iPhone");

        Assert.True(document.AttachSignedPdf("private/tenancies/x/signed/a.pdf"));
        Assert.Equal("private/tenancies/x/signed/a.pdf", document.SignedPdfKey);
    }

    /// <summary>
    /// Assembly happens after the signature is committed, so by the time it finishes
    /// the owner may have decided. Nothing should appear on an unsigned document.
    /// </summary>
    [Fact]
    public void AStampedPdf_WillNotAttachToSomethingUnsigned()
    {
        var document = Agreement();

        Assert.False(document.AttachSignedPdf("a.pdf"));
        Assert.Null(document.SignedPdfKey);
    }

    [Fact]
    public void AStampedPdf_WillNotAttachAfterTheOwnerHasAccepted()
    {
        var document = Agreement();
        document.TrySign(Hash, null, null);
        document.TryAccept();

        Assert.False(document.AttachSignedPdf("a.pdf"));
        Assert.Null(document.SignedPdfKey);
    }

    /// <summary>
    /// It says on its face that the document was signed, so leaving it downloadable
    /// would hand the tenant a file contradicting the status next to it.
    /// </summary>
    [Fact]
    public void RejectingADocument_TakesTheStampedPdfWithTheSignature()
    {
        var document = Agreement();
        document.TrySign(Hash, "102.89.3.11", "iPhone");
        document.AttachSignedPdf("private/tenancies/x/signed/a.pdf");

        Assert.True(document.TryReject("Wrong version"));

        Assert.Null(document.SignedPdfKey);
        Assert.Null(document.SignedAt);
        Assert.Null(document.SignedDocumentHash);
    }

    /// <summary>
    /// Signing again after a rejection must not resurrect the old file: it was built
    /// from the version the owner refused.
    /// </summary>
    [Fact]
    public void SigningAgainAfterARejection_StartsWithNoStampedPdf()
    {
        var document = Agreement();
        document.TrySign(Hash, null, null);
        document.AttachSignedPdf("first.pdf");
        document.TryReject("Wrong version");

        Assert.True(document.TrySign(Hash, null, null));
        Assert.Null(document.SignedPdfKey);
    }
}
