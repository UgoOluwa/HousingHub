using HousingHub.Service.Commons.FileStorage;
using Microsoft.AspNetCore.Http;
using Moq;

namespace HousingHub.Test.FileStorage;

public class UploadedFileValidatorTests
{
    // Real leading bytes for each supported format, trimmed to just enough to be
    // unambiguous. Anything after the signature is padding so streams have >0 bytes
    // to read where that matters.
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] GifBytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00];
    private static readonly byte[] BmpBytes = [0x42, 0x4D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];
    private static readonly byte[] WebpBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] AviBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x41, 0x56, 0x49, 0x20];
    private static readonly byte[] Mp4Bytes = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] WebmBytes = [0x1A, 0x45, 0xDF, 0xA3, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] HtmlBytes = System.Text.Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");

    private static Mock<IFormFile> CreateFormFile(string fileName, byte[] content, long? lengthOverride = null)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(lengthOverride ?? content.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return fileMock;
    }

    // ── Basic guards ─────────────────────────────────────────────

    [Fact]
    public void Validate_NullFile_ReturnsInvalid()
    {
        var result = UploadedFileValidator.Validate(null, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        Assert.Equal("No file was supplied.", result.Error);
    }

    [Fact]
    public void Validate_ZeroLengthFile_ReturnsInvalid()
    {
        var file = CreateFormFile("empty.jpg", [], lengthOverride: 0);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        Assert.Equal("No file was supplied.", result.Error);
    }

    [Fact]
    public void Validate_FileExceedsMaxBytes_ReturnsInvalid()
    {
        var file = CreateFormFile("big.jpg", JpegBytes, lengthOverride: UploadedFileValidator.DefaultMaxBytes + 1);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        // Derived from the limit rather than written out. This asserted on "10MB",
        // which tied it to a number the platform could never honour — so correcting
        // the limit broke the test instead of the test catching the limit.
        Assert.Contains($"{UploadedFileValidator.DefaultMaxBytes / (1024 * 1024)}MB", result.Error);
    }

    [Fact]
    public void Validate_FileAtExactlyMaxBytes_IsNotRejectedForSize()
    {
        var file = CreateFormFile("exact.jpg", JpegBytes, lengthOverride: UploadedFileValidator.DefaultMaxBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NoExtension_ReturnsInvalid()
    {
        var file = CreateFormFile("noextension", JpegBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        Assert.Equal("File has no extension.", result.Error);
    }

    [Fact]
    public void Validate_ExtensionNotInAllowlist_ReturnsInvalid()
    {
        var file = CreateFormFile("doc.exe", JpegBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        Assert.Contains("Unsupported file type", result.Error);
    }

    [Fact]
    public void Validate_ExtensionMatchIsCaseInsensitive()
    {
        var file = CreateFormFile("photo.JPG", JpegBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.True(result.IsValid);
    }

    // ── Signature detection: one real-world case per supported format ───

    [Theory]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    public void Validate_JpegSignature_DetectsImageJpeg(string fileName, string expectedContentType)
    {
        var file = CreateFormFile(fileName, JpegBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.True(result.IsValid);
        Assert.Equal(expectedContentType, result.ContentType);
    }

    [Fact]
    public void Validate_PngSignature_DetectsImagePng()
    {
        var file = CreateFormFile("photo.png", PngBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public void Validate_GifSignature_DetectsImageGif()
    {
        var file = CreateFormFile("photo.gif", GifBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("image/gif", result.ContentType);
    }

    [Fact]
    public void Validate_BmpSignature_DetectsImageBmp()
    {
        var file = CreateFormFile("photo.bmp", BmpBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("image/bmp", result.ContentType);
    }

    [Fact]
    public void Validate_PdfSignature_DetectsApplicationPdf()
    {
        var file = CreateFormFile("scan.pdf", PdfBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.DocumentExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public void Validate_WebpSignature_DetectsImageWebp()
    {
        var file = CreateFormFile("photo.webp", WebpBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("image/webp", result.ContentType);
    }

    [Fact]
    public void Validate_AviRiffSignature_DetectsVideoXMsvideo()
    {
        var file = CreateFormFile("clip.avi", AviBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.VideoExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("video/x-msvideo", result.ContentType);
    }

    [Fact]
    public void Validate_RiffContainerWithUnrecognisedMarker_ReturnsInvalid()
    {
        // Starts with "RIFF" like WebP/AVI, but bytes 8-11 are neither "WEBP" nor "AVI " —
        // exercises the disambiguation's fall-through instead of matching either format.
        byte[] unknownRiff = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x41, 0x43, 0x4F, 0x4E];
        var file = CreateFormFile("mystery.webp", unknownRiff);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        Assert.Contains("do not match a supported format", result.Error);
    }

    [Fact]
    public void Validate_Mp4Signature_DetectsVideoMp4()
    {
        var file = CreateFormFile("clip.mp4", Mp4Bytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.VideoExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("video/mp4", result.ContentType);
    }

    [Fact]
    public void Validate_Mp4SignatureWithMovExtension_IsValid()
    {
        // MOV and MP4 share the ISO base media container and therefore the same
        // leading bytes — the extension map treats them as equivalent.
        var file = CreateFormFile("clip.mov", Mp4Bytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.VideoExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("video/mp4", result.ContentType);
    }

    [Fact]
    public void Validate_WebmSignature_DetectsVideoWebm()
    {
        var file = CreateFormFile("clip.webm", WebmBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.VideoExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("video/webm", result.ContentType);
    }

    [Fact]
    public void Validate_WebmSignatureWithMkvExtension_IsValid()
    {
        // Matroska and WebM share a container and magic number.
        var file = CreateFormFile("clip.mkv", WebmBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.VideoExtensions);

        Assert.True(result.IsValid);
        Assert.Equal("video/webm", result.ContentType);
    }

    // ── The core security guarantee: content must match the claimed extension ───

    [Fact]
    public void Validate_HtmlDisguisedAsJpg_ReturnsInvalid()
    {
        // Named .jpg but is actually an HTML/script payload — the exact stored-XSS
        // vector this validator exists to close.
        var file = CreateFormFile("payload.jpg", HtmlBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        Assert.Contains("do not match a supported format", result.Error);
    }

    [Fact]
    public void Validate_PngBytesNamedAsJpg_ReturnsInvalid()
    {
        // Recognised signature, but it doesn't match the extension the file claims —
        // a real format substituted for another, not just garbage bytes.
        var file = CreateFormFile("disguised.jpg", PngBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        Assert.Contains("do not match the '.jpg' extension", result.Error);
    }

    [Fact]
    public void Validate_PdfBytesNamedAsJpg_ReturnsInvalid()
    {
        // application/pdf is in DocumentExtensions but not valid for a .jpg — still a
        // mismatch even though PDF itself is an otherwise-recognised signature.
        var file = CreateFormFile("disguised.jpg", PdfBytes);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        Assert.Contains("do not match the '.jpg' extension", result.Error);
    }

    [Fact]
    public void Validate_TruncatedFileShorterThanAnySignature_ReturnsInvalid()
    {
        byte[] tooShort = [0xFF];
        var file = CreateFormFile("tiny.jpg", tooShort);

        var result = UploadedFileValidator.Validate(file.Object, UploadedFileValidator.ImageExtensions);

        Assert.False(result.IsValid);
        Assert.Contains("do not match a supported format", result.Error);
    }

    [Fact]
    public void Validate_SvgIsNeverAccepted()
    {
        // SVG is deliberately excluded — it's XML that can carry script and has no
        // fixed magic number, so no signature will ever match it.
        byte[] svg = System.Text.Encoding.UTF8.GetBytes("<svg><script>alert(1)</script></svg>");
        var file = CreateFormFile("image.svg", svg);

        // Even granting an allowlist that (incorrectly) included .svg, content
        // detection alone must still reject it.
        var extensions = new HashSet<string>(UploadedFileValidator.ImageExtensions) { ".svg" };
        var result = UploadedFileValidator.Validate(file.Object, extensions);

        Assert.False(result.IsValid);
    }
}
