using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetTrending;

public class GetTrendingPropertiesQueryHandler : IRequestHandler<GetTrendingPropertiesQuery, BaseResponse<List<PropertyDto>>>
{
    private readonly IPropertyQueryService _propertyQueryService;

    public GetTrendingPropertiesQueryHandler(IPropertyQueryService propertyQueryService)
    {
        _propertyQueryService = propertyQueryService;
    }

    public async Task<BaseResponse<List<PropertyDto>>> Handle(GetTrendingPropertiesQuery request, CancellationToken cancellationToken)
    {
        var response = await _propertyQueryService.GetTrendingPropertiesAsync(request.Count, request.Skip);
        return new BaseResponse<List<PropertyDto>>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
