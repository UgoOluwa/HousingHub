using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.Commons.FileStorage;

public class LocalFileStorageService : IFileStorageService
{
    private const string UploadRoot = "wwwroot/uploads";

    public async Task<string> UploadFileAsync(IFormFile file, string subDirectory)
    {
        var directoryPath = Path.Combine(UploadRoot, subDirectory);
        Directory.CreateDirectory(directoryPath);

        var uniqueName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(directoryPath, uniqueName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{subDirectory}/{uniqueName}";
    }

    public Task DeleteFileAsync(string fileUrl)
    {
        var filePath = Path.Combine("wwwroot", fileUrl.TrimStart('/'));
        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }
}
