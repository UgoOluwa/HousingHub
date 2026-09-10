using HousingHub.Application.Commons.Bases;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyAddress;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
    /// <summary>
    /// Whose listing this becomes. Set by the controller from the JWT.
    /// </summary>
    /// <remarks>
    /// Unbindable for the same reason as UpdatePropertyCommand.AuthenticatedUserId:
    /// a client that could name the owner could create listings under somebody
    /// else's account. The controller overwrites it, and now nothing can supply it.
    /// </remarks>
    [property: BindNever] Guid OwnerId,
    // UpdatePropertyAddressDto, not CreatePropertyAddressDto — this is nested inside a
    // property that doesn't exist yet, so there's no PropertyId to bind from the form
    // (CreatePropertyAddressDto requires one; form binding silently dropped the whole
    // object when it was missing, so no address ever got saved on create).
    UpdatePropertyAddressDto? PropertyAddress,
    IList<IFormFile>? Files = null,
    bool ConfirmDuplicate = false,
    int? Bedrooms = null,
    int? Bathrooms = null) : IRequest<BaseResponse<CreatePropertyResultDto?>>;
