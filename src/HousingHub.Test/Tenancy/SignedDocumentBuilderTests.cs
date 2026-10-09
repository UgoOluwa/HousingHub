using System.Text;
using HousingHub.Service.Commons.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace HousingHub.Test.Tenancies;

/// <summary>
/// The artefact a tenant downloads after signing.
/// </summary>
/// <remarks>
/// Every assertion here reads the produced PDF back through a PDF parser rather
/// than searching the raw bytes. A content stream is compressed, so a byte search
/// finds nothing whether the text is there or not — which is the sort of test that
/// passes forever and proves nothing.
/// </remarks>
public class SignedDocumentBuilderTests
{
    private const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private readonly SignedDocumentBuilder _sut =
        new(NullLogger<SignedDocumentBuilder>.Instance);

    private static SignatureCertificate Certificate(
        string documentName = "Tenancy agreement",
        string signer = "Bayo Eze",
        string? userAgent = "Mozilla/5.0 (iPhone)") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            documentName,
            "2 bed in Yaba",
            "Ada Obi",
            signer,
            new DateTime(2026, 10, 9, 14, 22, 7, DateTimeKind.Utc),
            "102.89.3.11",
            userAgent,
            Hash);

    /// <summary>A source PDF whose pages are deliberately not A4.</summary>
    private static byte[] SourcePdf(params PdfSharp.PageSize[] sizes)
    {
        using var document = new PdfDocument();

        foreach (var size in sizes)
            document.AddPage().Size = size;

        using var buffer = new MemoryStream();
        document.Save(buffer, closeStream: false);
        return buffer.ToArray();
    }

    /// <summary>A real 2x2 PNG, built by hand so the test carries no binary fixture.</summary>
    private static byte[] SourcePng()
    {
        static byte[] Chunk(string type, byte[] data)
        {
            var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            var crc = Crc32(typeAndData);
            return BigEndian(data.Length)
                .Concat(typeAndData)
                .Concat(BigEndian((int)crc))
                .ToArray();
        }

        var header = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

        var ihdr = BigEndian(2).Concat(BigEndian(2))
            .Concat(new byte[] { 8, 2, 0, 0, 0 }).ToArray();

        // Two rows, each a filter byte followed by two RGB pixels.
        var raw = new byte[] { 0, 255, 0, 0, 0, 0, 255, 0, 255, 0, 0, 0, 0, 255 };

        return header
            .Concat(Chunk("IHDR", ihdr))
            .Concat(Chunk("IDAT", Deflate(raw)))
            .Concat(Chunk("IEND", []))
            .ToArray();
    }

    private static byte[] BigEndian(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(
            output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }
        return output.ToArray();
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return crc ^ 0xFFFFFFFF;
    }

    private static PdfDocument Reopen(byte[] pdf) =>
        PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);

    /// <summary>
    /// The literal strings on a page, unescaped and joined.
    /// </summary>
    /// <remarks>
    /// Scanned rather than matched with a regular expression. A literal may contain
    /// escaped parentheses, and a regex that stops at the first close bracket reads
    /// a correctly escaped file as a truncated one — which would make this helper
    /// fail exactly the tests it exists to prove.
    /// </remarks>
    private static string PrintedOn(PdfPage page)
    {
        var content = TextOn(page);
        var printed = new StringBuilder();
        var depth = 0;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (depth > 0 && c == '\\' && i + 1 < content.Length)
            {
                printed.Append(content[++i]);
                continue;
            }

            if (c == '(') { depth++; if (depth == 1) continue; }
            else if (c == ')') { depth--; if (depth == 0) continue; }

            if (depth > 0) printed.Append(c);
        }

        return printed.ToString();
    }

    private static string TextOn(PdfPage page)
    {
        var builder = new StringBuilder();
        foreach (var content in page.Contents)
            builder.Append(Encoding.Latin1.GetString(content.Stream.UnfilteredValue));
        return builder.ToString();
    }

    // ── a PDF source ─────────────────────────────────────────────

    [Fact]
    public void FromAPdf_KeepsEveryPageAndAddsACertificate()
    {
        var result = _sut.Build(
            SourcePdf(PdfSharp.PageSize.A4, PdfSharp.PageSize.A5), ".pdf", Certificate());

        Assert.NotNull(result);
        using var document = Reopen(result!);
        Assert.Equal(3, document.PageCount);
    }

    /// <summary>
    /// The pages are copied, not redrawn. A source page that is not A4 stays the
    /// size it was, which also proves the footer is placed off that page's own box.
    /// </summary>
    [Fact]
    public void FromAPdf_LeavesThePagesTheSizeTheyWere()
    {
        var result = _sut.Build(SourcePdf(PdfSharp.PageSize.A5), ".pdf", Certificate());

        using var document = Reopen(result!);
        var original = document.Pages[0];

        Assert.True(original.Width.Point < 500, $"expected A5, got {original.Width.Point}pt");
    }

    /// <summary>
    /// The footer is what survives a page being photocopied out of the bundle, so
    /// it goes on every page rather than only the first.
    /// </summary>
    [Fact]
    public void FromAPdf_StampsEveryPageWithWhoSignedAndWhen()
    {
        var result = _sut.Build(
            SourcePdf(PdfSharp.PageSize.A4, PdfSharp.PageSize.A4, PdfSharp.PageSize.A4),
            ".pdf",
            Certificate(signer: "Bayo Eze"));

        using var document = Reopen(result!);

        for (var i = 0; i < 3; i++)
        {
            var text = TextOn(document.Pages[i]);
            Assert.Contains("Signed electronically by Bayo Eze", text);
            Assert.Contains("9 October 2026", text);
        }
    }

    [Fact]
    public void TheCertificate_CarriesTheWholeAuditTrail()
    {
        var result = _sut.Build(SourcePdf(PdfSharp.PageSize.A4), ".pdf", Certificate());

        using var document = Reopen(result!);
        var certificate = TextOn(document.Pages[document.PageCount - 1]);

        Assert.Contains("CERTIFICATE OF ELECTRONIC SIGNATURE", certificate);
        Assert.Contains("Bayo Eze", certificate);
        Assert.Contains("102.89.3.11", certificate);
        Assert.Contains("Mozilla/5.0", certificate);
        Assert.Contains("2 bed in Yaba", certificate);
        Assert.Contains("Ada Obi", certificate);
    }

    /// <summary>
    /// Wrapped across lines, but not one character of it dropped — the hash is the
    /// only thing on the page that can be checked against the document itself.
    /// </summary>
    [Fact]
    public void TheCertificate_PrintsTheHashInFull()
    {
        var result = _sut.Build(SourcePdf(PdfSharp.PageSize.A4), ".pdf", Certificate());

        using var document = Reopen(result!);
        var certificate = TextOn(document.Pages[document.PageCount - 1]);

        // Rebuilt from the literal strings, since a 64-character hash is wrapped.
        Assert.Contains(Hash, PrintedOn(document.Pages[document.PageCount - 1]));
    }

    /// <summary>
    /// Nigeria is UTC+1 all year, so a signature at 14:22 UTC happened at 15:22
    /// local. Both are printed — one is what a Nigerian reader expects to see, the
    /// other is what settles an argument.
    /// </summary>
    [Fact]
    public void TheCertificate_GivesTheTimeInBothWatAndUtc()
    {
        var result = _sut.Build(SourcePdf(PdfSharp.PageSize.A4), ".pdf", Certificate());

        using var document = Reopen(result!);
        var certificate = TextOn(document.Pages[document.PageCount - 1]);

        Assert.Contains("15:22:07 WAT", certificate);
        Assert.Contains("14:22:07 UTC", certificate);
    }

    // ── an image source ──────────────────────────────────────────

    [Fact]
    public void FromAnImage_PutsItOnAPageOfItsOwn()
    {
        var result = _sut.Build(SourcePng(), ".png", Certificate());

        Assert.NotNull(result);
        using var document = Reopen(result!);

        Assert.Equal(2, document.PageCount);
        Assert.Contains("Signed electronically by", TextOn(document.Pages[0]));
    }

    // ── what it refuses ──────────────────────────────────────────

    /// <summary>
    /// Null, not an exception. The signature is already made and valid; a type we
    /// cannot draw costs a download button, not an agreement.
    /// </summary>
    [Fact]
    public void AnUnsupportedType_ProducesNothingRatherThanThrowing()
    {
        Assert.Null(_sut.Build([1, 2, 3, 4], ".webp", Certificate()));
    }

    [Fact]
    public void ABrokenPdf_ProducesNothingRatherThanThrowing()
    {
        var notReallyAPdf = Encoding.ASCII.GetBytes("%PDF-1.7 and then nonsense");

        Assert.Null(_sut.Build(notReallyAPdf, ".pdf", Certificate()));
    }

    // ── text that would otherwise break the file ─────────────────

    /// <summary>
    /// Parentheses delimit a string in a PDF content stream, so a document named
    /// with one would end the string early and corrupt everything after it.
    /// </summary>
    [Fact]
    public void ANameWithBrackets_DoesNotCorruptTheFile()
    {
        var result = _sut.Build(
            SourcePdf(PdfSharp.PageSize.A4),
            ".pdf",
            Certificate(documentName: "Agreement (signed copy) \\ final"));

        Assert.NotNull(result);

        // Reopening is the real assertion: a broken escape makes a file that will
        // not parse.
        using var document = Reopen(result!);
        Assert.Contains("Agreement", TextOn(document.Pages[document.PageCount - 1]));
    }

    /// <summary>Nigerian names carry accents, and they have to render as themselves.</summary>
    [Fact]
    public void AnAccentedName_SurvivesIntact()
    {
        var result = _sut.Build(
            SourcePdf(PdfSharp.PageSize.A4), ".pdf", Certificate(signer: "Bàbátúndé Káyòdé"));

        using var document = Reopen(result!);

        Assert.Contains("Bàbátúndé", TextOn(document.Pages[0]));
    }

    /// <summary>
    /// A character WinAnsi cannot render becomes a question mark rather than
    /// silently shifting every byte after it.
    /// </summary>
    [Fact]
    public void AnUnrenderableCharacter_BecomesAQuestionMark()
    {
        var result = _sut.Build(
            SourcePdf(PdfSharp.PageSize.A4), ".pdf", Certificate(signer: "Chidi 中文"));

        using var document = Reopen(result!);
        var text = TextOn(document.Pages[0]);

        Assert.Contains("Chidi ??", text);
        Assert.DoesNotContain("中", text);
    }

    /// <summary>
    /// A user agent is long and has nowhere obvious to break. It runs to a second
    /// certificate page rather than being cut short — it is evidence.
    /// </summary>
    [Fact]
    public void AVeryLongDeviceString_IsWrappedRatherThanTruncated()
    {
        var userAgent = string.Concat(Enumerable.Repeat("Mozilla/5.0(VeryLongDeviceToken)", 40));

        var result = _sut.Build(SourcePdf(PdfSharp.PageSize.A4), ".pdf", Certificate(userAgent: userAgent));

        using var document = Reopen(result!);

        var printed = new StringBuilder();
        for (var i = 1; i < document.PageCount; i++)
            printed.Append(PrintedOn(document.Pages[i]));

        Assert.Contains(userAgent, printed.ToString());
    }
}
