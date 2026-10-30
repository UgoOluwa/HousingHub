using HousingHub.Model.Entities;
using HousingHub.Model.Enums;

namespace HousingHub.Test.Inspection;

/// <summary>
/// Whether a viewing actually happened.
/// </summary>
/// <remarks>
/// A booking is not an attendance, and everything downstream rests on this — a
/// completed inspection is what qualifies somebody to be chosen as a tenant. Until
/// this existed nothing ever wrote <see cref="InspectionStatus.Completed"/>, so the
/// candidate list was permanently empty.
/// </remarks>
public class InspectionCompletionTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 18, 0, 0, DateTimeKind.Utc);

    private static PropertyInspection Confirmed(DateTime? slot = null)
    {
        var at = slot ?? Now.AddHours(-8);
        return new PropertyInspection(Guid.NewGuid(), Guid.NewGuid(), at.Date, at.TimeOfDay, null)
        {
            Status = InspectionStatus.Confirmed,
        };
    }

    // ── marking it done ──────────────────────────────────────────

    [Fact]
    public void AConfirmedViewingWhoseDayHasCome_CanBeMarkedDone()
    {
        var inspection = Confirmed();

        Assert.True(inspection.TryComplete("Lovely place.", Now));
        Assert.Equal(InspectionStatus.Completed, inspection.Status);
        Assert.Equal(Now, inspection.CompletedAt);
        Assert.Equal("Lovely place.", inspection.CompletionComment);
    }

    [Fact]
    public void TheCommentIsOptional()
    {
        var inspection = Confirmed();

        Assert.True(inspection.TryComplete(null, Now));
        Assert.Null(inspection.CompletionComment);
    }

    /// <summary>Whitespace is not a comment; it would render as an empty quote.</summary>
    [Fact]
    public void AWhitespaceComment_IsStoredAsNothing()
    {
        var inspection = Confirmed();

        inspection.TryComplete("   ", Now);

        Assert.Null(inspection.CompletionComment);
    }

    [Fact]
    public void ACommentIsTrimmed()
    {
        var inspection = Confirmed();

        inspection.TryComplete("  The gate was locked.  ", Now);

        Assert.Equal("The gate was locked.", inspection.CompletionComment);
    }

    /// <summary>
    /// The abuse this guards: book a slot next month, mark it done today, and become
    /// a candidate for a property nobody has seen.
    /// </summary>
    [Fact]
    public void AViewingBookedForTheFuture_CannotBeMarkedDone()
    {
        var inspection = Confirmed(Now.AddDays(3));

        Assert.False(inspection.TryComplete("Been there, honest.", Now));
        Assert.Equal(InspectionStatus.Confirmed, inspection.Status);
    }

    /// <summary>
    /// Allowed from the scheduled day, not the scheduled minute. Owners let people in
    /// early and the stored time carries no zone, so the hour is not something to be
    /// strict about — the day is.
    /// </summary>
    [Fact]
    public void AViewingEarlierTodayButBeforeItsSlot_CanStillBeMarkedDone()
    {
        var inspection = Confirmed(Now.Date.AddHours(23));

        Assert.True(inspection.TryComplete(null, Now));
    }

    [Theory]
    [InlineData(InspectionStatus.Pending)]
    [InlineData(InspectionStatus.Declined)]
    [InlineData(InspectionStatus.Rescheduled)]
    [InlineData(InspectionStatus.Cancelled)]
    [InlineData(InspectionStatus.Expired)]
    [InlineData(InspectionStatus.Completed)]
    public void AnythingOtherThanConfirmed_CannotBeMarkedDone(InspectionStatus status)
    {
        var inspection = Confirmed();
        inspection.Status = status;

        Assert.False(inspection.TryComplete(null, Now));
        Assert.Equal(status, inspection.Status);
    }

    // ── chasing and closing ──────────────────────────────────────

    [Fact]
    public void AskingIsRecordedOnce()
    {
        var inspection = Confirmed();

        Assert.True(inspection.TryMarkFollowUpSent(Now));
        Assert.Equal(Now, inspection.CompletionFollowUpSentAt);

        // The worker must not chase the same person twice.
        Assert.False(inspection.TryMarkFollowUpSent(Now.AddDays(1)));
        Assert.Equal(Now, inspection.CompletionFollowUpSentAt);
    }

    [Fact]
    public void OnceAsked_ItCanBeClosed()
    {
        var inspection = Confirmed();
        inspection.TryMarkFollowUpSent(Now);

        Assert.True(inspection.TryExpire());
        Assert.Equal(InspectionStatus.Expired, inspection.Status);
    }

    /// <summary>
    /// Closing one we never chased would punish somebody for our own silence — and
    /// the state means "we asked and heard nothing", not "time passed".
    /// </summary>
    [Fact]
    public void AnUnaskedViewing_IsNotClosed()
    {
        var inspection = Confirmed();

        Assert.False(inspection.TryExpire());
        Assert.Equal(InspectionStatus.Confirmed, inspection.Status);
    }

    /// <summary>
    /// The race the worker can actually lose: the renter answers between the query
    /// and the write. Their answer wins.
    /// </summary>
    [Fact]
    public void AViewingTheRenterConfirmedAfterBeingAsked_IsNotClosed()
    {
        var inspection = Confirmed();
        inspection.TryMarkFollowUpSent(Now.AddDays(-7));
        inspection.TryComplete("Yes, I went.", Now);

        Assert.False(inspection.TryExpire());
        Assert.Equal(InspectionStatus.Completed, inspection.Status);
    }

    /// <summary>Terminal. A renter who still wants the property books again.</summary>
    [Fact]
    public void AClosedViewing_CannotBeRevived()
    {
        var inspection = Confirmed();
        inspection.TryMarkFollowUpSent(Now.AddDays(-8));
        inspection.TryExpire();

        Assert.False(inspection.TryComplete("I did go, actually.", Now));
        Assert.Equal(InspectionStatus.Expired, inspection.Status);
    }
}
