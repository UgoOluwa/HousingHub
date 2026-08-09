using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.Commons.FileStorage;

public class S3FileStorageService : IFileStorageService
{
    /// <summary>
    /// Key prefix for objects that must never be publicly readable. Kept as a
    /// distinct prefix so the bucket policy can deny anonymous <c>s3:GetObject</c>
    /// on <c>private/*</c> while leaving property photos public.
    /// </summary>
    public const string PrivatePrefix = "private";

    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3FileStorageService> _logger;
    private readonly string _bucketName;
    private readonly string _region;

    public S3FileStorageService(IAmazonS3 s3Client, IConfiguration configuration, ILogger<S3FileStorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
        _bucketName = configuration["AWS:S3:BucketName"]!;
        _region = configuration["AWS:S3:Region"]!;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string subDirectory, string contentType)
    {
        var key = await PutAsync(file, subDirectory, contentType);
        return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}";
    }

    public async Task<string> UploadPrivateFileAsync(IFormFile file, string subDirectory, string contentType)
    {
        // Returns the key rather than a URL. A stored URL would either be permanent
        // (defeating the point) or stale the moment it expired.
        return await PutAsync(file, $"{PrivatePrefix}/{subDirectory}", contentType);
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan lifetime)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(lifetime),
        };

        return _s3Client.GetPreSignedURLAsync(request);
    }

    public async Task DeleteFileAsync(string fileUrlOrKey)
    {
        var key = ExtractKey(fileUrlOrKey);
        if (string.IsNullOrEmpty(key))
        {
            _logger.LogWarning("Could not resolve an S3 key from: {Value}", fileUrlOrKey);
            return;
        }

        await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        });

        _logger.LogInformation("Deleted file from S3: {Key}", key);
    }

    private async Task<string> PutAsync(IFormFile file, string subDirectory, string contentType)
    {
        ArgumentNullException.ThrowIfNull(file);

        // Generated name, never the client's. A caller-supplied filename can carry
        // path traversal or a second extension.
        var key = $"{subDirectory}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";

        using var stream = file.OpenReadStream();

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            // Server-derived from the verified file signature, never file.ContentType.
            ContentType = contentType,
        };

        // Images stay inline so <img src> and next/image keep working; anything else is
        // forced to download rather than render in the bucket's origin. Content-Disposition
        // only affects top-level navigation, so this does not change how images embed.
        // Set via Headers — PutObjectRequest itself has no ContentDisposition member.
        if (!contentType.StartsWith("image/", StringComparison.Ordinal))
            request.Headers.ContentDisposition = "attachment";

        await _s3Client.PutObjectAsync(request);

        _logger.LogInformation("Uploaded file to S3: {Key}", key);

        return key;
    }

    /// <summary>
    /// Accepts either a full public URL (property photos, profile pictures) or a bare
    /// object key (private documents) and returns the key.
    /// </summary>
    private string? ExtractKey(string fileUrlOrKey)
    {
        if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return null;

        if (!fileUrlOrKey.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return fileUrlOrKey.TrimStart('/');

        var prefix = $"https://{_bucketName}.s3.{_region}.amazonaws.com/";
        if (fileUrlOrKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return fileUrlOrKey[prefix.Length..];

        if (Uri.TryCreate(fileUrlOrKey, UriKind.Absolute, out var uri))
            return uri.AbsolutePath.TrimStart('/');

        return null;
    }
}
