namespace HousingHub.Service.Commons.Documents;

/// <summary>
/// Everything printed on the certificate page of a signed document.
/// </summary>
/// <remarks>
/// <para>
/// A plain record rather than the entity, because the builder must not be able to
/// reach back for anything the caller did not deliberately put on the page. What
/// appears on a document somebody may hand to a bank is decided at one call site.
/// </para>
/// <para>
/// <see cref="SourceFileHash"/> is the hash of the document as it stood when it was
/// signed. Printing it is what lets anybody holding the file check later that the
/// pages in front of them are the pages that were agreed to.
/// </para>
/// </remarks>
public record SignatureCertificate(
    Guid TenancyId,
    Guid DocumentId,
    string DocumentName,
    string? PropertyTitle,
    string? LandlordName,
    string SignerName,
    DateTime SignedAtUtc,
    string? SignerIpAddress,
    string? SignerUserAgent,
    string SourceFileHash);
