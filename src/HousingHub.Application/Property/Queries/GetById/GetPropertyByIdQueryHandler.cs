using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetById;

public class GetPropertyByIdQueryHandler : IRequestHandler<GetPropertyByIdQuery, BaseResponse<PropertyDto?>>
{
    private readonly IPropertyQueryService _propertyQueryService;

    public GetPropertyByIdQueryHandler(IPropertyQueryService propertyQueryService)
    {
        _propertyQueryService = propertyQueryService;
    }

    public async Task<BaseResponse<PropertyDto?>> Handle(GetPropertyByIdQuery request, CancellationToken cancellationToken)
    {
        var response = await _propertyQueryService.GetPropertyAsync(request.Id);
        return new BaseResponse<PropertyDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
