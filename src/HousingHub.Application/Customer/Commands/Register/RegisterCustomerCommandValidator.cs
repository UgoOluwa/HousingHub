using FluentValidation;

namespace HousingHub.Application.Customer.Commands.Register;

public class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.Email).EmailAddress().NotNull().NotEmpty();
        RuleFor(x => x.FirstName).NotNull().NotEmpty();
        RuleFor(x => x.LastName).NotNull().NotEmpty();
        RuleFor(x => x.PhoneNumber).NotNull().NotEmpty();
        RuleFor(x => x.CustomerType).IsInEnum().NotNull().NotEmpty();
        RuleFor(c => c.Password).NotEmpty().MinimumLength(8);
    }
}
