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
        if (request.CustomerId == Guid.Empty)
            return new BaseResponse<string>(false, null, Core.CustomResponses.ResponseMessages.InvalidCustomerId, null);

        // Government identity documents. Previously this path had no validation at
        // all and wrote to the same public bucket prefix as property photos, so the
        // documents were world-readable at a predictable URL shape.
        var validation = UploadedFileValidator.Validate(
            request.File,
            UploadedFileValidator.DocumentExtensions,
            UploadedFileValidator.DocumentMaxBytes);

        if (!validation.IsValid)
            return new BaseResponse<string>(false, null, validation.Error!, null);

        // Returns an object key, not a URL. The object lives under the private prefix
        // and is only ever reachable through a short-lived presigned URL minted for an
        // authorised reader.
        var key = await _fileStorageService.UploadPrivateFileAsync(
            request.File!, $"kyc/{request.CustomerId}", validation.ContentType);

        return new BaseResponse<string>(true, key, Core.CustomResponses.ResponseMessages.KycDocumentUploaded, null);
    }
}
