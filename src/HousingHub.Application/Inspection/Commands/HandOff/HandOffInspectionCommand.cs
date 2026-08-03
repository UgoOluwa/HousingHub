using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Commands.HandOff;

public record HandOffInspectionCommand(
    Guid InspectionId,
    Guid AuthenticatedUserId) : IRequest<BaseResponse<InspectionDto?>>;
