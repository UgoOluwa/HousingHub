using System.Security.Cryptography;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.Dtos.Tenancy;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.TenancyService.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.TenancyService;

/// <summary>
/// The paperwork between choosing a tenant and taking their money.
/// </summary>
/// <remarks>
/// <para>
/// The owner composes a request — the compulsory agreement plus whatever else they
/// want, each marked upload, sign-in-app or sign-offline — together with the fees on
/// top of the rent. They send it once. The tenant completes each item, the owner
/// accepts or sends it back with a reason, and the loop runs until everything is
/// accepted.
/// </para>
/// <para>
/// Compose-then-send rather than send-as-you-go, mirroring the verification pipeline:
/// a half-built request arriving in somebody's inbox is worse than no request, and a
/// set of documents is a thing a person reviews as a whole.
/// </para>
/// <para>
/// <b>Fees are visible from the moment the request is sent.</b> A cost that first
/// appears at the payment step is a surprise at the point where deals die.
/// </para>
/// </remarks>
public class TenancyDocumentService : ITenancyDocumentService
{
    private const string TenancyIndex = "TenancyId-index";

    /// <summary>
    /// How long a document link lives.
    /// </summary>
    /// <remarks>
    /// Long enough to read a tenancy agreement properly, short enough that a link
    /// pasted somewhere is not a standing grant. The URL is a credential, not an
    /// address.
    /// </remarks>
    private static readonly TimeSpan DocumentLinkLifetime = TimeSpan.FromMinutes(15);

    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly IEmailService _emailService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<TenancyDocumentService> _logger;

    public TenancyDocumentService(
        IUnitOfWOrk unitOfWork,
        IFileStorageService fileStorage,
        IEmailService emailService,
        IRealtimeNotifier realtimeNotifier,
        ILogger<TenancyDocumentService> logger)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _emailService = emailService;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task<BaseResponse<TenancyDocumentPackDto>> GetPackAsync(Guid tenancyId, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await LoadForEitherPartyAsync(tenancyId, authenticatedUserId);
            if (tenancy is null) return FailPack();

            return new BaseResponse<TenancyDocumentPackDto>(
                await BuildPackAsync(tenancy), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading document pack for tenancy {TenancyId}", tenancyId);
            return FailPack(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<TenancyDocumentDto>> AddDocumentAsync(
        Guid tenancyId, AddTenancyDocumentDto request, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await LoadForOwnerAsync(tenancyId, authenticatedUserId);
            if (tenancy is null) return FailDoc(ResponseMessages.SetNotFoundMessage("tenancy"));

            if (tenancy.Status != TenancyStatus.CandidateSelected)
                return FailDoc(ResponseMessages.TenancyNotComposing);

            if (string.IsNullOrWhiteSpace(request.Name))
                return FailDoc(ResponseMessages.SetCreationFailureMessage("document"));

            var needsSourceFile = request.Mode is TenancyDocumentMode.SignInApp or TenancyDocumentMode.SignOffline;

            if (needsSourceFile && request.File is null)
                return FailDoc(ResponseMessages.TenancySignDocumentMissingFile);

            // Refused rather than ignored. An owner who attached a file to an upload
            // request has misunderstood which way the document is travelling, and
            // silently dropping it would leave them thinking the tenant will see it.
            if (!needsSourceFile && request.File is not null)
                return FailDoc(ResponseMessages.TenancyUploadDocumentHasFile);

            // Nigeria's Evidence Act excludes land instruments from electronic
            // signature. Rent is a short tenancy and may be e-signed; a longer lease
            // or a sale has to be executed on paper, so the offline mode is the only
            // one offered. Refused here rather than at signing, because by then the
            // tenant has been told they can sign in the app.
            if (request.Mode == TenancyDocumentMode.SignInApp && tenancy.LeaseType != PropertyLeaseType.Rent)
                return FailDoc(ResponseMessages.TenancyCannotESignThisLease);

            var existing = await LoadDocumentsAsync(tenancyId);

            if (request.IsAgreement && existing.Any(d => d.IsAgreement))
                return FailDoc(ResponseMessages.TenancyOneAgreementOnly);

            string? fileKey = null;
            string? fileHash = null;

            if (request.File is not null)
            {
                var validation = UploadedFileValidator.Validate(
                    request.File, UploadedFileValidator.DocumentExtensions, UploadedFileValidator.DocumentMaxBytes);

                if (!validation.IsValid)
                    return FailDoc(validation.Error!);

                // Hashed from the bytes we are about to store, before storing them.
                // This is what a signature later attests to.
                fileHash = await ComputeHashAsync(request.File);

                fileKey = await _fileStorage.UploadPrivateFileAsync(
                    request.File, $"tenancies/{tenancyId}/source", validation.ContentType);
            }

            var document = new TenancyDocument(
                tenancyId,
                request.Name.Trim(),
                request.Mode,
                request.IsAgreement,
                request.Instructions?.Trim(),
                fileKey,
                fileHash);

            if (!await _unitOfWork.TenancyDocumentCommands.InsertAsync(document))
                return FailDoc(ResponseMessages.SetCreationFailureMessage("document"));

            await _unitOfWork.SaveAsync();

            return OkDoc(document, ResponseMessages.SetCreationSuccessMessage("document"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding document to tenancy {TenancyId}", tenancyId);
            return FailDoc(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> RemoveDocumentAsync(
        Guid tenancyId, Guid documentId, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await LoadForOwnerAsync(tenancyId, authenticatedUserId);
            if (tenancy is null) return FailBool(ResponseMessages.SetNotFoundMessage("tenancy"));

            if (tenancy.Status != TenancyStatus.CandidateSelected)
                return FailBool(ResponseMessages.TenancyNotComposing);

            var document = (await LoadDocumentsAsync(tenancyId)).FirstOrDefault(d => d.Id == documentId);
            if (document is null) return FailBool(ResponseMessages.TenancyDocumentNotYours);

            if (!string.IsNullOrWhiteSpace(document.SourceFileKey))
                await _fileStorage.DeleteFileAsync(document.SourceFileKey);

            await _unitOfWork.TenancyDocumentCommands.DeleteAsync(document);
            await _unitOfWork.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.SetDeletedSuccessMessage("document"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing document {DocumentId}", documentId);
            return FailBool(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<IReadOnlyList<TenancyFeeDto>>> SetFeesAsync(
        Guid tenancyId, SetTenancyFeesDto request, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await LoadForOwnerAsync(tenancyId, authenticatedUserId);
            if (tenancy is null)
                return new BaseResponse<IReadOnlyList<TenancyFeeDto>>(
                    [], false, string.Empty, ResponseMessages.SetNotFoundMessage("tenancy"));

            if (tenancy.Status != TenancyStatus.CandidateSelected)
                return new BaseResponse<IReadOnlyList<TenancyFeeDto>>(
                    [], false, string.Empty, ResponseMessages.TenancyNotComposing);

            // Replaced wholesale rather than merged. The owner is editing a list in
            // front of them, and a merge would quietly keep a fee they had deleted.
            var existing = await _unitOfWork.TenancyFeeQueries.QueryByIndexAsync(TenancyIndex, tenancyId);
            foreach (var fee in existing)
                await _unitOfWork.TenancyFeeCommands.DeleteAsync(fee);

            var created = new List<TenancyFee>();
            foreach (var input in request.Fees ?? [])
            {
                if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Description))
                    continue;
                if (input.AmountKobo <= 0) continue;

                var fee = new TenancyFee(tenancyId, input.Name.Trim(), input.Description.Trim(), input.AmountKobo);
                await _unitOfWork.TenancyFeeCommands.InsertAsync(fee);
                created.Add(fee);
            }

            await _unitOfWork.SaveAsync();

            return new BaseResponse<IReadOnlyList<TenancyFeeDto>>(
                created.Select(ToDto).ToList(), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting fees on tenancy {TenancyId}", tenancyId);
            return new BaseResponse<IReadOnlyList<TenancyFeeDto>>(
                [], false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<TenancyDocumentPackDto>> SendRequestAsync(Guid tenancyId, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await LoadForOwnerAsync(tenancyId, authenticatedUserId);
            if (tenancy is null) return FailPack();

            if (tenancy.Status != TenancyStatus.CandidateSelected)
                return FailPack(ResponseMessages.TenancyNotComposing);

            var documents = await LoadDocumentsAsync(tenancyId);

            if (documents.Count == 0)
                return FailPack(ResponseMessages.TenancyNoDocumentsRequested);

            // Every let has an agreement. Checked at send rather than at add, because
            // an owner composing a request may reasonably add the extras first.
            if (!documents.Any(d => d.IsAgreement))
                return FailPack(ResponseMessages.TenancyAgreementRequired);

            tenancy.Status = TenancyStatus.DocumentsRequested;
            tenancy.DateModified = DateTime.UtcNow;
            await _unitOfWork.TenancyCommands.UpdateAsync(tenancy);
            await _unitOfWork.SaveAsync();

            await NotifyDocumentsRequestedAsync(tenancy, documents.Count);

            return new BaseResponse<TenancyDocumentPackDto>(
                await BuildPackAsync(tenancy), true, string.Empty, ResponseMessages.TenancyDocumentsSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending document request for tenancy {TenancyId}", tenancyId);
            return FailPack(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<TenancyDocumentDto>> SubmitDocumentAsync(
        Guid tenancyId, Guid documentId, IFormFile file, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await LoadForTenantAsync(tenancyId, authenticatedUserId);
            if (tenancy is null) return FailDoc(ResponseMessages.SetNotFoundMessage("tenancy"));

            var document = (await LoadDocumentsAsync(tenancyId)).FirstOrDefault(d => d.Id == documentId);
            if (document is null) return FailDoc(ResponseMessages.TenancyDocumentNotYours);

            if (!document.IsWithTenant) return FailDoc(ResponseMessages.TenancyDocumentNotAwaitingYou);

            var validation = UploadedFileValidator.Validate(
                file, UploadedFileValidator.DocumentExtensions, UploadedFileValidator.DocumentMaxBytes);

            if (!validation.IsValid) return FailDoc(validation.Error!);

            var key = await _fileStorage.UploadPrivateFileAsync(
                file, $"tenancies/{tenancyId}/submitted", validation.ContentType);

            if (!document.TrySubmitFile(key))
                return FailDoc(ResponseMessages.TenancyDocumentNotAwaitingYou);

            await _unitOfWork.TenancyDocumentCommands.UpdateAsync(document);
            await _unitOfWork.SaveAsync();

            await NotifyAsync(
                tenancy.LandlordCustomerId, tenancy.Id,
                NotificationType.TenancyDocumentSubmitted,
                "A document is ready to review",
                $"\"{document.Name}\" has been sent back to you.");

            return OkDoc(document, ResponseMessages.TenancyDocumentSubmitted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting document {DocumentId}", documentId);
            return FailDoc(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<TenancyDocumentDto>> SignDocumentAsync(
        Guid tenancyId, Guid documentId, Guid authenticatedUserId, string? ipAddress, string? userAgent)
    {
        try
        {
            var tenancy = await LoadForTenantAsync(tenancyId, authenticatedUserId);
            if (tenancy is null) return FailDoc(ResponseMessages.SetNotFoundMessage("tenancy"));

            var document = (await LoadDocumentsAsync(tenancyId)).FirstOrDefault(d => d.Id == documentId);
            if (document is null) return FailDoc(ResponseMessages.TenancyDocumentNotYours);

            if (!document.IsWithTenant) return FailDoc(ResponseMessages.TenancyDocumentNotAwaitingYou);

            // Belt and braces with the check at add time: the lease type is
            // snapshotted on the tenancy and cannot change, but a document added
            // before any such check existed must not become signable by accident.
            if (tenancy.LeaseType != PropertyLeaseType.Rent)
                return FailDoc(ResponseMessages.TenancyCannotESignThisLease);

            if (string.IsNullOrWhiteSpace(document.SourceFileHash))
                return FailDoc(ResponseMessages.TenancySignDocumentMissingFile);

            if (!document.TrySign(document.SourceFileHash, ipAddress, userAgent))
                return FailDoc(ResponseMessages.TenancyDocumentNotAwaitingYou);

            await _unitOfWork.TenancyDocumentCommands.UpdateAsync(document);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation(
                "Tenancy document {DocumentId} signed by {CustomerId} against hash {Hash}",
                document.Id, authenticatedUserId, document.SignedDocumentHash);

            await NotifyAsync(
                tenancy.LandlordCustomerId, tenancy.Id,
                NotificationType.TenancyDocumentSubmitted,
                "A document has been signed",
                $"\"{document.Name}\" has been signed and is ready for you to review.");

            return OkDoc(document, ResponseMessages.TenancyDocumentSubmitted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing document {DocumentId}", documentId);
            return FailDoc(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<TenancyDocumentDto>> ReviewDocumentAsync(
        Guid tenancyId, Guid documentId, ReviewTenancyDocumentDto request, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await LoadForOwnerAsync(tenancyId, authenticatedUserId);
            if (tenancy is null) return FailDoc(ResponseMessages.SetNotFoundMessage("tenancy"));

            var documents = await LoadDocumentsAsync(tenancyId);
            var document = documents.FirstOrDefault(d => d.Id == documentId);
            if (document is null) return FailDoc(ResponseMessages.TenancyDocumentNotYours);

            if (document.Status != TenancyDocumentStatus.Submitted)
                return FailDoc(ResponseMessages.TenancyDocumentNotAwaitingReview);

            if (request.Accept)
            {
                if (!document.TryAccept()) return FailDoc(ResponseMessages.TenancyDocumentNotAwaitingReview);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                    return FailDoc(ResponseMessages.TenancyRejectionReasonRequired);

                if (!document.TryReject(request.RejectionReason))
                    return FailDoc(ResponseMessages.TenancyDocumentNotAwaitingReview);
            }

            await _unitOfWork.TenancyDocumentCommands.UpdateAsync(document);

            // Every document settled moves the whole tenancy on. Recomputed from the
            // set rather than counted incrementally, so a document added or removed
            // cannot leave a stale tally behind.
            bool allAccepted = documents.All(d => d.IsSettled);

            if (allAccepted && tenancy.Status == TenancyStatus.DocumentsRequested)
            {
                tenancy.Status = TenancyStatus.DocumentsAccepted;
                tenancy.DateModified = DateTime.UtcNow;
                await _unitOfWork.TenancyCommands.UpdateAsync(tenancy);
            }

            await _unitOfWork.SaveAsync();

            await NotifyAsync(
                tenancy.TenantCustomerId, tenancy.Id,
                request.Accept ? NotificationType.TenancyDocumentAccepted : NotificationType.TenancyDocumentRejected,
                request.Accept ? "A document was accepted" : "A document needs another look",
                request.Accept
                    ? $"\"{document.Name}\" has been accepted."
                    : $"\"{document.Name}\" was sent back: {document.RejectionReason}");

            if (allAccepted)
            {
                await NotifyAsync(
                    tenancy.TenantCustomerId, tenancy.Id,
                    NotificationType.TenancyDocumentsComplete,
                    "Everything's agreed",
                    "All your documents have been accepted. The next step is payment.");
            }

            return OkDoc(
                document,
                allAccepted
                    ? ResponseMessages.TenancyAllDocumentsAccepted
                    : request.Accept ? ResponseMessages.TenancyDocumentAccepted : ResponseMessages.TenancyDocumentRejected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing document {DocumentId}", documentId);
            return FailDoc(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<string>> GetDocumentUrlAsync(
        Guid tenancyId, Guid documentId, bool submitted, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await LoadForEitherPartyAsync(tenancyId, authenticatedUserId);
            if (tenancy is null)
                return new BaseResponse<string>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("tenancy"));

            var document = (await LoadDocumentsAsync(tenancyId)).FirstOrDefault(d => d.Id == documentId);
            var key = submitted ? document?.SubmittedFileKey : document?.SourceFileKey;

            if (document is null || string.IsNullOrWhiteSpace(key))
                return new BaseResponse<string>(null, false, string.Empty, ResponseMessages.TenancyDocumentNotYours);

            var url = await _fileStorage.GetPresignedUrlAsync(key, DocumentLinkLifetime);
            return new BaseResponse<string>(url, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error minting a link for document {DocumentId}", documentId);
            return new BaseResponse<string>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    // ── internals ────────────────────────────────────────────────

    /// <summary>SHA-256 of the file's bytes, as lowercase hex.</summary>
    private static async Task<string> ComputeHashAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<Tenancy?> LoadForOwnerAsync(Guid tenancyId, Guid userId)
    {
        var tenancy = await _unitOfWork.TenancyQueries.GetByIdAsync(tenancyId);
        return tenancy is not null && tenancy.LandlordCustomerId == userId ? tenancy : null;
    }

    private async Task<Tenancy?> LoadForTenantAsync(Guid tenancyId, Guid userId)
    {
        var tenancy = await _unitOfWork.TenancyQueries.GetByIdAsync(tenancyId);
        return tenancy is not null && tenancy.TenantCustomerId == userId ? tenancy : null;
    }

    private async Task<Tenancy?> LoadForEitherPartyAsync(Guid tenancyId, Guid userId)
    {
        var tenancy = await _unitOfWork.TenancyQueries.GetByIdAsync(tenancyId);
        if (tenancy is null) return null;

        return tenancy.LandlordCustomerId == userId || tenancy.TenantCustomerId == userId ? tenancy : null;
    }

    private async Task<List<TenancyDocument>> LoadDocumentsAsync(Guid tenancyId)
    {
        var documents = await _unitOfWork.TenancyDocumentQueries.QueryByIndexAsync(TenancyIndex, tenancyId);

        // Agreement first: it is the one everything else is attached to, and burying
        // it among five uploads is how somebody signs the wrong thing.
        return documents
            .OrderByDescending(d => d.IsAgreement)
            .ThenBy(d => d.DateCreated)
            .ToList();
    }

    private async Task<TenancyDocumentPackDto> BuildPackAsync(Tenancy tenancy)
    {
        var documents = await LoadDocumentsAsync(tenancy.Id);
        var fees = await _unitOfWork.TenancyFeeQueries.QueryByIndexAsync(TenancyIndex, tenancy.Id);
        var property = await _unitOfWork.PropertyQueries.GetByIdAsync(tenancy.PropertyId);

        var feeDtos = fees.OrderBy(f => f.DateCreated).Select(ToDto).ToList();

        return new TenancyDocumentPackDto(
            tenancy.Id,
            tenancy.Status,
            property?.Title,
            tenancy.AgreedRentKobo,
            documents.Select(ToDto).ToList(),
            feeDtos,
            tenancy.AgreedRentKobo + feeDtos.Sum(f => f.AmountKobo),
            documents.Count > 0 && documents.All(d => d.IsSettled));
    }

    private async Task NotifyDocumentsRequestedAsync(Tenancy tenancy, int documentCount)
    {
        await NotifyAsync(
            tenancy.TenantCustomerId, tenancy.Id,
            NotificationType.TenancyDocumentsRequested,
            "Your documents are ready",
            documentCount == 1
                ? "The owner has sent you a document to complete."
                : $"The owner has sent you {documentCount} documents to complete.");

        var tenant = await _unitOfWork.CustomerQueries.GetByIdAsync(tenancy.TenantCustomerId);
        var property = await _unitOfWork.PropertyQueries.GetByIdAsync(tenancy.PropertyId);

        if (tenant is null || string.IsNullOrWhiteSpace(tenant.Email))
        {
            _logger.LogWarning(
                "Sent documents on tenancy {TenancyId} but found no address to email", tenancy.Id);
            return;
        }

        var fees = await _unitOfWork.TenancyFeeQueries.QueryByIndexAsync(TenancyIndex, tenancy.Id);

        await _emailService.SendTenancyDocumentsRequestedAsync(
            tenant.Email,
            tenant.FirstName,
            property?.Title ?? "your property",
            documentCount,
            tenancy.AgreedRentKobo + fees.Sum(f => f.AmountKobo));
    }

    private async Task NotifyAsync(Guid recipientId, Guid subjectId, NotificationType type, string title, string body)
    {
        try
        {
            var notification = new Notification(recipientId, subjectId, type, title, body);
            await _unitOfWork.NotificationCommands.InsertAsync(notification);
            await _unitOfWork.SaveAsync();

            await _realtimeNotifier.SendNotificationAsync(
                notification.RecipientId,
                new NotificationDto(
                    notification.Id, notification.DateCreated, notification.RecipientId,
                    notification.InspectionId, notification.Type, notification.Title,
                    notification.Message, notification.IsRead));
        }
        catch (Exception ex)
        {
            // A failed notification must not undo the decision it described.
            _logger.LogError(ex, "Could not notify {RecipientId} about tenancy {SubjectId}", recipientId, subjectId);
        }
    }

    private static TenancyDocumentDto ToDto(TenancyDocument d) => new(
        d.Id, d.TenancyId, d.Name, d.Instructions, d.Mode, d.IsAgreement, d.Status,
        !string.IsNullOrWhiteSpace(d.SourceFileKey),
        !string.IsNullOrWhiteSpace(d.SubmittedFileKey),
        d.SubmittedAt, d.ReviewedAt, d.RejectionReason, d.SignedAt, d.DateCreated);

    private static TenancyFeeDto ToDto(TenancyFee f) => new(f.Id, f.Name, f.Description, f.AmountKobo);

    private static BaseResponse<TenancyDocumentDto> OkDoc(TenancyDocument d, string message) =>
        new(ToDto(d), true, string.Empty, message);

    private static BaseResponse<TenancyDocumentDto> FailDoc(string message) =>
        new(default, false, string.Empty, message);

    private static BaseResponse<bool> FailBool(string message) =>
        new(false, false, string.Empty, message);

    private static BaseResponse<TenancyDocumentPackDto> FailPack(string? message = null) =>
        new(default, false, string.Empty, message ?? ResponseMessages.SetNotFoundMessage("tenancy"));
}
