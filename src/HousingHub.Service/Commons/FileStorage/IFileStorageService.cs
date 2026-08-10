using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.Commons.FileStorage;

public interface IFileStorageService
{
    /// <summary>
    /// Stores a file that is intended to be publicly readable — property photos and
    /// profile pictures.
    /// </summary>
    /// <param name="contentType">
    /// The content type to store the object as. Callers must pass a value derived
    /// from <see cref="UploadedFileValidator"/> rather than
    /// <see cref="IFormFile.ContentType"/>, which is attacker-controlled: a file
    /// stored as <c>text/html</c> is served as a script from the bucket origin.
    /// </param>
    /// <returns>A public URL.</returns>
    Task<string> UploadFileAsync(IFormFile file, string subDirectory, string contentType);

    /// <summary>
    /// Stores a file that must never be publicly readable — currently KYC identity
    /// documents. Written under a separate key prefix so a bucket policy can deny
    /// anonymous reads on it independently of the public objects.
    /// </summary>
    /// <returns>
    /// The S3 object key, not a URL. Callers persist the key and mint a short-lived
    /// URL on demand via <see cref="GetPresignedUrlAsync"/>.
    /// </returns>
    Task<string> UploadPrivateFileAsync(IFormFile file, string subDirectory, string contentType);

    /// <summary>
    /// Mints a time-limited read URL for a private object.
    /// </summary>
    /// <param name="key">The object key returned by <see cref="UploadPrivateFileAsync"/>.</param>
    /// <param name="lifetime">How long the URL stays valid. Keep this short.</param>
    Task<string> GetPresignedUrlAsync(string key, TimeSpan lifetime);

    Task DeleteFileAsync(string fileUrlOrKey);
}
