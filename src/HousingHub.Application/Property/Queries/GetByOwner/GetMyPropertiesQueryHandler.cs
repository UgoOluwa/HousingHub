using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetByOwner;

public class GetMyPropertiesQueryHandler : IRequestHandler<GetMyPropertiesQuery, BaseResponse<List<PropertyDto>>>
{
    private readonly IPropertyQueryService _propertyQueryService;

    public GetMyPropertiesQueryHandler(IPropertyQueryService propertyQueryService)
    {
        _propertyQueryService = propertyQueryService;
    }

    public async Task<BaseResponse<List<PropertyDto>>> Handle(GetMyPropertiesQuery request, CancellationToken cancellationToken)
    {
        var response = await _propertyQueryService.GetPropertiesByOwnerAsync(request.OwnerId);
        return new BaseResponse<List<PropertyDto>>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
