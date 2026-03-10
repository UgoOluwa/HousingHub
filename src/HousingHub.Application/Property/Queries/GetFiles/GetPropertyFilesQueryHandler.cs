using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Dtos.PropertyFile;
using HousingHub.Service.PropertyFileService.Interfaces;
using MediatR;

namespace HousingHub.Application.Property.Queries.GetFiles;

public class GetPropertyFilesQueryHandler : IRequestHandler<GetPropertyFilesQuery, BaseResponse<List<PropertyFileDto>?>>
{
    private readonly IPropertyFileQueryService _propertyFileQueryService;

    public GetPropertyFilesQueryHandler(IPropertyFileQueryService propertyFileQueryService)
    {
        _propertyFileQueryService = propertyFileQueryService;
    }

    public async Task<BaseResponse<List<PropertyFileDto>?>> Handle(GetPropertyFilesQuery request, CancellationToken cancellationToken)
    {
        var response = await _propertyFileQueryService.GetAllPropertyFilesAsync(request.PropertyId);
        return new BaseResponse<List<PropertyFileDto>?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
