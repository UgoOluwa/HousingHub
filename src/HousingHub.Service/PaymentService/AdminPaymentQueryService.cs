using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Payment;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.PaymentService;

/// <summary>
/// Read-only views of payments for staff.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately query-only. Nothing here can settle, refund or unflag a payment —
/// an admin endpoint that could mark a payment successful would be a way to hand
/// out paid services without any money moving, and it would be indistinguishable
/// in the data from a real settlement.
/// </para>
/// <para>
/// Resolving a flagged payment therefore means going to Paystack's dashboard, which
/// is the right first answer: the question is always "did this money actually
/// arrive", and the provider is the only thing that can answer it.
/// </para>
/// </remarks>
public class AdminPaymentQueryService : IAdminPaymentQueryService
{
    private const string ReferenceIndex = "Reference-index";
    private const string FlagWatchIndex = "FlagWatch-index";

    private readonly IUnitOfWOrk _unitOfWork;
    private readonly ILogger<AdminPaymentQueryService> _logger;

    public AdminPaymentQueryService(IUnitOfWOrk unitOfWork, ILogger<AdminPaymentQueryService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<BaseResponse<PaginatedResult<AdminPaymentDto>>> GetPaymentsAsync(
        int pageNumber, int pageSize, PaymentStatus? status = null)
    {
        try
        {
            // A full read, then paged in memory. Payments have no status index beyond
            // the sparse flagged one, so narrowing by any other status cannot be
            // pushed down — and DynamoDB paging is cursor-based, which does not give
            // a page count. Fine at this volume; if the table grows past a few
            // thousand rows this wants a date-bucketed index and a cursor API.
            var all = await _unitOfWork.PaymentQueries.GetAllAsync();

            var filtered = all
                .Where(p => status is null || p.Status == status.Value)
                .OrderByDescending(p => p.DateCreated)
                .ToList();

            var page = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var dtos = await ToDtosAsync(page);

            return Ok(new PaginatedResult<AdminPaymentDto>(dtos, filtered.Count, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing payments for admin");
            return Fail<PaginatedResult<AdminPaymentDto>>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<List<AdminPaymentDto>>> GetFlaggedAsync()
    {
        try
        {
            var flagged = await _unitOfWork.PaymentQueries.QueryByIndexAsync(
                FlagWatchIndex, Payment.FlaggedMarker);

            var ordered = flagged.OrderByDescending(p => p.DateCreated).ToList();

            return Ok(await ToDtosAsync(ordered));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing flagged payments");
            return new BaseResponse<List<AdminPaymentDto>>(
                new List<AdminPaymentDto>(), false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<int>> GetFlaggedCountAsync()
    {
        try
        {
            var flagged = await _unitOfWork.PaymentQueries.QueryByIndexAsync(
                FlagWatchIndex, Payment.FlaggedMarker);

            return Ok(flagged.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting flagged payments");
            // Zero would read as "nothing needs attention", which is the one thing
            // this must not say when it does not know.
            return new BaseResponse<int>(0, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<AdminPaymentDto>> GetByReferenceAsync(string reference)
    {
        try
        {
            var matches = await _unitOfWork.PaymentQueries.QueryByIndexAsync(ReferenceIndex, reference);
            var payment = matches.FirstOrDefault();

            if (payment is null)
                return Fail<AdminPaymentDto>(ResponseMessages.SetNotFoundMessage("payment"));

            var dtos = await ToDtosAsync([payment]);
            return Ok(dtos[0]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading payment {Reference} for admin", reference);
            return Fail<AdminPaymentDto>(ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// Maps payments and attaches each payer's name and email.
    /// </summary>
    /// <remarks>
    /// One indexed read per distinct customer, issued together, rather than one per
    /// row inside a loop. A page of twenty payments by twenty payers costs twenty
    /// parallel GetItems; the same page by one payer costs one.
    /// </remarks>
    private async Task<List<AdminPaymentDto>> ToDtosAsync(IReadOnlyList<Payment> payments)
    {
        if (payments.Count == 0) return [];

        var customerIds = payments.Select(p => p.CustomerId).Distinct().ToList();
        var customers = await _unitOfWork.CustomerQueries.GetManyByAsync(c => c.Id, customerIds);
        var byId = customers.ToDictionary(c => c.Id);

        return payments.Select(p =>
        {
            byId.TryGetValue(p.CustomerId, out var customer);

            return new AdminPaymentDto(
                p.Id,
                p.Reference,
                p.Purpose,
                p.SubjectId,
                p.AmountKobo,
                p.PurposeFeeKobo,
                p.IdentityFeeKobo,
                p.IncludesIdentityVerification,
                p.Currency,
                p.Status,
                p.Provider,
                p.ProviderReference,
                p.Channel,
                p.PaidAt,
                p.DateCreated,
                p.CustomerId,
                customer is null ? null : $"{customer.FirstName} {customer.LastName}".Trim(),
                customer?.Email,
                p.FailureReason,
                p.FlagNote);
        }).ToList();
    }

    private static BaseResponse<T> Ok<T>(T data) =>
        new(data, true, string.Empty, ResponseMessages.Successful);

    private static BaseResponse<T> Fail<T>(string message) =>
        new(default, false, string.Empty, message);
}
