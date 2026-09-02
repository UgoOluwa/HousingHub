using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Payment;

namespace HousingHub.Service.PaymentService.Interfaces;

public interface IAdminPaymentQueryService
{
    /// <summary>Payments, newest first, optionally narrowed to one status.</summary>
    Task<BaseResponse<PaginatedResult<AdminPaymentDto>>> GetPaymentsAsync(
        int pageNumber, int pageSize, PaymentStatus? status = null);

    /// <summary>
    /// Payments that need a person, newest first.
    /// </summary>
    /// <remarks>
    /// Its own method rather than a status filter, because it reads a sparse index
    /// instead of scanning — and because it is the only one of these queues where
    /// not looking has a cost to a customer.
    /// </remarks>
    Task<BaseResponse<List<AdminPaymentDto>>> GetFlaggedAsync();

    /// <summary>How many payments are waiting on a person. For a badge.</summary>
    Task<BaseResponse<int>> GetFlaggedCountAsync();

    /// <summary>One payment, by our reference.</summary>
    Task<BaseResponse<AdminPaymentDto>> GetByReferenceAsync(string reference);
}
