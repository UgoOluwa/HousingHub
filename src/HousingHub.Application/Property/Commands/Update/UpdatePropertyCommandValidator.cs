using FluentValidation;

namespace HousingHub.Application.Property.Commands.Update;

/// <summary>
/// Bounds the room counts on edit.
/// </summary>
/// <remarks>
/// Update had no validator at all, so the create-side bounds could be walked straight
/// past by saving a listing and then editing it. Only the nullable room counts are
/// checked here — every other field on this command is a patch whose absence is
/// meaningful, and retrofitting create's required-field rules onto a partial update
/// would reject edits that are legitimately partial.
/// </remarks>
public class UpdatePropertyCommandValidator : AbstractValidator<UpdatePropertyCommand>
{
    public UpdatePropertyCommandValidator()
    {
        RuleFor(x => x.Bedrooms).InclusiveBetween(0, 50)
            .When(x => x.Bedrooms.HasValue)
            .WithMessage("Please enter a number of bedrooms between 0 and 50.");
        RuleFor(x => x.Bathrooms).InclusiveBetween(0, 50)
            .When(x => x.Bathrooms.HasValue)
            .WithMessage("Please enter a number of bathrooms between 0 and 50.");
    }
}
