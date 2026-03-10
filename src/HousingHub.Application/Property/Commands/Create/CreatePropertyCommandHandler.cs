using AutoMapper;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Commands.Create;

public class CreatePropertyCommandHandler : IRequestHandler<CreatePropertyCommand, BaseResponse<PropertyDto?>>
{
    private readonly IPropertyCommandService _propertyCommandService;
    private readonly IMapper _mapper;

    public CreatePropertyCommandHandler(IPropertyCommandService propertyCommandService, IMapper mapper)
    {
        _propertyCommandService = propertyCommandService;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PropertyDto?>> Handle(CreatePropertyCommand request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<CreatePropertyDto>(request);
        var response = await _propertyCommandService.CreateProperty(dto, request.OwnerId);
        return new BaseResponse<PropertyDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
