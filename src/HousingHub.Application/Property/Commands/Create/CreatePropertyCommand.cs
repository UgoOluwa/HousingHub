using HousingHub.Application.Commons.Bases;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyAddress;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace HousingHub.Application.Property.Commands.Create;

public record
    CreatePropertyCommand(
    string Title,
    string Description,
    PropertyType PropertyType,
    decimal Price,
    PropertyAvailability Availability,
    PropertyLeaseType PropertyLeaseType,
    PropertyFeature Features,
    string? ContactPersonName,
    string? ContactPersonEmail,
    string? ContactPersonPhoneNumber,
    Guid OwnerId,
    // UpdatePropertyAddressDto, not CreatePropertyAddressDto — this is nested inside a
    // property that doesn't exist yet, so there's no PropertyId to bind from the form
    // (CreatePropertyAddressDto requires one; form binding silently dropped the whole
    // object when it was missing, so no address ever got saved on create).
    UpdatePropertyAddressDto? PropertyAddress,
    IList<IFormFile>? Files = null,
    bool ConfirmDuplicate = false) : IRequest<BaseResponse<CreatePropertyResultDto?>>;
