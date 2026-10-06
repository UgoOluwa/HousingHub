using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Tenancy;
using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.TenancyService.Interfaces;

public interface ITenancyDocumentService
{
    /// <summary>Everything asked for, every fee, and the total. Both parties.</summary>
    Task<BaseResponse<TenancyDocumentPackDto>> GetPackAsync(Guid tenancyId, Guid authenticatedUserId);

    /// <summary>Adds one document while the owner is still composing the request.</summary>
    Task<BaseResponse<TenancyDocumentDto>> AddDocumentAsync(
        Guid tenancyId, AddTenancyDocumentDto request, Guid authenticatedUserId);

    /// <summary>Removes one, while still composing.</summary>
    Task<BaseResponse<bool>> RemoveDocumentAsync(Guid tenancyId, Guid documentId, Guid authenticatedUserId);

    /// <summary>Replaces the fee list wholesale, while still composing.</summary>
    Task<BaseResponse<IReadOnlyList<TenancyFeeDto>>> SetFeesAsync(
        Guid tenancyId, SetTenancyFeesDto request, Guid authenticatedUserId);

    /// <summary>
    /// Sends the whole request to the tenant. Nothing can be changed afterwards.
    /// </summary>
    Task<BaseResponse<TenancyDocumentPackDto>> SendRequestAsync(Guid tenancyId, Guid authenticatedUserId);

    /// <summary>The tenant returns a file — their own, or a signed scan.</summary>
    Task<BaseResponse<TenancyDocumentDto>> SubmitDocumentAsync(
        Guid tenancyId, Guid documentId, IFormFile file, Guid authenticatedUserId);

    /// <summary>The tenant signs in the app.</summary>
    Task<BaseResponse<TenancyDocumentDto>> SignDocumentAsync(
        Guid tenancyId, Guid documentId, Guid authenticatedUserId, string? ipAddress, string? userAgent);

    /// <summary>The owner accepts or sends one back with a reason.</summary>
    Task<BaseResponse<TenancyDocumentDto>> ReviewDocumentAsync(
        Guid tenancyId, Guid documentId, ReviewTenancyDocumentDto request, Guid authenticatedUserId);

    /// <summary>
    /// A short-lived link to one of a document's files.
    /// </summary>
    /// <param name="submitted">
    /// True for what the tenant returned, false for what the owner supplied to be
    /// signed.
    /// </param>
    Task<BaseResponse<string>> GetDocumentUrlAsync(
        Guid tenancyId, Guid documentId, bool submitted, Guid authenticatedUserId);
}
