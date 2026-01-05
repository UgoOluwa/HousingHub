using FluentValidation;

namespace HousingHub.Application.Customer.Commands.Create;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Email).EmailAddress().NotNull().NotEmpty();
        RuleFor(x => x.FirstName).NotNull().NotEmpty();
        RuleFor(x => x.LastName).NotNull().NotEmpty();
        RuleFor(x => x.PhoneNumber).NotNull().NotEmpty();
        RuleFor(x => x.CustomerType).IsInEnum().NotNull().NotEmpty();
    }
}
