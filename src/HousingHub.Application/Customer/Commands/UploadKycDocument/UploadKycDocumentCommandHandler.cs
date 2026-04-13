using HousingHub.Application.Commons.Bases;
using HousingHub.Service.Commons.FileStorage;
using MediatR;

namespace HousingHub.Application.Customer.Commands.UploadKycDocument;

public class UploadKycDocumentCommandHandler : IRequestHandler<UploadKycDocumentCommand, BaseResponse<string>>
{
    private readonly IFileStorageService _fileStorageService;

    public UploadKycDocumentCommandHandler(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    public async Task<BaseResponse<string>> Handle(UploadKycDocumentCommand request, CancellationToken cancellationToken)
    {
        var url = await _fileStorageService.UploadFileAsync(request.File, $"kyc/{request.CustomerId}");
        return new BaseResponse<string>(true, url, "Document uploaded successfully.", null);
    }
}
