using HousingHub.Model.Enums;

namespace HousingHub.Service.VerificationService;

/// <summary>
/// What documents each verification tier actually requires.
/// </summary>
/// <remarks>
/// <para>
/// Kept as data in one place rather than as conditionals inside the submit path,
/// because these requirements are the part most likely to change: LASRERA's rules
/// differ from other states', and adding a state means adding a row here rather
/// than editing a method.
/// </para>
/// <para>
/// <b>Deliberately permissive at the edges.</b> A missing optional document does
/// not block submission — it lowers what the reviewer can conclude. Requiring the
/// full set up front would stall every applicant on the one document that is
/// hardest to obtain, and in this market that is usually the tax clearance.
/// </para>
/// </remarks>
public static class VerificationRequirements
{
    /// <summary>
    /// Documents without which a case cannot be submitted at all.
    /// </summary>
    /// <remarks>
    /// One entry each, chosen as the document that carries the registration number
    /// everything else is checked against. Everything else is corroboration.
    /// </remarks>
    public static IReadOnlyList<VerificationDocumentType> RequiredFor(VerificationSubjectType subjectType) =>
        subjectType switch
        {
            // The CAC certificate is the anchor: it carries the RC number, and the
            // directors named on it are what an account holder's verified identity
            // gets compared against. Without it there is nothing to check.
            VerificationSubjectType.Business => [VerificationDocumentType.CacCertificate],

            // Either document establishes a claim to the land. Handled as "one of"
            // below rather than both, because which one exists depends on how the
            // property was acquired — a C of O for a direct state grant, a Deed of
            // Assignment for a transfer.
            VerificationSubjectType.Property => [VerificationDocumentType.CertificateOfOccupancy],

            VerificationSubjectType.Identity => [VerificationDocumentType.GovernmentIssuedId],

            _ => [],
        };

    /// <summary>
    /// Groups where supplying any one member satisfies the requirement.
    /// </summary>
    /// <remarks>
    /// A property owner has a C of O <i>or</i> a Deed of Assignment depending on how
    /// they acquired the land, and demanding both would reject legitimate owners for
    /// holding the wrong kind of correct paperwork.
    /// </remarks>
    public static IReadOnlyList<IReadOnlyList<VerificationDocumentType>> AlternativeGroups(
        VerificationSubjectType subjectType) =>
        subjectType switch
        {
            VerificationSubjectType.Property =>
            [
                [
                    VerificationDocumentType.CertificateOfOccupancy,
                    VerificationDocumentType.DeedOfAssignment,
                ],
            ],
            _ => [],
        };

    /// <summary>Tier a subject reaches when a case of this type is approved.</summary>
    public static VerificationTier TierFor(VerificationSubjectType subjectType) =>
        subjectType switch
        {
            VerificationSubjectType.Business => VerificationTier.BusinessVerified,
            VerificationSubjectType.Property => VerificationTier.TitleVerified,
            VerificationSubjectType.Identity => VerificationTier.IdentityVerified,
            _ => VerificationTier.Unverified,
        };

    /// <summary>
    /// Required document types not yet supplied, accounting for alternatives.
    /// </summary>
    /// <returns>Empty when the case can be submitted.</returns>
    public static List<VerificationDocumentType> MissingFrom(
        VerificationSubjectType subjectType,
        IEnumerable<VerificationDocumentType> supplied)
    {
        var have = supplied.ToHashSet();
        var groups = AlternativeGroups(subjectType);

        var missing = new List<VerificationDocumentType>();

        foreach (var required in RequiredFor(subjectType))
        {
            if (have.Contains(required)) continue;

            // Satisfied if any alternative in the same group was supplied.
            var group = groups.FirstOrDefault(g => g.Contains(required));
            if (group is not null && group.Any(have.Contains)) continue;

            missing.Add(required);
        }

        return missing;
    }
}
