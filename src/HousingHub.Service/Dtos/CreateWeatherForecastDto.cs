namespace HousingHub.Service.Dtos;

public record CreateWeatherForecastDto(DateOnly Date, int TemperatureC, string? Summary);