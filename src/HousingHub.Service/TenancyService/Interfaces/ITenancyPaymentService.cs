using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.Dtos.Tenancy;

namespace HousingHub.Service.TenancyService.Interfaces;

/// <summary>
/// The tenant paying to take the property.
/// </summary>
/// <remarks>
/// One transaction covering rent, the owner's fees, our commission and the
/// provider's fee. The provider splits it: the owner's share goes straight to
/// their account and never sits with us, which is what keeps this a payment rail
/// rather than a money-holding business.
/// </remarks>
public interface ITenancyPaymentService
{
    /// <summary>What it will cost, itemised. Readable by the tenant and the owner.</summary>
    Task<BaseResponse<TenancyPaymentQuoteDto>> QuoteAsync(Guid tenancyId, Guid authenticatedUserId);

    /// <summary>Starts the payment and returns where to send the tenant. The tenant only.</summary>
    Task<BaseResponse<PaymentDto>> InitialiseAsync(
        Guid tenancyId, string? callbackUrl, Guid authenticatedUserId);
}
