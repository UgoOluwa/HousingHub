using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Tenancy;

namespace HousingHub.Service.TenancyService.Interfaces;

public interface ITenancyService
{
    /// <summary>Everyone who completed an inspection for this property and could be chosen.</summary>
    Task<BaseResponse<List<TenancyCandidateDto>>> GetCandidatesAsync(Guid propertyId, Guid authenticatedUserId);

    /// <summary>
    /// The owner picks somebody. Creates the tenancy and puts the listing under offer.
    /// </summary>
    Task<BaseResponse<TenancyDto>> SelectCandidateAsync(
        Guid propertyId, Guid candidateCustomerId, Guid authenticatedUserId);

    /// <summary>The owner pulls out. Releases the property.</summary>
    Task<BaseResponse<TenancyDto>> WithdrawAsync(Guid tenancyId, string? reason, Guid authenticatedUserId);

    /// <summary>The candidate says they are no longer interested. Releases the property.</summary>
    Task<BaseResponse<TenancyDto>> DeclineAsync(Guid tenancyId, Guid authenticatedUserId);

    /// <summary>Everything the caller is party to, as landlord or as tenant, newest first.</summary>
    Task<BaseResponse<List<TenancyDto>>> GetMyTenanciesAsync(Guid customerId);

    /// <summary>One tenancy, readable only by its two parties.</summary>
    Task<BaseResponse<TenancyDto>> GetAsync(Guid tenancyId, Guid authenticatedUserId);
}
