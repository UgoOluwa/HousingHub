using HousingHub.Application.Commons.Bases;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetAll;

public class GetAllPropertiesQueryHandler : IRequestHandler<GetAllPropertiesQuery, BaseResponsePagination<PaginatedResult<PropertyDto>>>
{
    private readonly IPropertyQueryService _propertyQueryService;

    public GetAllPropertiesQueryHandler(IPropertyQueryService propertyQueryService)
    {
        _propertyQueryService = propertyQueryService;
    }

    public async Task<BaseResponsePagination<PaginatedResult<PropertyDto>>> Handle(GetAllPropertiesQuery request, CancellationToken cancellationToken)
    {
        var response = await _propertyQueryService.GetAllPropertiesPaginatedAsync(request.Filter);
        var paginatedResponse = new BaseResponsePagination<PaginatedResult<PropertyDto>>(
            response.IsSuccessful, response.Data, response.Message, null);

        if (response.Data != null)
        {
            paginatedResponse.PageNumber = response.Data.PageNumber;
            paginatedResponse.TotalPages = response.Data.TotalPages;
            paginatedResponse.TotalCount = response.Data.TotalCount;
        }

        return paginatedResponse;
    }
}
