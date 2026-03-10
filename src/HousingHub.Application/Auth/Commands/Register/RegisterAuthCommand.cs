using HousingHub.Application.Commons.Bases;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Auth.Commands.Register;

public record RegisterAuthCommand(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Password,
    CustomerType CustomerType) : IRequest<BaseResponse<CustomerDto?>>;
