using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.PropertyFile;
using HousingHub.Service.PropertyFileService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Commands.UploadFiles;

public class UploadPropertyFilesCommandHandler : IRequestHandler<UploadPropertyFilesCommand, BaseResponse<List<PropertyFileDto>?>>
{
    private readonly IPropertyFileCommandService _propertyFileCommandService;

    public UploadPropertyFilesCommandHandler(IPropertyFileCommandService propertyFileCommandService)
    {
        _propertyFileCommandService = propertyFileCommandService;
    }

    public async Task<BaseResponse<List<PropertyFileDto>?>> Handle(UploadPropertyFilesCommand request, CancellationToken cancellationToken)
    {
        var response = await _propertyFileCommandService.UploadPropertyFiles(request.PropertyId, request.AuthenticatedUserId, request.Files);
        return new BaseResponse<List<PropertyFileDto>?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
