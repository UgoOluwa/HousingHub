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

    /// <summary>
    /// Reads a private object back into memory.
    /// </summary>
    /// <remarks>
    /// A byte array rather than a stream: callers hash or re-encode the whole thing,
    /// the size ceiling is already enforced at upload, and a returned stream is one
    /// more thing a caller can forget to dispose. Null when the object is not there.
    /// </remarks>
    Task<byte[]?> ReadPrivateFileAsync(string key);

    /// <summary>
    /// Stores bytes we generated ourselves, rather than a file somebody uploaded.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="UploadPrivateFileAsync"/> because the safety rules
    /// differ: an uploaded file needs its name discarded and its type verified from
    /// the bytes, while this content is ours and its type is known by construction.
    /// </remarks>
    Task<string> UploadPrivateBytesAsync(
        byte[] content, string subDirectory, string extension, string contentType);

    Task DeleteFileAsync(string fileUrlOrKey);
}
