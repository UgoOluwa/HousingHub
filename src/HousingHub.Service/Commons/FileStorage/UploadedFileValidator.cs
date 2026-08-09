using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.Commons.FileStorage;

/// <summary>
/// Shared validation for user-supplied uploads.
/// </summary>
/// <remarks>
/// Property files were the only upload path with any validation; KYC documents and
/// profile photos had none at all, so any authenticated user could push arbitrary
/// content of arbitrary size into the bucket.
///
/// Extension checks alone are not enough. The browser-supplied content type is
/// attacker-controlled, so a file named <c>.jpg</c> could be stored as
/// <c>text/html</c> and served as a script from the bucket origin. This class
/// therefore derives the content type from the *verified* file signature rather
/// than trusting either the extension or the declared type.
/// </remarks>
public static class UploadedFileValidator
{
    public const long DefaultMaxBytes = 10 * 1024 * 1024;   // 10 MB
    public const long DocumentMaxBytes = 15 * 1024 * 1024;  // 15 MB — scans run larger

    public static readonly IReadOnlySet<string> ImageExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    public static readonly IReadOnlySet<string> VideoExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".avi", ".mkv", ".webm" };

    /// <summary>Identity documents: images plus PDF, which is how most scans arrive.</summary>
    public static readonly IReadOnlySet<string> DocumentExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

    /// <summary>
    /// Leading byte signatures, mapped to the content type we will serve them as.
    /// Deliberately excludes SVG: it is an image by extension but an XML document
    /// that can carry script, and it has no fixed magic number.
    /// </summary>
    private static readonly (byte[] Signature, int Offset, string ContentType)[] Signatures =
    [
        ([0xFF, 0xD8, 0xFF], 0, "image/jpeg"),
        ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 0, "image/png"),
        ([0x47, 0x49, 0x46, 0x38], 0, "image/gif"),
        ([0x42, 0x4D], 0, "image/bmp"),
        ([0x25, 0x50, 0x44, 0x46], 0, "application/pdf"),          // %PDF

        // RIFF containers. Bytes 0-3 are "RIFF" for both WebP and AVI; the format is
        // identified by bytes 8-11, checked in DetectContentType.
        ([0x52, 0x49, 0x46, 0x46], 0, "image/webp"),
        ([0x52, 0x49, 0x46, 0x46], 0, "video/x-msvideo"),          // AVI

        // ISO base media (MP4, MOV, M4V). The first four bytes are a box length that
        // varies by encoder, so match "ftyp" at offset 4 rather than a fixed prefix —
        // matching only 0x18/0x20 lengths rejected most real-world files.
        ([0x66, 0x74, 0x79, 0x70], 4, "video/mp4"),

        ([0x1A, 0x45, 0xDF, 0xA3], 0, "video/webm"),               // Matroska / WebM
    ];

    public sealed record Result(bool IsValid, string? Error, string ContentType)
    {
        public static Result Invalid(string error) => new(false, error, "application/octet-stream");
        public static Result Valid(string contentType) => new(true, null, contentType);
    }

    /// <summary>
    /// Validates size, extension and file signature, and returns the content type the
    /// file should be *stored* as.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <param name="allowedExtensions">Permitted extensions, including the leading dot.</param>
    /// <param name="maxBytes">Maximum accepted size.</param>
    public static Result Validate(
        IFormFile? file,
        IReadOnlySet<string> allowedExtensions,
        long maxBytes = DefaultMaxBytes)
    {
        if (file is null || file.Length == 0)
            return Result.Invalid("No file was supplied.");

        if (file.Length > maxBytes)
            return Result.Invalid($"File exceeds the {maxBytes / (1024 * 1024)}MB limit.");

        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension))
            return Result.Invalid("File has no extension.");

        if (!allowedExtensions.Contains(extension))
        {
            return Result.Invalid(
                $"Unsupported file type '{extension}'. Allowed: {string.Join(", ", allowedExtensions.Order())}.");
        }

        var detected = DetectContentType(file);

        if (detected is null)
        {
            return Result.Invalid(
                "File contents do not match a supported format. The file may be corrupt or renamed.");
        }

        // A .png whose bytes say PDF is either a mistake or an attack; either way we
        // should not store it under a misleading name.
        if (!ContentTypeMatchesExtension(detected, extension))
            return Result.Invalid($"File contents do not match the '{extension}' extension.");

        return Result.Valid(detected);
    }

    /// <summary>
    /// Reads the leading bytes and returns the matching content type, or null when the
    /// signature is unrecognised. Never trusts <see cref="IFormFile.ContentType"/>.
    /// </summary>
    private static string? DetectContentType(IFormFile file)
    {
        const int headerLength = 16;
        Span<byte> header = stackalloc byte[headerLength];

        using (var stream = file.OpenReadStream())
        {
            int read = 0;
            while (read < headerLength)
            {
                int chunk = stream.Read(header[read..]);
                if (chunk == 0) break;
                read += chunk;
            }

            if (read == 0) return null;
            header = header[..read];
        }

        foreach (var (signature, offset, contentType) in Signatures)
        {
            if (header.Length < offset + signature.Length) continue;
            if (!header.Slice(offset, signature.Length).SequenceEqual(signature)) continue;

            // RIFF is a container, not a format. Disambiguate on bytes 8-11.
            if (contentType is "image/webp" or "video/x-msvideo")
            {
                if (header.Length < 12) continue;

                ReadOnlySpan<byte> marker = contentType == "image/webp"
                    ? [0x57, 0x45, 0x42, 0x50]   // "WEBP"
                    : [0x41, 0x56, 0x49, 0x20];  // "AVI "

                if (!header.Slice(8, 4).SequenceEqual(marker)) continue;
            }

            return contentType;
        }

        return null;
    }

    private static bool ContentTypeMatchesExtension(string contentType, string extension) =>
        (contentType, extension.ToLowerInvariant()) switch
        {
            ("image/jpeg", ".jpg" or ".jpeg") => true,
            ("image/png", ".png") => true,
            ("image/gif", ".gif") => true,
            ("image/bmp", ".bmp") => true,
            ("image/webp", ".webp") => true,
            ("application/pdf", ".pdf") => true,
            // MP4 and QuickTime share the ISO base media container.
            ("video/mp4", ".mp4" or ".mov") => true,
            // Matroska and WebM share a container and magic number.
            ("video/webm", ".webm" or ".mkv") => true,
            ("video/x-msvideo", ".avi") => true,
            _ => false,
        };
}
