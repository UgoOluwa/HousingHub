using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Tenancy;

namespace HousingHub.Service.TenancyService.Interfaces;

/// <summary>
/// Read-only views of tenancies for staff.
/// </summary>
/// <remarks>
/// Query-only, and that is the whole design. An admin endpoint that could accept a
/// document, set a fee or advance a tenancy would be a way to manufacture an
/// executed agreement that neither party agreed to, and the row it wrote would look
/// exactly like the real thing. Staff see everything and change nothing.
/// </remarks>
public interface IAdminTenancyQueryService
{
    /// <summary>Tenancies, newest first, optionally narrowed to one status.</summary>
    Task<BaseResponse<PaginatedResult<AdminTenancyDto>>> GetTenanciesAsync(
        int pageNumber, int pageSize, TenancyStatus? status = null);

    /// <summary>One tenancy with its documents, signature trail and fees.</summary>
    Task<BaseResponse<AdminTenancyDetailDto>> GetAsync(Guid tenancyId);

    /// <summary>
    /// A short-lived link to one document's file.
    /// </summary>
    /// <remarks>
    /// Restricted to SuperAdmin at the controller. These are a tenant's personal
    /// papers — an employment letter, a reference, a signed agreement — and the
    /// reason to open one is a dispute, not curiosity.
    /// </remarks>
    Task<BaseResponse<string>> GetDocumentUrlAsync(
        Guid tenancyId, Guid documentId, TenancyDocumentFile file);
}
