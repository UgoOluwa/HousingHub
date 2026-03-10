using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.Commons.FileStorage;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(IFormFile file, string subDirectory);
    Task DeleteFileAsync(string fileUrl);
}
