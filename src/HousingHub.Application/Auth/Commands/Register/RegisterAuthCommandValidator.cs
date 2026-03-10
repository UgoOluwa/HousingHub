using FluentValidation;

namespace HousingHub.Application.Auth.Commands.Register;

public class RegisterAuthCommandValidator : AbstractValidator<RegisterAuthCommand>
{
    public RegisterAuthCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
