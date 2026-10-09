namespace HousingHub.Model.Enums;

/// <summary>
/// Where a tenancy has got to, from the owner choosing someone through to the end
/// of the let.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle values run in order from 1; terminal values sit in their own band from
/// 10, so a new stage can be inserted later without renumbering anything.
/// </para>
/// <para>
/// These values are persisted. <b>Never reuse or renumber one.</b>
/// </para>
/// </remarks>
public enum TenancyStatus
{
    /// <summary>The owner has picked this candidate. Nothing has been agreed yet.</summary>
    CandidateSelected = 1,

    /// <summary>Documents have been requested and are with the tenant. Phase B.</summary>
    DocumentsRequested = 2,

    /// <summary>Every requested document has been accepted by the owner. Phase B.</summary>
    DocumentsAccepted = 3,

    /// <summary>Waiting on the tenant to pay rent and fees. Phase C.</summary>
    AwaitingPayment = 4,

    /// <summary>Paid, keys handed over, the let is running. Phase C.</summary>
    Active = 5,

    /// <summary>The let has come to an end. Phase D.</summary>
    Ended = 6,

    /// <summary>
    /// The owner pulled out before completion.
    /// </summary>
    /// <remarks>
    /// Distinct from the candidate declining, because the two mean opposite things
    /// to the person on the other end and should never be summarised together.
    /// </remarks>
    Withdrawn = 10,

    /// <summary>
    /// The candidate is no longer interested.
    /// </summary>
    /// <remarks>
    /// Exists so a tenancy cannot get stuck. Without it, an owner whose candidate has
    /// moved on waits indefinitely on somebody who will never respond, and the
    /// property stays under offer while they do.
    /// </remarks>
    DeclinedByCandidate = 11,
}
