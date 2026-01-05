using FluentValidation;

namespace HousingHub.Application.Customer.Commands.Update;

internal class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotNull().NotEmpty();
        RuleFor(x => x.Email).EmailAddress().NotNull().NotEmpty();
        RuleFor(x => x.FirstName).NotNull().NotEmpty();
        RuleFor(x => x.LastName).NotNull().NotEmpty();
        RuleFor(x => x.PhoneNumber).NotNull().NotEmpty();
        RuleFor(x => x.CustomerType).IsInEnum().NotNull().NotEmpty();
    }
}
