using FluentValidation;

namespace HousingHub.Application.WeatherForecast.Commands.Create;

public class CreateWeatherForecastCommandValidator : AbstractValidator<CreateWeatherForecastCommand>
{
    public CreateWeatherForecastCommandValidator()
    {
        RuleFor(x => x.Date).NotNull().NotEmpty();
        RuleFor(x => x.TemperatureC).NotNull().NotEmpty();
        RuleFor(x => x.Summary).MaximumLength(500);
    }
}
