using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetDashboardStats;

public record GetOwnerDashboardStatsQuery(Guid OwnerId) : IRequest<BaseResponse<OwnerDashboardStatsDto>>;
