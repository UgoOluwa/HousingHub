using HousingHub.Application.Commons.Bases;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace HousingHub.Application.Customer.Commands.UploadKycDocument;

public record UploadKycDocumentCommand(Guid CustomerId, IFormFile File) : IRequest<BaseResponse<string>>;
