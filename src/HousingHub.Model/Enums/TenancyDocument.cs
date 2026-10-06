namespace HousingHub.Model.Enums;

/// <summary>
/// How a requested document gets completed.
/// </summary>
/// <remarks>
/// <para>
/// The mode decides who provides the file and what "done" means, so it is chosen by
/// the owner when they ask for the document rather than inferred later.
/// </para>
/// <para>
/// Persisted. <b>Never reuse or renumber.</b>
/// </para>
/// </remarks>
public enum TenancyDocumentMode
{
    /// <summary>
    /// The tenant supplies a document they already have — an employment letter, a
    /// reference. The owner provides nothing.
    /// </summary>
    Upload = 1,

    /// <summary>
    /// The owner provides the document and the tenant signs it electronically.
    /// </summary>
    /// <remarks>
    /// Only valid where the instrument can be signed electronically at all. Nigeria's
    /// Evidence Act excludes land instruments, so a sale or a lease over three years
    /// has to use <see cref="SignOffline"/> — see docs/tenancy-lifecycle-plan.md.
    /// </remarks>
    SignInApp = 2,

    /// <summary>
    /// The owner provides the document; the tenant downloads it, signs on paper and
    /// uploads the scan.
    /// </summary>
    /// <remarks>
    /// Both a convenience and the legal path for anything that cannot be e-signed.
    /// One mechanism, two jobs.
    /// </remarks>
    SignOffline = 3,
}

/// <summary>
/// Where one requested document has got to.
/// </summary>
/// <remarks>
/// The loop is Requested → Submitted → Accepted, with Rejected returning it to the
/// tenant carrying a reason. Persisted; never reuse or renumber.
/// </remarks>
public enum TenancyDocumentStatus
{
    /// <summary>Asked for. Waiting on the tenant.</summary>
    Requested = 1,

    /// <summary>The tenant has uploaded or signed it. Waiting on the owner.</summary>
    Submitted = 2,

    /// <summary>The owner accepted it. Terminal.</summary>
    Accepted = 3,

    /// <summary>
    /// The owner sent it back with a reason.
    /// </summary>
    /// <remarks>
    /// Not terminal — the tenant corrects it and submits again, which returns the
    /// document to <see cref="Submitted"/>. The reason stays on the record so the
    /// history of what was wrong survives the correction.
    /// </remarks>
    Rejected = 4,
}
