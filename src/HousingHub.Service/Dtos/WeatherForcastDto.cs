namespace HousingHub.Service.Dtos;

public record WeatherForcastDto(Guid Id, DateTime DateCreated, DateTime DateModified, DateOnly Date, int TemperatureC, int TemperatureF, string? Summary);
