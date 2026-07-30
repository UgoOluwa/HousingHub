using HousingHub.Application.Commons.Bases;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Commands.PublishProperty;

public class PublishPropertyCommandHandler : IRequestHandler<PublishPropertyCommand, BaseResponse<bool>>
{
    private readonly IPropertyCommandService _propertyCommandService;

    public PublishPropertyCommandHandler(IPropertyCommandService propertyCommandService)
    {
        _propertyCommandService = propertyCommandService;
    }

    public async Task<BaseResponse<bool>> Handle(PublishPropertyCommand request, CancellationToken cancellationToken)
    {
        var response = await _propertyCommandService.SetPropertyPublishedAsync(request.PropertyId, request.IsPublished);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
