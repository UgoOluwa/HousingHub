using HousingHub.Application.Commons.Bases;
using HousingHub.Service.PropertyFileService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Commands.DeleteFile;

public class DeletePropertyFileCommandHandler : IRequestHandler<DeletePropertyFileCommand, BaseResponse<bool>>
{
    private readonly IPropertyFileCommandService _propertyFileCommandService;

    public DeletePropertyFileCommandHandler(IPropertyFileCommandService propertyFileCommandService)
    {
        _propertyFileCommandService = propertyFileCommandService;
    }

    public async Task<BaseResponse<bool>> Handle(DeletePropertyFileCommand request, CancellationToken cancellationToken)
    {
        var response = await _propertyFileCommandService.DeletePropertyFile(request.FileId, request.AuthenticatedUserId);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
