using HousingHub.Application.Commons.Bases;
using HousingHub.Model.Enums;
using MediatR;

namespace HousingHub.Application.Customer.Commands.SubmitKyc;

public record SubmitKycCommand(
    Guid CustomerId,
    DateTime? DateOfBirth,
    string NationalIdNumber,
    IDType IdType,
    string? IdDocumentUrl,
    string? JobTitle,
    string? CompanyName,
    string? Industry) : IRequest<BaseResponse<bool>>;
