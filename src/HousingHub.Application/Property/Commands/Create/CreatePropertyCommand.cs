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
    CreatePropertyAddressDto? PropertyAddress,
    IList<IFormFile>? Files = null,
    // true = publish immediately; false = save as a draft the owner can publish later.
    bool Publish = true) : IRequest<BaseResponse<PropertyDto?>>;
