using HousingHub.Application.Commons.Bases;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Customer;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HousingHub.Application.Auth.Commands.SetAccountType;

/// <summary>
/// One-time onboarding step: the authenticated user tells us how they intend to use
/// Housing Hub.
/// </summary>
public record SetAccountTypeCommand(
    CustomerType CustomerType,

    /// <summary>
    /// Who is being changed. Taken from the JWT by the controller.
    /// </summary>
    /// <remarks>
    /// <see cref="BindNeverAttribute"/> so it cannot arrive from the request at all.
    /// The controller already overwrites it, and that was already correct — but "the
    /// controller remembers to overwrite it" is a convention, and a pen test read
    /// this shape on the property endpoints as an authentication bypass by parameter.
    /// Making the field unbindable turns the convention into something the framework
    /// enforces, so a future endpoint that forgets cannot be exploited.
    ///
    /// It also stops the field appearing in validation errors, which is how an
    /// anonymous caller learns that a server-side identity field exists here.
    /// </remarks>
    [property: BindNever] Guid CustomerId = default)
    : IRequest<BaseResponse<LoginCustomerResponseDto?>>;
