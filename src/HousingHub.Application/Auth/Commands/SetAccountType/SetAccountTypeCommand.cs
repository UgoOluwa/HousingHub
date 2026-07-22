using HousingHub.Application.Commons.Bases;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.SetAccountType;

/// <summary>
/// One-time onboarding step: the authenticated user tells us how they intend to use
/// Housing Hub. CustomerId is taken from the JWT, never from the request body.
/// </summary>
public record SetAccountTypeCommand(CustomerType CustomerType, Guid CustomerId = default)
    : IRequest<BaseResponse<LoginCustomerResponseDto?>>;
