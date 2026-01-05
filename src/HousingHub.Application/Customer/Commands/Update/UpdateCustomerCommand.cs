using HousingHub.Application.Commons.Bases;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Customer.Commands.Update;

public record UpdateCustomerCommand(Guid Id, string FirstName, string LastName, string Email, string PhoneNumber, CustomerType CustomerType, DateTime? DateOfBirth) : IRequest<BaseResponse<CustomerDto?>>;
