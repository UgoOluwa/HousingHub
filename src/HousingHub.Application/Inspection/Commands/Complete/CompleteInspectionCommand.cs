using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.Complete;

/// <summary>
/// The renter confirming their viewing went ahead.
/// </summary>
/// <remarks>
/// <paramref name="AuthenticatedUserId"/> arrives from the JWT — the controller
/// overwrites whatever was in the body before this is sent, as every other command
/// here does.
/// </remarks>
public record CompleteInspectionCommand(
    Guid InspectionId,
    string? Comment,
    Guid AuthenticatedUserId) : IRequest<BaseResponse<InspectionDto?>>;
