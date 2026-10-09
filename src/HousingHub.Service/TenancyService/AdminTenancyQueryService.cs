using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Dtos.Tenancy;
using HousingHub.Service.TenancyService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.TenancyService;

/// <inheritdoc cref="IAdminTenancyQueryService"/>
public class AdminTenancyQueryService : IAdminTenancyQueryService
{
    private const string TenancyIndex = "TenancyId-index";

    /// <summary>
    /// How long a staff document link lives.
    /// </summary>
    /// <remarks>
    /// The same fifteen minutes the parties get. Staff have no stronger claim on a
    /// tenant's employment letter than the people whose tenancy it is.
    /// </remarks>
    private static readonly TimeSpan DocumentLinkLifetime = TimeSpan.FromMinutes(15);

    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<AdminTenancyQueryService> _logger;

    public AdminTenancyQueryService(
        IUnitOfWOrk unitOfWork,
        IFileStorageService fileStorage,
        ILogger<AdminTenancyQueryService> logger)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<BaseResponse<PaginatedResult<AdminTenancyDto>>> GetTenanciesAsync(
        int pageNumber, int pageSize, TenancyStatus? status = null)
    {
        try
        {
            // A full read, then paged in memory — the same shape as the admin payment
            // queue and for the same reason: there is no status index to push the
            // filter down to, and DynamoDB's cursor paging gives no page count. Fine
            // at this volume; a date-bucketed index and a cursor API is the answer if
            // the table grows past a few thousand rows.
            var all = await _unitOfWork.TenancyQueries.GetAllAsync();

            var filtered = all
                .Where(t => status is null || t.Status == status.Value)
                .OrderByDescending(t => t.DateCreated)
                .ToList();

            var page = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var dtos = await ToDtosAsync(page);

            return Ok(new PaginatedResult<AdminTenancyDto>(dtos, filtered.Count, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing tenancies for admin");
            return Fail<PaginatedResult<AdminTenancyDto>>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<AdminTenancyDetailDto>> GetAsync(Guid tenancyId)
    {
        try
        {
            var tenancy = await _unitOfWork.TenancyQueries.GetByIdAsync(tenancyId);
            if (tenancy is null)
                return Fail<AdminTenancyDetailDto>(ResponseMessages.SetNotFoundMessage("tenancy"));

            var summaries = await ToDtosAsync([tenancy]);
            var documents = await LoadDocumentsAsync(tenancyId);
            var fees = await _unitOfWork.TenancyFeeQueries.QueryByIndexAsync(TenancyIndex, tenancyId);

            return Ok(new AdminTenancyDetailDto(
                summaries[0],
                documents.Select(ToDto).ToList(),
                fees.OrderBy(f => f.DateCreated)
                    .Select(f => new TenancyFeeDto(f.Id, f.Name, f.Description, f.AmountKobo))
                    .ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading tenancy {TenancyId} for admin", tenancyId);
            return Fail<AdminTenancyDetailDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<string>> GetDocumentUrlAsync(
        Guid tenancyId, Guid documentId, TenancyDocumentFile file)
    {
        try
        {
            var document = (await LoadDocumentsAsync(tenancyId)).FirstOrDefault(d => d.Id == documentId);

            var key = file switch
            {
                TenancyDocumentFile.Submitted => document?.SubmittedFileKey,
                TenancyDocumentFile.Signed => document?.SignedPdfKey,
                _ => document?.SourceFileKey,
            };

            if (document is null || string.IsNullOrWhiteSpace(key))
                return Fail<string>(ResponseMessages.SetNotFoundMessage("document"));

            // Logged at information, not debug. Reading somebody's tenancy paperwork
            // is a thing we should be able to account for afterwards.
            _logger.LogInformation(
                "Admin opened the {Which} file of document {DocumentId} on tenancy {TenancyId}",
                file, documentId, tenancyId);

            var url = await _fileStorage.GetPresignedUrlAsync(key, DocumentLinkLifetime);
            return Ok(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building document link for tenancy {TenancyId}", tenancyId);
            return Fail<string>(ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// Maps tenancies and attaches both parties, the property and the money.
    /// </summary>
    /// <remarks>
    /// One batched read per related table rather than one read per row inside a
    /// loop. A page of twenty tenancies costs four round trips, not eighty — and
    /// landlord and tenant ids are resolved through a single customer read, because
    /// in this system the same person is routinely both.
    /// </remarks>
    private async Task<List<AdminTenancyDto>> ToDtosAsync(IReadOnlyList<Tenancy> tenancies)
    {
        if (tenancies.Count == 0) return [];

        var customerIds = tenancies
            .SelectMany(t => new[] { t.LandlordCustomerId, t.TenantCustomerId })
            .Distinct()
            .ToList();

        var customers = await _unitOfWork.CustomerQueries.GetManyByAsync(c => c.Id, customerIds);
        var customersById = customers.ToDictionary(c => c.Id);

        var properties = await _unitOfWork.PropertyQueries.GetManyByAsync(
            p => p.Id, tenancies.Select(t => t.PropertyId).Distinct());
        var propertiesById = properties.ToDictionary(p => p.Id);

        var documentsByTenancy = new Dictionary<Guid, List<TenancyDocument>>();
        var feesByTenancy = new Dictionary<Guid, long>();

        foreach (var tenancy in tenancies)
        {
            documentsByTenancy[tenancy.Id] = (await LoadDocumentsAsync(tenancy.Id)).ToList();
            var fees = await _unitOfWork.TenancyFeeQueries.QueryByIndexAsync(TenancyIndex, tenancy.Id);
            feesByTenancy[tenancy.Id] = fees.Sum(f => f.AmountKobo);
        }

        return tenancies.Select(t =>
        {
            customersById.TryGetValue(t.LandlordCustomerId, out var landlord);
            customersById.TryGetValue(t.TenantCustomerId, out var tenant);
            propertiesById.TryGetValue(t.PropertyId, out var property);

            var documents = documentsByTenancy[t.Id];
            var feesKobo = feesByTenancy[t.Id];

            return new AdminTenancyDto(
                t.Id,
                t.PropertyId,
                property?.Title,
                t.LandlordCustomerId,
                FullName(landlord),
                landlord?.Email,
                t.TenantCustomerId,
                FullName(tenant),
                tenant?.Email,
                t.Status,
                t.LeaseType,
                t.AgreedRentKobo,
                feesKobo,
                t.AgreedRentKobo + feesKobo,
                documents.Count,
                documents.Count(d => d.Status == TenancyDocumentStatus.Accepted),
                documents.Count(d => d.IsWithTenant),
                documents.Count(d => d.Status == TenancyDocumentStatus.Submitted),
                t.SelectedAt,
                t.WithdrawnReason,
                t.ClosedAt,
                t.DateCreated);
        }).ToList();
    }

    private async Task<IReadOnlyList<TenancyDocument>> LoadDocumentsAsync(Guid tenancyId)
    {
        var documents = await _unitOfWork.TenancyDocumentQueries.QueryByIndexAsync(TenancyIndex, tenancyId);
        return documents.OrderBy(d => d.DateCreated).ToList();
    }

    private static string? FullName(Customer? customer) =>
        customer is null ? null : $"{customer.FirstName} {customer.LastName}".Trim();

    private static AdminTenancyDocumentDto ToDto(TenancyDocument d) => new(
        d.Id,
        d.TenancyId,
        d.Name,
        d.Instructions,
        d.Mode,
        d.IsAgreement,
        d.Status,
        !string.IsNullOrWhiteSpace(d.SourceFileKey),
        !string.IsNullOrWhiteSpace(d.SubmittedFileKey),
        !string.IsNullOrWhiteSpace(d.SignedPdfKey),
        d.SubmittedAt,
        d.ReviewedAt,
        d.RejectionReason,
        d.SignedAt,
        d.SignerIpAddress,
        d.SignerUserAgent,
        d.SourceFileHash,
        d.SignedDocumentHash,
        d.DateCreated);

    private static BaseResponse<T> Ok<T>(T data) =>
        new(data, true, string.Empty, ResponseMessages.Successful);

    private static BaseResponse<T> Fail<T>(string message) =>
        new(default, false, string.Empty, message);
}
