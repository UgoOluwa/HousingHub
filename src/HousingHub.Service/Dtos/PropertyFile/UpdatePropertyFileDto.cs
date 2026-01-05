using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.Dtos.PropertyFile;

public record UpdatePropertyFileDto(Guid Id, IFormFile file, Guid PropertyId);
