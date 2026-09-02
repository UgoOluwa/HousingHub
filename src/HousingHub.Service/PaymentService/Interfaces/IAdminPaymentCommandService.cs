using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Payment;

namespace HousingHub.Service.PaymentService.Interfaces;

public interface IAdminPaymentCommandService
{
    /// <summary>
    /// Sends a payment back to whoever made it.
    /// </summary>
    /// <remarks>
    /// The only action in this system that moves money out, so it is the only one
    /// that demands a reason and records who asked. The amount refunded is what the
    /// provider says actually arrived, not what was asked for — those differ exactly
    /// when a payment was flagged, which is the commonest reason to refund at all.
    /// </remarks>
    Task<BaseResponse<AdminPaymentDto>> RefundAsync(string reference, string reason, Guid adminId);
}
