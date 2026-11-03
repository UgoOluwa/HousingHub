using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace HousingHub.Service.Commons.Documents;

/// <summary>
/// Turns a signed document into something a person can download and send on.
/// </summary>
/// <remarks>
/// <para>
/// Every page of the original carries a footer naming who signed it and when, and a
/// certificate page is appended with the full audit trail. The footer is the part
/// that matters once a page has been photocopied out of context; the certificate is
/// the part that answers a challenge.
/// </para>
/// <para>
/// <b>No font is embedded and none is resolved from the system.</b> Text is written
/// as raw content-stream operators against Helvetica, one of the fourteen fonts
/// every PDF reader is required to have. That is why this works unchanged on a
/// Lambda image with no fonts installed, which is where it runs — a font resolver
/// would be one more thing to be absent in production and present in development.
/// </para>
/// <para>
/// This is not a digital signature in the PKI sense. It makes no cryptographic
/// claim about the PDF itself; it is a record of an act, bound to the document by a
/// hash. Nigeria's Evidence Act is satisfied by the record, not by the file format.
/// </para>
/// </remarks>
public class SignedDocumentBuilder : ISignedDocumentBuilder
{
    private const double PageWidth = 595;   // A4 at 72dpi
    private const double PageHeight = 842;
    private const double Margin = 56;
    private const double ContentWidth = PageWidth - (Margin * 2);

    /// <summary>Where the page stops and a new one starts.</summary>
    private const double BottomLimit = 90;

    /// <summary>
    /// Helvetica's average advance is a little over half the point size. Used to
    /// wrap rather than to lay out, so it errs narrow — a line that breaks early
    /// looks fine, one that runs off the page loses evidence.
    /// </summary>
    private const double AverageCharWidthRatio = 0.55;

    private static readonly string[] PdfExtensions = [".pdf"];
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg"];

    private readonly ILogger<SignedDocumentBuilder> _logger;

    public SignedDocumentBuilder(ILogger<SignedDocumentBuilder> logger)
    {
        _logger = logger;
    }

    public byte[]? Build(byte[] source, string sourceExtension, SignatureCertificate certificate)
    {
        try
        {
            using var output = new PdfDocument();
            output.Info.Title = certificate.DocumentName;
            output.Info.Creator = "Housing Hub";
            output.Info.Subject = $"Signed by {certificate.SignerName}";

            var regular = AddStandardFont(output, "/Helvetica");
            var bold = AddStandardFont(output, "/Helvetica-Bold");

            if (!CarrySourceAcross(output, source, sourceExtension, certificate.DocumentId))
                return null;

            var footer = FooterText(certificate);
            foreach (var page in output.Pages)
                StampFooter(page, regular, footer);

            WriteCertificate(output, regular, bold, certificate);

            using var buffer = new MemoryStream();
            output.Save(buffer, closeStream: false);
            return buffer.ToArray();
        }
        catch (Exception ex)
        {
            // Swallowed on purpose. The signature is already recorded and is valid
            // without this file; throwing here would undo an act the tenant has
            // already performed, which is a far worse outcome than no download.
            _logger.LogError(
                ex, "Could not assemble the signed PDF for document {DocumentId}", certificate.DocumentId);
            return null;
        }
    }

    /// <summary>Copies the original into the output, however it arrived.</summary>
    private bool CarrySourceAcross(PdfDocument output, byte[] source, string extension, Guid documentId)
    {
        if (PdfExtensions.Contains(extension))
        {
            using var sourceStream = new MemoryStream(source);
            using var sourceDocument = PdfReader.Open(sourceStream, PdfDocumentOpenMode.Import);

            foreach (var page in sourceDocument.Pages)
                output.AddPage(page);

            return output.PageCount > 0;
        }

        if (ImageExtensions.Contains(extension))
        {
            var page = NewPage(output);

            using var imageStream = new MemoryStream(source);
            using var image = XImage.FromStream(imageStream);
            using var gfx = XGraphics.FromPdfPage(page);

            // Fitted rather than stretched. A squashed agreement is harder to read
            // and invites an argument about whether it is the same document.
            var usableHeight = PageHeight - (Margin * 2);
            var scale = Math.Min(ContentWidth / image.PixelWidth, usableHeight / image.PixelHeight);
            var width = image.PixelWidth * scale;
            var height = image.PixelHeight * scale;

            gfx.DrawImage(image, (PageWidth - width) / 2, Margin, width, height);
            return true;
        }

        _logger.LogWarning(
            "No signed PDF for document {DocumentId}: cannot carry a {Extension} across",
            documentId, extension);

        return false;
    }

    private static string FooterText(SignatureCertificate certificate) =>
        $"Signed electronically by {certificate.SignerName} on {Wat(certificate.SignedAtUtc)}"
        + $"  ·  Housing Hub reference {Reference(certificate.DocumentId)}";

    /// <summary>
    /// Writes a footer onto a page we did not create.
    /// </summary>
    /// <remarks>
    /// Appended as a new content stream and wrapped in q/Q, so the original page's
    /// drawing state is restored before ours runs and ours cannot leak into anything
    /// that follows. The font goes in under a name nothing else would choose, since
    /// an imported page already has resources of its own and /F1 is the first name
    /// anybody picks.
    /// </remarks>
    private static void StampFooter(PdfPage page, PdfDictionary font, string text)
    {
        var resources = page.Elements.GetDictionary("/Resources");
        if (resources is null)
        {
            resources = new PdfDictionary(page.Owner);
            page.Elements["/Resources"] = resources;
        }

        var fonts = resources.Elements.GetDictionary("/Font");
        if (fonts is null)
        {
            fonts = new PdfDictionary(page.Owner);
            resources.Elements["/Font"] = fonts;
        }

        fonts.Elements["/HousingHubStamp"] = font.Reference!;

        // Positioned off the MediaBox rather than off A4. An imported page can be any
        // size, and an origin that is not at zero is ordinary in scanned documents.
        var box = page.MediaBox;
        var content = page.Contents.AppendContent();

        var stream = new StringBuilder()
            .Append("q 0.4 0.4 0.4 rg BT /HousingHubStamp 7 Tf 1 0 0 1 ")
            .Append(Number(box.X1 + 28)).Append(' ').Append(Number(box.Y1 + 20))
            .Append(" Tm ").Append(Literal(text)).Append(" Tj ET Q\n");

        content.CreateStream(Encoding.Latin1.GetBytes(stream.ToString()));
    }

    private void WriteCertificate(
        PdfDocument output, PdfDictionary regular, PdfDictionary bold, SignatureCertificate certificate)
    {
        var cursor = new Cursor(output, regular, bold);

        cursor.Heading("CERTIFICATE OF ELECTRONIC SIGNATURE");
        cursor.Rule();
        cursor.Gap(10);

        cursor.Field("Document", certificate.DocumentName);
        cursor.Field("Property", certificate.PropertyTitle ?? "—");
        cursor.Field("Landlord", certificate.LandlordName ?? "—");
        cursor.Gap(8);

        cursor.Field("Signed by", certificate.SignerName);
        cursor.Field("Date and time", $"{Wat(certificate.SignedAtUtc)}  ({Utc(certificate.SignedAtUtc)})");
        cursor.Field("IP address", certificate.SignerIpAddress ?? "not recorded");
        cursor.Field("Device", certificate.SignerUserAgent ?? "not recorded");
        cursor.Gap(8);

        cursor.Field("Tenancy reference", Reference(certificate.TenancyId));
        cursor.Field("Document reference", Reference(certificate.DocumentId));
        cursor.Field("SHA-256 of the signed document", certificate.SourceFileHash);
        cursor.Gap(14);

        cursor.Rule();
        cursor.Gap(10);
        cursor.Paragraph(
            "This page was produced by Housing Hub at the moment the signature was made. "
            + "The hash above is of the document exactly as it stood then. If the preceding "
            + "pages are ever altered, their hash will no longer match it.");
        cursor.Gap(6);
        cursor.Paragraph(
            "This is a record of an electronic signature, not a cryptographic signature of "
            + "this file. Housing Hub holds the original document, this certificate and the "
            + "audit trail behind it.");

        cursor.Finish();
    }

    // ── PDF primitives ───────────────────────────────────────────

    /// <summary>
    /// One of the fourteen fonts every reader has, so nothing is embedded.
    /// </summary>
    /// <remarks>
    /// WinAnsi because the default encoding for a base font is the font's own, which
    /// has no accented characters in it — and Nigerian names have them.
    /// </remarks>
    private static PdfDictionary AddStandardFont(PdfDocument document, string baseFont)
    {
        var font = new PdfDictionary(document);
        document.Internals.AddObject(font);

        font.Elements["/Type"] = new PdfName("/Font");
        font.Elements["/Subtype"] = new PdfName("/Type1");
        font.Elements["/BaseFont"] = new PdfName(baseFont);
        font.Elements["/Encoding"] = new PdfName("/WinAnsiEncoding");

        return font;
    }

    private static PdfPage NewPage(PdfDocument document)
    {
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(PageWidth);
        page.Height = XUnit.FromPoint(PageHeight);
        return page;
    }

    private static string Number(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// A PDF literal string: parentheses and backslashes carry meaning inside one.
    /// </summary>
    private static string Literal(string text) =>
        "(" + Sanitise(text)
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)") + ")";

    /// <summary>
    /// Reduces text to what WinAnsi can actually render.
    /// </summary>
    /// <remarks>
    /// The typographic characters our own copy uses — curly quotes, en and em
    /// dashes — are mapped to their ASCII equivalents rather than dropped, because
    /// a name or a note full of question marks reads as corruption.
    /// </remarks>
    private static string Sanitise(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            var mapped = character switch
            {
                '‘' or '’' => '\'',
                '“' or '”' => '"',
                '–' or '—' => '-',
                ' ' => ' ',
                '…' => '.',
                _ => character,
            };

            // 0x80-0x9F is where Latin-1 keeps control codes and WinAnsi keeps
            // printable ones, so the two disagree exactly there. Excluded rather
            // than reasoned about.
            if (mapped is (>= ' ' and <= '~') or (>= ' ' and <= 'ÿ'))
                builder.Append(mapped);
            else if (!char.IsControl(mapped))
                builder.Append('?');
        }

        return builder.ToString();
    }

    private static string Reference(Guid id) =>
        id.ToString("N")[..8].ToUpperInvariant();

    /// <summary>
    /// West Africa Time, which is UTC+1 all year — Nigeria keeps no daylight saving.
    /// </summary>
    /// <remarks>
    /// A fixed offset rather than a named zone, because the tz database id differs
    /// between Windows and Linux and this has to render identically on a developer's
    /// machine and on Lambda.
    /// </remarks>
    private static string Wat(DateTime utc) =>
        utc.AddHours(1).ToString("d MMMM yyyy 'at' HH:mm:ss", CultureInfo.InvariantCulture) + " WAT";

    private static string Utc(DateTime utc) =>
        utc.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

    /// <summary>
    /// Lays text down the page, starting a new one when it runs out of room.
    /// </summary>
    /// <remarks>
    /// Overflow onto a second page rather than truncation. The values most likely to
    /// be long are the device string and the hash, and both are evidence — a
    /// certificate that quietly drops the end of a user agent is worse than one that
    /// runs to two pages.
    /// </remarks>
    private sealed class Cursor
    {
        private const double LabelWidth = 150;
        private const double LineHeight = 13;

        private readonly PdfDocument _document;
        private readonly PdfDictionary _regular;
        private readonly PdfDictionary _bold;
        private readonly StringBuilder _content = new();

        private PdfPage _page;
        private double _y;

        public Cursor(PdfDocument document, PdfDictionary regular, PdfDictionary bold)
        {
            _document = document;
            _regular = regular;
            _bold = bold;
            _page = StartPage();
            _y = PageHeight - Margin - 20;
        }

        public void Heading(string text)
        {
            Text(text, Margin, _y, 14, bold: true);
            _y -= 18;
        }

        public void Rule()
        {
            _content
                .Append("0.75 0.75 0.75 RG 0.5 w ")
                .Append(Number(Margin)).Append(' ').Append(Number(_y)).Append(" m ")
                .Append(Number(PageWidth - Margin)).Append(' ').Append(Number(_y)).Append(" l S\n");
            _y -= 4;
        }

        public void Gap(double points) => _y -= points;

        public void Field(string label, string value)
        {
            EnsureRoom();

            Text(label, Margin, _y, 8, bold: true, grey: true);

            var lines = Wrap(value, ContentWidth - LabelWidth, 9.5);
            foreach (var line in lines)
            {
                EnsureRoom();
                Text(line, Margin + LabelWidth, _y, 9.5);
                _y -= LineHeight;
            }

            // A label with no value still consumed a row, so only pad when the value
            // wrapped to nothing at all.
            if (lines.Count == 0) _y -= LineHeight;
        }

        public void Paragraph(string text)
        {
            foreach (var line in Wrap(text, ContentWidth, 8.5))
            {
                EnsureRoom();
                Text(line, Margin, _y, 8.5, grey: true);
                _y -= 11;
            }
        }

        public void Finish() => Flush();

        private PdfPage StartPage()
        {
            var page = NewPage(_document);

            var resources = new PdfDictionary(_document);
            var fonts = new PdfDictionary(_document);
            fonts.Elements["/F1"] = _regular.Reference!;
            fonts.Elements["/F2"] = _bold.Reference!;
            resources.Elements["/Font"] = fonts;
            page.Elements["/Resources"] = resources;

            return page;
        }

        private void EnsureRoom()
        {
            if (_y >= BottomLimit) return;

            Flush();
            _page = StartPage();
            _y = PageHeight - Margin - 20;
        }

        private void Text(string text, double x, double y, double size, bool bold = false, bool grey = false)
        {
            _content
                .Append(grey ? "0.45 0.45 0.45 rg " : "0 0 0 rg ")
                .Append("BT /").Append(bold ? "F2" : "F1").Append(' ').Append(Number(size))
                .Append(" Tf 1 0 0 1 ").Append(Number(x)).Append(' ').Append(Number(y)).Append(" Tm ")
                .Append(Literal(text)).Append(" Tj ET\n");
        }

        private void Flush()
        {
            if (_content.Length == 0) return;

            var stream = new PdfDictionary(_document);
            stream.CreateStream(Encoding.Latin1.GetBytes(_content.ToString()));
            _document.Internals.AddObject(stream);

            var contents = new PdfArray(_document);
            contents.Elements.Add(stream.Reference!);
            _page.Elements["/Contents"] = contents;

            _content.Clear();
        }

        /// <summary>
        /// Breaks on spaces, and mid-token when a token is itself too long.
        /// </summary>
        /// <remarks>
        /// The mid-token case is not theoretical here: a SHA-256 is sixty-four
        /// characters with nowhere to break, and a user agent is one long run of
        /// slashes and brackets.
        /// </remarks>
        private static List<string> Wrap(string text, double width, double size)
        {
            var limit = Math.Max(8, (int)(width / (size * AverageCharWidthRatio)));
            var lines = new List<string>();
            var line = new StringBuilder();

            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var token = word;

                while (token.Length > limit)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }

                    lines.Add(token[..limit]);
                    token = token[limit..];
                }

                if (line.Length == 0) line.Append(token);
                else if (line.Length + 1 + token.Length <= limit) line.Append(' ').Append(token);
                else
                {
                    lines.Add(line.ToString());
                    line.Clear().Append(token);
                }
            }

            if (line.Length > 0) lines.Add(line.ToString());

            return lines;
        }
    }
}
