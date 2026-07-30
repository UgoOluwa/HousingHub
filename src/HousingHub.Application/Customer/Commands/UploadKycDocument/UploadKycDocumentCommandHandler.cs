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
        if (request.File == null || request.File.Length == 0)
            return new BaseResponse<string>(false, null, string.Empty, "No file was provided. Please select a valid document to upload.");

        if (request.CustomerId == Guid.Empty)
            return new BaseResponse<string>(false, null, string.Empty, "Invalid customer ID.");

        var url = await _fileStorageService.UploadFileAsync(request.File, $"kyc/{request.CustomerId}");
        return new BaseResponse<string>(true, url, "Document uploaded successfully.", null);
    }
}
