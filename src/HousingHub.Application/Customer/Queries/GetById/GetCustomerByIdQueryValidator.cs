using FluentValidation;

namespace HousingHub.Application.Customer.Queries.GetById;

public class GetCustomerByIdQueryValidator : AbstractValidator<GetCustomerByIdQuery>
{
    public GetCustomerByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
    }
}
