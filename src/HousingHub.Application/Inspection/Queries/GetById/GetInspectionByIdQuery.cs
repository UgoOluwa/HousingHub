using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Inspection;
using MediatR;

namespace HousingHub.Application.Inspection.Queries.GetById;

public record GetInspectionByIdQuery(Guid Id) : IRequest<BaseResponse<InspectionDto?>>;
