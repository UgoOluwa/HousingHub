using FluentValidation;

namespace HousingHub.Application.Property.Commands.Create;

public class CreatePropertyCommandValidator : AbstractValidator<CreatePropertyCommand>
{
    public CreatePropertyCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.PropertyType).IsInEnum();
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Availability).IsInEnum();
        RuleFor(x => x.PropertyLeaseType).IsInEnum();
        RuleFor(x => x.OwnerId).NotEmpty();
    }
}
