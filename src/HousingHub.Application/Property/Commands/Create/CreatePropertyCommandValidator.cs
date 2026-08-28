using FluentValidation;

namespace HousingHub.Application.Property.Commands.Create;

public class CreatePropertyCommandValidator : AbstractValidator<CreatePropertyCommand>
{
    public CreatePropertyCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        // Default copy for IsInEnum reads "'Property Type' has a range of values which
        // does not include '0'." — a sentence about the enum, shown to someone filling
        // in a form. Every rule below is overridden for the same reason.
        RuleFor(x => x.PropertyType).IsInEnum()
            .WithMessage("Please choose a property type from the list.");
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Availability).IsInEnum()
            .WithMessage("Please choose whether this listing is available, rented, under offer or sold.");
        RuleFor(x => x.PropertyLeaseType).IsInEnum()
            .WithMessage("Please choose whether this listing is for rent, lease or sale.");
        RuleFor(x => x.OwnerId).NotEmpty();
    }
}
