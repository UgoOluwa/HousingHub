namespace HousingHub.Service.Commons.Documents;

public interface ISignedDocumentBuilder
{
    /// <summary>
    /// Produces one PDF: the document, footer-stamped on every page, followed by a
    /// certificate of signature.
    /// </summary>
    /// <param name="source">The bytes the signature was taken against.</param>
    /// <param name="sourceExtension">
    /// Lower-case, with the dot. Decides how the source is carried into the output —
    /// PDF pages are copied, images are placed on a page of their own.
    /// </param>
    /// <returns>
    /// Null when the source cannot be carried across: an unsupported type, or a PDF
    /// that will not open. A null is not a failed signature — the signature stands
    /// on its own record, and the caller is expected to carry on without the file.
    /// </returns>
    byte[]? Build(byte[] source, string sourceExtension, SignatureCertificate certificate);
}
