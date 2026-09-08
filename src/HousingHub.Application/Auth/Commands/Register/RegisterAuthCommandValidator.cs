using FluentValidation;
using HousingHub.Model.Enums;

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

        // Registration is anonymous, so CustomerType arrives from an untrusted body.
        // Without this rule a caller could register with CustomerType.Admin (8) and
        // receive a token whose customer_type claim satisfies the AdminOnly policy.
        //
        // A pen test noted that a caller can still self-select HouseOwner, Agent or
        // Developer here, bypassing the onboarding screen. That is accepted, and the
        // reason is that the type is a statement of intent rather than a credential:
        //
        //   - Publishing a listing requires an admin-verified identity, checked in
        //     PropertyCommandService.SetPublishedAsync, not the account type.
        //   - Every badge a renter sees comes from VerificationTier, which only an
        //     admin decision sets.
        //   - So the most a self-declared owner gets is the ability to assemble
        //     drafts nobody can see.
        //
        // The message deliberately no longer lists the enum members. Naming the
        // internal roles — including ones the signup form does not offer — told an
        // anonymous caller more about the model than it helped anyone, and the only
        // requests that reach this rule are hand-made ones.
        RuleFor(x => x.CustomerType)
            .Must(t => t.IsSelectableAtOnboarding())
            .WithMessage("Please choose one of the account types offered on the signup form.");
    }
}
