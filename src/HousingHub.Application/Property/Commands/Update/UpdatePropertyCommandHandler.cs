using AutoMapper;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Commands.Update;

public class UpdatePropertyCommandHandler : IRequestHandler<UpdatePropertyCommand, BaseResponse<PropertyDto?>>
{
    private readonly IPropertyCommandService _propertyCommandService;
    private readonly IMapper _mapper;

    public UpdatePropertyCommandHandler(IPropertyCommandService propertyCommandService, IMapper mapper)
    {
        _propertyCommandService = propertyCommandService;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PropertyDto?>> Handle(UpdatePropertyCommand request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<UpdatePropertyDto>(request);
        var response = await _propertyCommandService.UpdateProperty(dto, request.AuthenticatedUserId);
        return new BaseResponse<PropertyDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
