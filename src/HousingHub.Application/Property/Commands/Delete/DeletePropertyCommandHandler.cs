using HousingHub.Application.Commons.Bases;
using HousingHub.Service.PropertyService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Commands.Delete;

public class DeletePropertyCommandHandler : IRequestHandler<DeletePropertyCommand, BaseResponse<bool>>
{
    private readonly IPropertyCommandService _propertyCommandService;

    public DeletePropertyCommandHandler(IPropertyCommandService propertyCommandService)
    {
        _propertyCommandService = propertyCommandService;
    }

    public async Task<BaseResponse<bool>> Handle(DeletePropertyCommand request, CancellationToken cancellationToken)
    {
        var response = await _propertyCommandService.DeleteProperty(request.PropertyId, request.AuthenticatedUserId);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
