namespace HousingHub.Service.Dtos.PropertyFile;

public record PropertyFileDto(Guid Id, DateTime DateCreated, DateTime DateModified, string FileUrl, int Type, DateTime DateUploaded, Guid PropertyId);
