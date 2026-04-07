using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetNearby;

public class GetNearbyPropertiesQueryHandler : IRequestHandler<GetNearbyPropertiesQuery, BaseResponse<List<PropertyDto>>>
{
    private readonly IPropertyQueryService _propertyQueryService;

    public GetNearbyPropertiesQueryHandler(IPropertyQueryService propertyQueryService)
    {
        _propertyQueryService = propertyQueryService;
    }

    public async Task<BaseResponse<List<PropertyDto>>> Handle(GetNearbyPropertiesQuery request, CancellationToken cancellationToken)
    {
        var response = await _propertyQueryService.GetNearbyPropertiesAsync(
            request.Latitude, request.Longitude, request.RadiusKm, request.Count);
        return new BaseResponse<List<PropertyDto>>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
