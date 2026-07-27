using HousingHub.Application.Commons.Bases;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Commands.SetPublished;

public class SetPropertyPublishedCommandHandler : IRequestHandler<SetPropertyPublishedCommand, BaseResponse<bool>>
{
    private readonly IPropertyCommandService _propertyCommandService;

    public SetPropertyPublishedCommandHandler(IPropertyCommandService propertyCommandService)
    {
        _propertyCommandService = propertyCommandService;
    }

    public async Task<BaseResponse<bool>> Handle(SetPropertyPublishedCommand request, CancellationToken cancellationToken)
    {
        var response = await _propertyCommandService.SetOwnPropertyPublishedAsync(
            request.PropertyId, request.AuthenticatedUserId, request.IsPublished);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
