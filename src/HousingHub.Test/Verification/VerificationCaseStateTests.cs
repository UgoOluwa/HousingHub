using HousingHub.Model.Entities;
using HousingHub.Model.Enums;

namespace HousingHub.Test.Verification;

/// <summary>
/// The case state machine, tested on the entity directly.
/// </summary>
/// <remarks>
/// These transitions are the boundary between "the applicant owns this" and
/// "review owns this". Getting them wrong does not throw — it lets a submitted
/// case be edited underneath the reviewer, or lets a badge be granted on evidence
/// nobody looked at. Neither failure is visible from the outside, which is why
/// they are pinned here rather than left to the service tests.
/// </remarks>
public class VerificationCaseStateTests
{
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly Guid SubmitterId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private static VerificationCase NewCase() => new(
        SubjectId, VerificationSubjectType.Business, SubmitterId, VerificationTier.BusinessVerified);

    private static VerificationCase SubmittedCase()
    {
        var c = NewCase();
        c.TrySubmit();
        return c;
    }

    // ── Cancel ───────────────────────────────────────────────────

    [Fact]
    public void TryCancel_OnADraft_Succeeds()
    {
        var c = NewCase();

        Assert.True(c.TryCancel());
        Assert.Equal(VerificationCaseStatus.Cancelled, c.Status);
    }

    [Fact]
    public void TryCancel_OnADraft_ClosesItToFurtherDocuments()
    {
        var c = NewCase();
        c.TryCancel();

        Assert.False(c.CanAcceptDocuments);
    }

    [Fact]
    public void TryCancel_OnceSubmitted_Refuses()
    {
        // The case belongs to the reviewer from here. Withdrawing mid-review would
        // let someone pull a case back the moment it started going badly, which is
        // exactly the case a reviewer most needs to finish.
        var c = SubmittedCase();

        Assert.False(c.TryCancel());
        Assert.Equal(VerificationCaseStatus.Submitted, c.Status);
    }

    [Fact]
    public void TryCancel_UnderReview_Refuses()
    {
        var c = SubmittedCase();
        c.TryBeginReview(AdminId);

        Assert.False(c.TryCancel());
        Assert.Equal(VerificationCaseStatus.UnderReview, c.Status);
    }

    [Fact]
    public void TryCancel_Twice_RefusesTheSecondTime()
    {
        var c = NewCase();
        c.TryCancel();

        Assert.False(c.TryCancel());
        Assert.Equal(VerificationCaseStatus.Cancelled, c.Status);
    }

    [Fact]
    public void ACancelledCase_CannotBeSubmitted()
    {
        var c = NewCase();
        c.TryCancel();

        Assert.False(c.TrySubmit());
        Assert.Equal(VerificationCaseStatus.Cancelled, c.Status);
    }

    [Fact]
    public void ACancelledCase_IsNotAwaitingReview()
    {
        // It must never surface in the admin queue: ReviewQueueStatus is a sparse
        // index, and a cancelled case appearing there would be work nobody can do.
        var c = NewCase();
        c.TryCancel();

        Assert.False(c.IsAwaitingReview);
    }

    // ── Draft ────────────────────────────────────────────────────

    [Fact]
    public void ANewCase_StartsAsADraftTheSubmitterOwns()
    {
        var c = NewCase();

        Assert.Equal(VerificationCaseStatus.Draft, c.Status);
        Assert.True(c.CanAcceptDocuments);
        Assert.True(c.IsOwnedBySubmitter);
        Assert.False(c.IsAwaitingReview);
    }

    [Fact]
    public void ADraft_IsNotInTheReviewQueue()
    {
        // The queue index is sparse. A draft carrying the marker would put every
        // half-finished upload in front of a reviewer.
        Assert.Null(NewCase().ReviewQueueStatus);
    }

    // ── Submit ───────────────────────────────────────────────────

    [Fact]
    public void Submitting_HandsOwnershipToReview()
    {
        var c = NewCase();

        Assert.True(c.TrySubmit());
        Assert.Equal(VerificationCaseStatus.Submitted, c.Status);
        Assert.NotNull(c.SubmittedAt);

        // The critical pair: documents freeze, and review picks it up.
        Assert.False(c.CanAcceptDocuments);
        Assert.False(c.IsOwnedBySubmitter);
        Assert.True(c.IsAwaitingReview);
        Assert.Equal(VerificationCase.AwaitingReviewMarker, c.ReviewQueueStatus);
    }

    [Fact]
    public void SubmittingTwice_IsRefused()
    {
        var c = SubmittedCase();
        var submittedAt = c.SubmittedAt;

        Assert.False(c.TrySubmit());
        Assert.Equal(submittedAt, c.SubmittedAt);
    }

    // ── Review ───────────────────────────────────────────────────

    [Fact]
    public void BeginningReview_ClaimsTheCaseAndKeepsItInTheQueue()
    {
        var c = SubmittedCase();

        Assert.True(c.TryBeginReview(AdminId));
        Assert.Equal(VerificationCaseStatus.UnderReview, c.Status);
        Assert.Equal(AdminId, c.DecidedByAdminId);

        // Still outstanding work — a claimed case that nobody finishes must not
        // vanish from the queue.
        Assert.Equal(VerificationCase.AwaitingReviewMarker, c.ReviewQueueStatus);
    }

    [Fact]
    public void BeginningReviewOnADraft_IsRefused()
    {
        Assert.False(NewCase().TryBeginReview(AdminId));
    }

    [Fact]
    public void ASecondAdminCannotClaimAClaimedCase()
    {
        var c = SubmittedCase();
        c.TryBeginReview(AdminId);

        Assert.False(c.TryBeginReview(Guid.NewGuid()));
        Assert.Equal(AdminId, c.DecidedByAdminId);
    }

    // ── Decisions ────────────────────────────────────────────────

    [Fact]
    public void Approving_RecordsWhoAndWhenAndLeavesTheQueue()
    {
        var c = SubmittedCase();
        var expiry = DateTime.UtcNow.AddYears(1);

        Assert.True(c.TryDecide(VerificationCaseStatus.Approved, AdminId, note: null, expiresAt: expiry));

        Assert.Equal(VerificationCaseStatus.Approved, c.Status);
        Assert.Equal(AdminId, c.DecidedByAdminId);
        Assert.NotNull(c.DecidedAt);
        Assert.Equal(expiry, c.ExpiresAt);
        Assert.Null(c.ReviewQueueStatus);
    }

    [Fact]
    public void ApprovingNeedsNoNote_ButRejectingDoes()
    {
        // A rejection the applicant cannot act on is just a support ticket.
        Assert.True(NewCaseSubmitted().TryDecide(VerificationCaseStatus.Approved, AdminId, null));
        Assert.False(NewCaseSubmitted().TryDecide(VerificationCaseStatus.Rejected, AdminId, null));
        Assert.False(NewCaseSubmitted().TryDecide(VerificationCaseStatus.Rejected, AdminId, "   "));
        Assert.True(NewCaseSubmitted().TryDecide(VerificationCaseStatus.Rejected, AdminId, "Scan is illegible."));

        static VerificationCase NewCaseSubmitted()
        {
            var c = new VerificationCase(SubjectId, VerificationSubjectType.Business,
                SubmitterId, VerificationTier.BusinessVerified);
            c.TrySubmit();
            return c;
        }
    }

    [Fact]
    public void EscalatingAsNameMismatch_AlsoRequiresAReason()
    {
        // The reviewer's reasoning is the only record of why a mismatch was treated
        // as impersonation rather than a typo, and somebody will have to defend that
        // decision later.
        var c = SubmittedCase();

        Assert.False(c.TryDecide(VerificationCaseStatus.EscalatedNameMismatch, AdminId, null));
        Assert.True(c.TryDecide(VerificationCaseStatus.EscalatedNameMismatch, AdminId,
            "CAC certificate is in the name of a different company."));
        Assert.Equal(VerificationCaseStatus.EscalatedNameMismatch, c.Status);
    }

    [Fact]
    public void ARejectedCase_CarriesNoExpiry()
    {
        // An expiry on a rejection would eventually flip it to Expired, which reads
        // as "was once valid" — it never was.
        var c = SubmittedCase();

        c.TryDecide(VerificationCaseStatus.Rejected, AdminId, "Wrong document.", DateTime.UtcNow.AddYears(1));

        Assert.Null(c.ExpiresAt);
    }

    [Theory]
    [InlineData(VerificationCaseStatus.Draft)]
    [InlineData(VerificationCaseStatus.UnderReview)]
    [InlineData(VerificationCaseStatus.Expired)]
    public void DecidingIntoANonTerminalState_IsRefused(VerificationCaseStatus target)
    {
        // Only Approved, Rejected and EscalatedNameMismatch are decisions. Allowing a
        // case to be pushed back to Draft would hand a submitted case back to the
        // applicant with the reviewer's name already on it.
        Assert.False(SubmittedCase().TryDecide(target, AdminId, "note"));
    }

    [Fact]
    public void DecidingACaseThatWasAlreadyDecided_IsRefused()
    {
        var c = SubmittedCase();
        c.TryDecide(VerificationCaseStatus.Approved, AdminId, null);

        Assert.False(c.TryDecide(VerificationCaseStatus.Rejected, Guid.NewGuid(), "changed my mind"));
        Assert.Equal(VerificationCaseStatus.Approved, c.Status);
    }

    [Fact]
    public void DecidingADraft_IsRefused()
    {
        Assert.False(NewCase().TryDecide(VerificationCaseStatus.Approved, AdminId, null));
    }

    // ── Expiry ───────────────────────────────────────────────────

    [Fact]
    public void AnApprovedCase_ExpiresOnceItsEarliestDocumentHas()
    {
        var c = SubmittedCase();
        c.TryDecide(VerificationCaseStatus.Approved, AdminId, null, DateTime.UtcNow.AddDays(-1));

        Assert.True(c.TryExpire(DateTime.UtcNow));
        Assert.Equal(VerificationCaseStatus.Expired, c.Status);
    }

    [Fact]
    public void AnApprovedCase_DoesNotExpireEarly()
    {
        var c = SubmittedCase();
        c.TryDecide(VerificationCaseStatus.Approved, AdminId, null, DateTime.UtcNow.AddYears(1));

        Assert.False(c.TryExpire(DateTime.UtcNow));
        Assert.Equal(VerificationCaseStatus.Approved, c.Status);
    }

    [Fact]
    public void ACaseWithNoExpiryNeverExpires()
    {
        // A Certificate of Occupancy does not lapse. Defaulting to some horizon would
        // silently un-verify people for no reason.
        var c = SubmittedCase();
        c.TryDecide(VerificationCaseStatus.Approved, AdminId, null, expiresAt: null);

        Assert.False(c.TryExpire(DateTime.UtcNow.AddYears(50)));
        Assert.Equal(VerificationCaseStatus.Approved, c.Status);
    }

    [Fact]
    public void ARejectedCaseCannotExpire()
    {
        var c = SubmittedCase();
        c.TryDecide(VerificationCaseStatus.Rejected, AdminId, "No.");

        Assert.False(c.TryExpire(DateTime.UtcNow.AddYears(50)));
    }

    // ── Derived index key ────────────────────────────────────────

    [Fact]
    public void TheQueueMarkerIsDerived_AndIgnoresWhatTheMapperWritesBack()
    {
        // The setter exists only so the DynamoDB mapper can deserialize. If it stored
        // its argument, Status and ReviewQueueStatus could disagree, and which one
        // won would depend on the order the mapper assigns properties — which is not
        // specified anywhere.
        var c = SubmittedCase();

        c.ReviewQueueStatus = null;
        Assert.Equal(VerificationCase.AwaitingReviewMarker, c.ReviewQueueStatus);

        c.TryDecide(VerificationCaseStatus.Approved, AdminId, null);
        c.ReviewQueueStatus = VerificationCase.AwaitingReviewMarker;
        Assert.Null(c.ReviewQueueStatus);
    }
}
