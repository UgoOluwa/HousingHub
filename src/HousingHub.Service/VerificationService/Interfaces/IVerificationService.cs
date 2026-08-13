using HousingHub.Core.CustomResponses;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Verification;
using Microsoft.AspNetCore.Http;

namespace HousingHub.Service.VerificationService.Interfaces;

/// <summary>
/// The document pipeline that all verification types run on.
/// </summary>
/// <remarks>
/// Business, title and — later — identity and financial verification are the same
/// workflow with different document types. Building them as one pipeline rather
/// than three features is what makes Phase 5's financial verification a new
/// document type on existing machinery instead of a third ground-up build.
/// </remarks>
public interface IVerificationService
{
    // ── Submitter ───────────────────────────────────────────────

    /// <summary>
    /// Opens a draft case. Returns the existing draft if one is already open for
    /// this subject, so a user who navigates away and back does not accumulate
    /// abandoned cases.
    /// </summary>
    Task<BaseResponse<VerificationCaseDto>> StartCaseAsync(
        Guid customerId, StartVerificationCaseDto request);

    /// <summary>Attaches a document to a draft case owned by the caller.</summary>
    Task<BaseResponse<VerificationDocumentDto>> AddDocumentAsync(
        Guid customerId, Guid caseId, AddVerificationDocumentDto request, IFormFile file);

    /// <summary>Removes a document from a draft case owned by the caller.</summary>
    Task<BaseResponse<bool>> RemoveDocumentAsync(Guid customerId, Guid caseId, Guid documentId);

    /// <summary>
    /// Hands a draft to review. Fails if the required documents for the requested
    /// tier are not all present.
    /// </summary>
    Task<BaseResponse<VerificationCaseDto>> SubmitCaseAsync(Guid customerId, Guid caseId);

    /// <summary>Every case the caller has submitted, newest first.</summary>
    Task<BaseResponse<List<VerificationCaseDto>>> GetMyCasesAsync(Guid customerId);

    /// <summary>One case with its documents. Caller must be the submitter.</summary>
    Task<BaseResponse<VerificationCaseDetailDto>> GetMyCaseAsync(Guid customerId, Guid caseId);

    /// <summary>
    /// A short-lived link to one of the caller's own documents, for reviewing what
    /// they uploaded before submitting.
    /// </summary>
    Task<BaseResponse<string>> GetMyDocumentUrlAsync(Guid customerId, Guid caseId, Guid documentId);

    // ── Reviewer ────────────────────────────────────────────────

    /// <summary>
    /// The review queue. Reads the sparse index, so cost is proportional to
    /// outstanding work rather than to total cases ever created.
    /// </summary>
    Task<BaseResponse<PaginatedResult<VerificationCaseDto>>> GetReviewQueueAsync(
        int pageNumber, int pageSize, VerificationSubjectType? subjectType = null);

    /// <summary>One case with documents and submitter context, for a reviewer.</summary>
    Task<BaseResponse<VerificationCaseDetailDto>> GetCaseForReviewAsync(Guid caseId);

    /// <summary>Short-lived link to a document, for a reviewer.</summary>
    Task<BaseResponse<string>> GetDocumentUrlForReviewAsync(Guid documentId);

    /// <summary>Claims a submitted case so two admins do not review it at once.</summary>
    Task<BaseResponse<bool>> BeginReviewAsync(Guid adminId, Guid caseId);

    /// <summary>Approves or rejects a single document within a case.</summary>
    Task<BaseResponse<bool>> ReviewDocumentAsync(
        Guid adminId, Guid documentId, ReviewDocumentDto request);

    /// <summary>
    /// Records the final decision on a case.
    /// </summary>
    /// <remarks>
    /// Approval requires every document to have been reviewed and approved — an
    /// admin should not be able to approve a case while one of its documents is
    /// still pending, because the badge that results would rest on evidence nobody
    /// looked at.
    /// </remarks>
    Task<BaseResponse<bool>> DecideCaseAsync(Guid adminId, Guid caseId, DecideCaseDto request);
}
