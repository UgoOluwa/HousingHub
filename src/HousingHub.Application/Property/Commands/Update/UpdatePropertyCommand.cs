using HousingHub.Application.Commons.Bases;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyAddress;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    /// <summary>
    /// Who is asking. Taken from the JWT by the controller, never from the request.
    /// </summary>
    /// <remarks>
    /// <see cref="BindNeverAttribute"/> is the point. This is the field a pen test
    /// reported as an authentication bypass by query parameter: an identity that the
    /// service authorises against, sitting on a type bound from client input. The
    /// controller does overwrite it — <c>command with { AuthenticatedUserId =
    /// userId.Value }</c> — so the reported bypass did not work, but the shape only
    /// held because one line in one controller remembered to. It is now unbindable,
    /// so a request cannot supply it and a future endpoint cannot forget.
    /// </remarks>
    [property: BindNever] Guid AuthenticatedUserId,
    int? Bedrooms = null,
    int? Bathrooms = null) : IRequest<BaseResponse<PropertyDto?>>;
