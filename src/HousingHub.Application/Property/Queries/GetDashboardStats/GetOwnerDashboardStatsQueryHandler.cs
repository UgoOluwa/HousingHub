using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetDashboardStats;

public class GetOwnerDashboardStatsQueryHandler : IRequestHandler<GetOwnerDashboardStatsQuery, BaseResponse<OwnerDashboardStatsDto>>
{
    private readonly IPropertyQueryService _propertyQueryService;

    public GetOwnerDashboardStatsQueryHandler(IPropertyQueryService propertyQueryService)
    {
        _propertyQueryService = propertyQueryService;
    }

    public async Task<BaseResponse<OwnerDashboardStatsDto>> Handle(GetOwnerDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var response = await _propertyQueryService.GetOwnerDashboardStatsAsync(request.OwnerId);
        return new BaseResponse<OwnerDashboardStatsDto>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
