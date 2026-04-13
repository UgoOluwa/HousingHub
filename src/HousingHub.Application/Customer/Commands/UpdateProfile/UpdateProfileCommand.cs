using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Customer.Commands.UpdateProfile;

public record UpdateProfileCommand(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateTime? DateOfBirth,
    string? JobTitle,
    string? CompanyName,
    string? Industry) : IRequest<BaseResponse<CustomerDto?>>;
