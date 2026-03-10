using HousingHub.Application.Commons.Bases;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyAddress;
using MediatR;

namespace HousingHub.Application.Property.Commands.Update;

public record UpdatePropertyCommand(
    Guid Id,
    string? Title,
    string? Description,
    PropertyType? PropertyType,
    decimal? Price,
    PropertyAvailability? Availability,
    PropertyLeaseType? PropertyLeaseType,
    PropertyFeature? Features,
    string? ContactPersonName,
    string? ContactPersonEmail,
    string? ContactPersonPhoneNumber,
    UpdatePropertyAddressDto? PropertyAddress,
    Guid AuthenticatedUserId) : IRequest<BaseResponse<PropertyDto?>>;
