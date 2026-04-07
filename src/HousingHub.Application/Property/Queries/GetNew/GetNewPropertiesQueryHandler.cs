using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetNew;

public class GetNewPropertiesQueryHandler : IRequestHandler<GetNewPropertiesQuery, BaseResponse<List<PropertyDto>>>
{
    private readonly IPropertyQueryService _propertyQueryService;

    public GetNewPropertiesQueryHandler(IPropertyQueryService propertyQueryService)
    {
        _propertyQueryService = propertyQueryService;
    }

    public async Task<BaseResponse<List<PropertyDto>>> Handle(GetNewPropertiesQuery request, CancellationToken cancellationToken)
    {
        var response = await _propertyQueryService.GetNewPropertiesAsync(request.Count);
        return new BaseResponse<List<PropertyDto>>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
