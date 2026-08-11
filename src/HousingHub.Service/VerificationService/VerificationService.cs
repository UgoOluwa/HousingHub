using System.ComponentModel;
using System.Reflection;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.Dtos.Verification;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.VerificationService.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.VerificationService;

/// <summary>
/// The generic document-verification pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Business, title and (later) identity and financial verification all run through
/// here. The type of verification changes which documents are asked for and what
/// tier approval grants — not the workflow.
/// </para>
/// <para>
/// <b>Every method takes the acting user's id and enforces ownership itself.</b>
/// Controllers pass the id from the JWT; nothing trusts a caller-supplied
/// identifier. That is the same discipline as the rest of the codebase, and it
/// matters more here than anywhere else: these documents are title deeds and
/// company records, and a leak is a regulatory event rather than a bug report.
/// </para>
/// </remarks>
public class VerificationService : IVerificationService
{
    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly IEmailService _emailService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<VerificationService> _logger;

    /// <summary>
    /// How long a document view link stays valid.
    /// </summary>
    /// <remarks>
    /// Short on purpose. The URL carries its own signature, so anyone who obtains it
    /// can read the document for as long as it lives — it is a bearer credential,
    /// not a reference. Ten minutes is enough to open and read a scan, and short
    /// enough that a link pasted into a chat is dead before it is useful.
    /// </remarks>
    private static readonly TimeSpan DocumentLinkLifetime = TimeSpan.FromMinutes(10);

    public VerificationService(
        IUnitOfWOrk unitOfWork,
        IFileStorageService fileStorage,
        IEmailService emailService,
        IRealtimeNotifier realtimeNotifier,
        ILogger<VerificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _emailService = emailService;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    // ── Submitter ───────────────────────────────────────────────

    public async Task<BaseResponse<VerificationCaseDto>> StartCaseAsync(
        Guid customerId, StartVerificationCaseDto request)
    {
        try
        {
            Guid subjectId;

            if (request.SubjectType == VerificationSubjectType.Property)
            {
                if (request.SubjectId is null || request.SubjectId == Guid.Empty)
                    return Fail<VerificationCaseDto>(ResponseMessages.VerificationSubjectRequired);

                // A title case names a property in the request body, so ownership has
                // to be proved here. Without this check anyone could open a
                // verification case against someone else's listing and, worse, attach
                // their own documents to it.
                var property = await _unitOfWork.PropertyQueries.GetByIdAsync(request.SubjectId.Value);
                if (property is null || property.OwnerId != customerId)
                    return Fail<VerificationCaseDto>(ResponseMessages.VerificationPropertyNotOwned);

                subjectId = property.Id;
            }
            else
            {
                // For business and identity the subject is always the caller. Taking
                // it from the body would let somebody open a case against another
                // person's account and have the resulting badge land on them.
                subjectId = customerId;
            }

            // Return the open draft rather than making another one. A user who
            // navigates away mid-upload and comes back should find their documents,
            // not start again beside an orphan.
            var existing = (await _unitOfWork.VerificationCaseQueries.GetAllAsync(
                    c => c.SubjectId == subjectId))
                .FirstOrDefault(c => c.SubjectType == request.SubjectType
                                     && c.Status == VerificationCaseStatus.Draft);

            if (existing is not null)
                return Ok(await ToDtoAsync(existing));

            var newCase = new VerificationCase(
                subjectId,
                request.SubjectType,
                customerId,
                VerificationRequirements.TierFor(request.SubjectType));

            await _unitOfWork.VerificationCaseCommands.InsertAsync(newCase);
            await _unitOfWork.SaveAsync();

            return Ok(await ToDtoAsync(newCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in StartCaseAsync for customer {CustomerId}", customerId);
            return Fail<VerificationCaseDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<VerificationDocumentDto>> AddDocumentAsync(
        Guid customerId, Guid caseId, AddVerificationDocumentDto request, IFormFile file)
    {
        try
        {
            var (verificationCase, error) = await LoadOwnedCaseAsync(customerId, caseId);
            if (verificationCase is null) return Fail<VerificationDocumentDto>(error!);

            if (!verificationCase.CanAcceptDocuments)
                return Fail<VerificationDocumentDto>(ResponseMessages.VerificationCaseNotEditable);

            // Magic-byte validation, not extension-trust. Reuses the validator the
            // KYC and property-photo paths already use, so a file renamed to .pdf
            // is rejected here for the same reason it is rejected there.
            var validation = UploadedFileValidator.Validate(
                file,
                UploadedFileValidator.DocumentExtensions,
                UploadedFileValidator.DocumentMaxBytes);

            if (!validation.IsValid)
                return Fail<VerificationDocumentDto>(validation.Error!);

            // Private prefix, partitioned by case. Title deeds and company records
            // must never be anonymously readable — the key is stored and a link is
            // minted on demand, rather than a URL being persisted.
            var storageKey = await _fileStorage.UploadPrivateFileAsync(
                file,
                $"verification/{verificationCase.Id}",
                validation.ContentType);

            var document = new VerificationDocument(
                verificationCase.Id,
                request.DocumentType,
                storageKey,
                file.FileName,
                validation.ContentType,
                file.Length)
            {
                DocumentNumber = Trim(request.DocumentNumber),
                NameOnDocument = Trim(request.NameOnDocument),
                IssuingAuthority = Trim(request.IssuingAuthority),
                IssuedAt = request.IssuedAt,
                ExpiresAt = request.ExpiresAt,
            };

            await _unitOfWork.VerificationDocumentCommands.InsertAsync(document);
            await _unitOfWork.SaveAsync();

            return Ok(ToDto(document));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AddDocumentAsync for case {CaseId}", caseId);
            return Fail<VerificationDocumentDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> RemoveDocumentAsync(Guid customerId, Guid caseId, Guid documentId)
    {
        try
        {
            var (verificationCase, error) = await LoadOwnedCaseAsync(customerId, caseId);
            if (verificationCase is null) return Fail<bool>(error!);

            if (!verificationCase.CanAcceptDocuments)
                return Fail<bool>(ResponseMessages.VerificationCaseNotEditable);

            var document = await _unitOfWork.VerificationDocumentQueries.GetByIdAsync(documentId);

            // Confirm the document belongs to this case, not merely that it exists.
            // Otherwise a valid case id plus somebody else's document id deletes
            // their evidence.
            if (document is null || document.VerificationCaseId != verificationCase.Id)
                return Fail<bool>(ResponseMessages.SetNotFoundMessage("document"));

            await _unitOfWork.VerificationDocumentCommands.DeleteAsync(document);
            await _unitOfWork.SaveAsync();

            // Storage cleanup is best-effort. An orphaned private object costs a few
            // cents; failing the user's delete because S3 was briefly unavailable
            // leaves them looking at a document they asked to remove.
            try
            {
                await _fileStorage.DeleteFileAsync(document.StorageKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete verification object {Key}", document.StorageKey);
            }

            return Ok<bool>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RemoveDocumentAsync for case {CaseId}", caseId);
            return Fail<bool>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<VerificationCaseDto>> SubmitCaseAsync(Guid customerId, Guid caseId)
    {
        try
        {
            var (verificationCase, error) = await LoadOwnedCaseAsync(customerId, caseId);
            if (verificationCase is null) return Fail<VerificationCaseDto>(error!);

            var documents = await LoadDocumentsAsync(verificationCase.Id);

            var missing = VerificationRequirements.MissingFrom(
                verificationCase.SubjectType, documents.Select(d => d.DocumentType));

            if (missing.Count > 0)
            {
                return Fail<VerificationCaseDto>(
                    ResponseMessages.VerificationDocumentsMissing(missing.Select(DescribeDocumentType)));
            }

            if (!verificationCase.TrySubmit())
                return Fail<VerificationCaseDto>(ResponseMessages.VerificationCaseAlreadySubmitted);

            await _unitOfWork.VerificationCaseCommands.UpdateAsync(verificationCase);
            await _unitOfWork.SaveAsync();

            return Ok(await ToDtoAsync(verificationCase, documents.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SubmitCaseAsync for case {CaseId}", caseId);
            return Fail<VerificationCaseDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<List<VerificationCaseDto>>> GetMyCasesAsync(Guid customerId)
    {
        try
        {
            var cases = await _unitOfWork.VerificationCaseQueries.GetAllAsync(
                c => c.SubmittedByCustomerId == customerId);

            var ordered = cases.OrderByDescending(c => c.DateCreated).ToList();
            var counts = await CountDocumentsAsync(ordered.Select(c => c.Id));

            var dtos = new List<VerificationCaseDto>(ordered.Count);
            foreach (var item in ordered)
                dtos.Add(await ToDtoAsync(item, counts.GetValueOrDefault(item.Id, 0)));

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMyCasesAsync for customer {CustomerId}", customerId);
            return FailWith(new List<VerificationCaseDto>(), ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<VerificationCaseDetailDto>> GetMyCaseAsync(Guid customerId, Guid caseId)
    {
        try
        {
            var (verificationCase, error) = await LoadOwnedCaseAsync(customerId, caseId);
            if (verificationCase is null) return Fail<VerificationCaseDetailDto>(error!);

            return Ok(await ToDetailDtoAsync(verificationCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMyCaseAsync for case {CaseId}", caseId);
            return Fail<VerificationCaseDetailDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<string>> GetMyDocumentUrlAsync(Guid customerId, Guid caseId, Guid documentId)
    {
        try
        {
            var (verificationCase, error) = await LoadOwnedCaseAsync(customerId, caseId);
            if (verificationCase is null) return Fail<string>(error!);

            var document = await _unitOfWork.VerificationDocumentQueries.GetByIdAsync(documentId);
            if (document is null || document.VerificationCaseId != verificationCase.Id)
                return Fail<string>(ResponseMessages.SetNotFoundMessage("document"));

            var url = await _fileStorage.GetPresignedUrlAsync(document.StorageKey, DocumentLinkLifetime);
            return Ok(url, "Link valid for 10 minutes.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMyDocumentUrlAsync for document {DocumentId}", documentId);
            return Fail<string>(ResponseMessages.UnexpectedError);
        }
    }

    // ── Reviewer ────────────────────────────────────────────────

    public async Task<BaseResponse<PaginatedResult<VerificationCaseDto>>> GetReviewQueueAsync(
        int pageNumber, int pageSize, VerificationSubjectType? subjectType = null)
    {
        try
        {
            // Reads the sparse ReviewQueueStatus index, so this costs what the
            // outstanding work costs — not what every case ever decided costs. See
            // VerificationCase.ReviewQueueStatus.
            var awaiting = await _unitOfWork.VerificationCaseQueries.GetAllAsync(
                c => c.ReviewQueueStatus == VerificationCase.AwaitingReviewMarker);

            IEnumerable<VerificationCase> query = awaiting;

            if (subjectType.HasValue)
                query = query.Where(c => c.SubjectType == subjectType.Value);

            // Oldest first: a review queue is a queue. Newest-first quietly starves
            // whoever has been waiting longest, which is the applicant most likely to
            // give up.
            var ordered = query.OrderBy(c => c.SubmittedAt ?? c.DateCreated).ToList();
            var totalCount = ordered.Count;

            var paged = ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var counts = await CountDocumentsAsync(paged.Select(c => c.Id));

            var dtos = new List<VerificationCaseDto>(paged.Count);
            foreach (var item in paged)
                dtos.Add(await ToDtoAsync(item, counts.GetValueOrDefault(item.Id, 0), includeLabels: true));

            return Ok(new PaginatedResult<VerificationCaseDto>(dtos, totalCount, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetReviewQueueAsync");
            return Fail<PaginatedResult<VerificationCaseDto>>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<VerificationCaseDetailDto>> GetCaseForReviewAsync(Guid caseId)
    {
        try
        {
            var verificationCase = await _unitOfWork.VerificationCaseQueries.GetByIdAsync(caseId);
            if (verificationCase is null)
                return Fail<VerificationCaseDetailDto>(ResponseMessages.VerificationCaseNotFound);

            return Ok(await ToDetailDtoAsync(verificationCase, includeLabels: true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCaseForReviewAsync for case {CaseId}", caseId);
            return Fail<VerificationCaseDetailDto>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<string>> GetDocumentUrlForReviewAsync(Guid documentId)
    {
        try
        {
            var document = await _unitOfWork.VerificationDocumentQueries.GetByIdAsync(documentId);
            if (document is null)
                return Fail<string>(ResponseMessages.SetNotFoundMessage("document"));

            var url = await _fileStorage.GetPresignedUrlAsync(document.StorageKey, DocumentLinkLifetime);
            return Ok(url, "Link valid for 10 minutes.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetDocumentUrlForReviewAsync for document {DocumentId}", documentId);
            return Fail<string>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> BeginReviewAsync(Guid adminId, Guid caseId)
    {
        try
        {
            var verificationCase = await _unitOfWork.VerificationCaseQueries.GetByIdAsync(caseId);
            if (verificationCase is null) return Fail<bool>(ResponseMessages.VerificationCaseNotFound);

            if (!verificationCase.TryBeginReview(adminId))
                return Fail<bool>(ResponseMessages.VerificationCaseNotAwaitingReview);

            await _unitOfWork.VerificationCaseCommands.UpdateAsync(verificationCase);
            await _unitOfWork.SaveAsync();

            return Ok<bool>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BeginReviewAsync for case {CaseId}", caseId);
            return Fail<bool>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> ReviewDocumentAsync(
        Guid adminId, Guid documentId, ReviewDocumentDto request)
    {
        try
        {
            var document = await _unitOfWork.VerificationDocumentQueries.GetByIdAsync(documentId);
            if (document is null) return Fail<bool>(ResponseMessages.SetNotFoundMessage("document"));

            var status = request.Approved ? DocumentReviewStatus.Approved : DocumentReviewStatus.Rejected;

            if (!document.TryReview(status, adminId, request.RejectionReason))
                return Fail<bool>(ResponseMessages.VerificationDocumentRejectionReasonRequired);

            await _unitOfWork.VerificationDocumentCommands.UpdateAsync(document);
            await _unitOfWork.SaveAsync();

            return Ok<bool>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ReviewDocumentAsync for document {DocumentId}", documentId);
            return Fail<bool>(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> DecideCaseAsync(Guid adminId, Guid caseId, DecideCaseDto request)
    {
        try
        {
            var verificationCase = await _unitOfWork.VerificationCaseQueries.GetByIdAsync(caseId);
            if (verificationCase is null) return Fail<bool>(ResponseMessages.VerificationCaseNotFound);

            var documents = await LoadDocumentsAsync(verificationCase.Id);

            if (request.Outcome == VerificationCaseStatus.Approved)
            {
                // Approving while a document is still Pending would mean the badge
                // rests on evidence nobody looked at. The reviewer must have formed a
                // view on every piece of it.
                if (documents.Any(d => d.Status == DocumentReviewStatus.Pending))
                    return Fail<bool>(ResponseMessages.VerificationDocumentsNotAllReviewed);

                if (documents.Any(d => d.Status == DocumentReviewStatus.Rejected))
                    return Fail<bool>(ResponseMessages.VerificationDocumentsNotAllReviewed);
            }

            // A case is only current until its shortest-lived document lapses.
            var expiresAt = documents
                .Where(d => d.Status == DocumentReviewStatus.Approved && d.ExpiresAt.HasValue)
                .Select(d => d.ExpiresAt!.Value)
                .DefaultIfEmpty()
                .Min();

            var earliestExpiry = expiresAt == default ? (DateTime?)null : expiresAt;

            if (!verificationCase.TryDecide(request.Outcome, adminId, request.Note, earliestExpiry))
            {
                return Fail<bool>(verificationCase.IsAwaitingReview
                    ? ResponseMessages.VerificationDecisionNoteRequired
                    : ResponseMessages.VerificationCaseNotAwaitingReview);
            }

            await _unitOfWork.VerificationCaseCommands.UpdateAsync(verificationCase);
            await _unitOfWork.SaveAsync();

            await NotifyDecisionAsync(verificationCase);

            return Ok<bool>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DecideCaseAsync for case {CaseId}", caseId);
            return Fail<bool>(ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// Tells the applicant what was decided, in-app and by email.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Best-effort throughout, and deliberately after the save. The decision is
    /// already persisted by the time this runs, so a mail outage must not make a
    /// completed review report as a failure — that is exactly the bug the KYC path
    /// had, where an admin saw an error for an approval that had actually gone
    /// through and would then try again.
    /// </para>
    /// <para>
    /// Both channels, not one. Email is what reaches someone who is not currently
    /// using the site, which after a multi-day review is most people; the in-app
    /// notification is what they see when they do come back, and it is the only
    /// record that survives a spam filter.
    /// </para>
    /// <para>
    /// EscalatedNameMismatch is deliberately silent. It means the documents appear to
    /// belong to somebody else, and telling a suspected impersonator precisely which
    /// check caught them just teaches them what to fix. A human decides what to say
    /// there.
    /// </para>
    /// </remarks>
    private async Task NotifyDecisionAsync(VerificationCase verificationCase)
    {
        if (verificationCase.Status is not (VerificationCaseStatus.Approved or VerificationCaseStatus.Rejected))
            return;

        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByIdAsync(verificationCase.SubmittedByCustomerId);
            if (customer is null) return;

            var approved = verificationCase.Status == VerificationCaseStatus.Approved;
            var subject = await DescribeSubjectAsync(verificationCase);

            var notification = new Notification(
                customer.Id,
                approved ? NotificationType.VerificationApproved : NotificationType.VerificationRejected,
                approved ? "Verification approved" : "Verification needs attention",
                approved
                    ? $"Your verification for {subject} has been approved."
                    : $"Your verification for {subject} wasn't approved. {verificationCase.DecisionNote}",
                verificationCase.SubjectType == VerificationSubjectType.Property
                    ? verificationCase.SubjectId
                    : null);

            await _unitOfWork.NotificationCommands.InsertAsync(notification);
            await _unitOfWork.SaveAsync();

            var dto = new Dtos.Notification.NotificationDto(
                notification.Id, notification.DateCreated, notification.RecipientId, notification.InspectionId,
                notification.Type, notification.Title, notification.Message, notification.IsRead,
                notification.PropertyId);

            await _realtimeNotifier.SendNotificationAsync(customer.Id, dto);

            if (approved)
            {
                await _emailService.SendVerificationApprovedAsync(customer.Email, customer.FirstName, subject);
            }
            else
            {
                await _emailService.SendVerificationRejectedAsync(
                    customer.Email, customer.FirstName, subject,
                    verificationCase.DecisionNote ?? "No reason provided.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Verification decision on case {CaseId} was saved, but notifying the applicant failed",
                verificationCase.Id);
        }
    }

    /// <summary>Human-readable name for what was verified, used in copy the applicant reads.</summary>
    private async Task<string> DescribeSubjectAsync(VerificationCase verificationCase)
    {
        if (verificationCase.SubjectType != VerificationSubjectType.Property)
            return "your business";

        var property = await _unitOfWork.PropertyQueries.GetByIdAsync(verificationCase.SubjectId);
        return property?.Title is { Length: > 0 } title ? $"\"{title}\"" : "your listing";
    }

    // ── Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Loads a case the given customer submitted, or explains why not.
    /// </summary>
    /// <remarks>
    /// Returns the same "not found" message whether the case is absent or belongs to
    /// somebody else. A distinct "forbidden" would confirm the id exists, turning
    /// GUID guessing into a way to discover who is applying for what.
    /// </remarks>
    private async Task<(VerificationCase? Case, string? Error)> LoadOwnedCaseAsync(Guid customerId, Guid caseId)
    {
        var verificationCase = await _unitOfWork.VerificationCaseQueries.GetByIdAsync(caseId);

        if (verificationCase is null || verificationCase.SubmittedByCustomerId != customerId)
            return (null, ResponseMessages.VerificationCaseNotFound);

        return (verificationCase, null);
    }

    private async Task<List<VerificationDocument>> LoadDocumentsAsync(Guid caseId)
    {
        var documents = await _unitOfWork.VerificationDocumentQueries.GetAllAsync(
            d => d.VerificationCaseId == caseId);

        return documents.OrderBy(d => d.DateCreated).ToList();
    }

    /// <summary>Document counts per case, in one batched indexed read rather than one per case.</summary>
    private async Task<Dictionary<Guid, int>> CountDocumentsAsync(IEnumerable<Guid> caseIds)
    {
        var ids = caseIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var documents = await _unitOfWork.VerificationDocumentQueries.GetManyByAsync(
            d => d.VerificationCaseId, ids);

        return documents
            .GroupBy(d => d.VerificationCaseId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<VerificationCaseDto> ToDtoAsync(
        VerificationCase source, int? documentCount = null, bool includeLabels = false)
    {
        string? subjectLabel = null;
        string? submittedByName = null;

        if (includeLabels)
        {
            // Only for the reviewer's screens. Two extra key reads per case is a fine
            // price for a queue a human can triage without opening every row, but it
            // is not worth paying on the submitter's own list where they already know
            // what they submitted.
            var submitter = await _unitOfWork.CustomerQueries.GetByIdAsync(source.SubmittedByCustomerId);
            submittedByName = submitter is null ? null : $"{submitter.FirstName} {submitter.LastName}";

            subjectLabel = source.SubjectType switch
            {
                VerificationSubjectType.Property =>
                    (await _unitOfWork.PropertyQueries.GetByIdAsync(source.SubjectId))?.Title,
                _ => submittedByName,
            };
        }

        return new VerificationCaseDto(
            source.Id,
            source.SubjectId,
            source.SubjectType,
            source.SubmittedByCustomerId,
            source.RequestedTier,
            source.Status,
            source.DateCreated,
            source.SubmittedAt,
            source.DecidedAt,
            source.DecisionNote,
            source.ExpiresAt,
            documentCount ?? 0,
            subjectLabel,
            submittedByName);
    }

    private async Task<VerificationCaseDetailDto> ToDetailDtoAsync(
        VerificationCase source, bool includeLabels = false)
    {
        var documents = await LoadDocumentsAsync(source.Id);

        var missing = VerificationRequirements.MissingFrom(
            source.SubjectType, documents.Select(d => d.DocumentType));

        return new VerificationCaseDetailDto(
            await ToDtoAsync(source, documents.Count, includeLabels),
            documents.Select(ToDto).ToList(),
            missing);
    }

    private static VerificationDocumentDto ToDto(VerificationDocument source) =>
        new(
            source.Id,
            source.DocumentType,
            source.OriginalFileName,
            source.FileSizeInBytes,
            source.DocumentNumber,
            source.NameOnDocument,
            source.IssuingAuthority,
            source.IssuedAt,
            source.ExpiresAt,
            source.Status,
            source.RejectionReason,
            source.ReviewedAt,
            source.AutoCheckPassed,
            source.AutoCheckProvider);

    /// <summary>Human-readable name for a document type, from its [Description].</summary>
    private static string DescribeDocumentType(VerificationDocumentType type)
    {
        var member = typeof(VerificationDocumentType).GetField(type.ToString());
        var description = member?.GetCustomAttribute<DescriptionAttribute>()?.Description;

        return description ?? type.ToString();
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BaseResponse<T> Ok<T>(T data, string message = ResponseMessages.Successful) =>
        new(data, true, string.Empty, message);

    private static BaseResponse<T> Fail<T>(string message) =>
        new(default, false, string.Empty, message);

    /// <summary>Failure that still carries a payload — an empty list rather than null.</summary>
    private static BaseResponse<T> FailWith<T>(T data, string message) =>
        new(data, false, string.Empty, message);
}
