using HousingHub.Application.Commons.Bases;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace HousingHub.Application.Customer.Commands.UpdateProfilePhoto;

/// <summary>
/// Sets or clears a customer's profile photo. A null File clears it.
/// CustomerId comes from the JWT, never the request body.
/// </summary>
public record UpdateProfilePhotoCommand(Guid CustomerId, IFormFile? File) : IRequest<BaseResponse<string?>>;
