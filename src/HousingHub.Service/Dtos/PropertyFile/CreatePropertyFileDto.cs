using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.Dtos.PropertyFile;

public record CreatePropertyFileDto(IFormFile file, Guid PropertyId);
