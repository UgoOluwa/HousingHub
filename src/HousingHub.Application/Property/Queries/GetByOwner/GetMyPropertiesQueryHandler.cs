using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetByOwner;

public class GetMyPropertiesQueryHandler : IRequestHandler<GetMyPropertiesQuery, BaseResponse<HousingHub.Core.CustomResponses.PaginatedResult<PropertyDto>>>
{
    private readonly IPropertyQueryService _propertyQueryService;

    public GetMyPropertiesQueryHandler(IPropertyQueryService propertyQueryService)
    {
        _propertyQueryService = propertyQueryService;
    }

    public async Task<BaseResponse<HousingHub.Core.CustomResponses.PaginatedResult<PropertyDto>>> Handle(GetMyPropertiesQuery request, CancellationToken cancellationToken)
    {
        var response = await _propertyQueryService.GetPropertiesByOwnerPaginatedAsync(request.OwnerId, request.Filter);
        return new BaseResponse<HousingHub.Core.CustomResponses.PaginatedResult<PropertyDto>>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
