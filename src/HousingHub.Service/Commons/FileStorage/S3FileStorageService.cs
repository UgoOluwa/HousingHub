using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.Commons.FileStorage;

public class S3FileStorageService : IFileStorageService
{
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

    public async Task<string> UploadFileAsync(IFormFile file, string subDirectory)
    {
        var key = $"{subDirectory}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";

        using var stream = file.OpenReadStream();

        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = file.ContentType
        };

        await _s3Client.PutObjectAsync(putRequest);

        _logger.LogInformation("Uploaded file to S3: {Key}", key);

        return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}";
    }

    public async Task DeleteFileAsync(string fileUrl)
    {
        var key = ExtractKeyFromUrl(fileUrl);
        if (string.IsNullOrEmpty(key))
        {
            _logger.LogWarning("Could not extract S3 key from URL: {Url}", fileUrl);
            return;
        }

        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        await _s3Client.DeleteObjectAsync(deleteRequest);

        _logger.LogInformation("Deleted file from S3: {Key}", key);
    }

    private string? ExtractKeyFromUrl(string fileUrl)
    {
        var prefix = $"https://{_bucketName}.s3.{_region}.amazonaws.com/";
        if (fileUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return fileUrl[prefix.Length..];

        // Fallback: try to parse as URI and use the path
        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
            return uri.AbsolutePath.TrimStart('/');

        return null;
    }
}
