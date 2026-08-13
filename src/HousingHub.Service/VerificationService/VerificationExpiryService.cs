using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.VerificationService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.VerificationService;

/// <summary>
/// Sweeps lapsed verifications and revokes the badges they granted.
/// </summary>
/// <remarks>
/// <para>
/// Reads the sparse <c>ExpiryWatch-index</c>, which contains only approved cases
/// that carry an expiry — a small fraction of all cases, and the only ones that
/// can possibly lapse. The sweep therefore costs what the expiring work costs
/// rather than growing with every verification ever granted.
/// </para>
/// <para>
/// <b>Each case is handled independently.</b> One failure must not abort the run,
/// because the cases after it would keep badges they are no longer entitled to
/// until somebody noticed. Failures are counted and returned so the caller can
/// alert on them.
/// </para>
/// </remarks>
public class VerificationExpiryService : IVerificationExpiryService
{
    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<VerificationExpiryService> _logger;

    public VerificationExpiryService(
        IUnitOfWOrk unitOfWork,
        IEmailService emailService,
        IRealtimeNotifier realtimeNotifier,
        ILogger<VerificationExpiryService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task<VerificationExpirySummary> ExpireLapsedAsync(DateTime asOf)
    {
        var watched = await _unitOfWork.VerificationCaseQueries.GetAllAsync(
            c => c.ExpiryWatch == VerificationCase.ExpiryWatchMarker);

        // The index cannot express "expiry is in the past", so the date comparison
        // happens here. It reads a small set either way.
        var lapsed = watched
            .Where(c => c.ExpiresAt.HasValue && c.ExpiresAt.Value <= asOf)
            .ToList();

        var expired = 0;
        var revoked = 0;
        var failed = 0;

        foreach (var verificationCase in lapsed)
        {
            try
            {
                if (!verificationCase.TryExpire(asOf)) continue;

                await _unitOfWork.VerificationCaseCommands.UpdateAsync(verificationCase);
                await _unitOfWork.SaveAsync();
                expired++;

                if (await RevokeTierAsync(verificationCase)) revoked++;

                await NotifyLapsedAsync(verificationCase);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "Could not expire verification case {CaseId}. The subject may still be "
                    + "showing a badge it is no longer entitled to.",
                    verificationCase.Id);
            }
        }

        _logger.LogInformation(
            "Verification expiry sweep: examined {Examined}, expired {Expired}, revoked {Revoked}, failed {Failed}",
            lapsed.Count, expired, revoked, failed);

        return new VerificationExpirySummary(lapsed.Count, expired, revoked, failed);
    }

    public async Task<VerificationReminderSummary> SendExpiryRemindersAsync(DateTime asOf)
    {
        var watched = await _unitOfWork.VerificationCaseQueries.GetAllAsync(
            c => c.ExpiryWatch == VerificationCase.ExpiryWatchMarker);

        var candidates = watched.Where(c => c.ExpiresAt.HasValue && c.ExpiresAt.Value > asOf).ToList();

        var sent = 0;
        var failed = 0;

        foreach (var verificationCase in candidates)
        {
            // Thresholds are ordered descending, so the first one crossed is the
            // tightest warning that still applies. A case three days out gets the
            // seven-day nudge, not the thirty-day one it slept through.
            var daysRemaining = (int)Math.Ceiling((verificationCase.ExpiresAt!.Value - asOf).TotalDays);

            var threshold = ExpiryReminderThresholds.DaysBefore
                .FirstOrDefault(t => daysRemaining <= t && verificationCase.NeedsExpiryReminder(t));

            if (threshold == 0) continue;

            try
            {
                // Sent before the threshold is recorded, so a failure is retried
                // tomorrow rather than silently swallowed. The cost of that ordering
                // is a possible duplicate if the save fails after the email went out
                // — far better than a warning that never arrives.
                await SendReminderAsync(verificationCase, daysRemaining);

                verificationCase.MarkExpiryReminderSent(threshold);
                await _unitOfWork.VerificationCaseCommands.UpdateAsync(verificationCase);
                await _unitOfWork.SaveAsync();

                sent++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "Could not send the {Threshold}-day expiry reminder for case {CaseId}",
                    threshold, verificationCase.Id);
            }
        }

        _logger.LogInformation(
            "Verification expiry reminders: examined {Examined}, sent {Sent}, failed {Failed}",
            candidates.Count, sent, failed);

        return new VerificationReminderSummary(candidates.Count, sent, failed);
    }

    /// <summary>
    /// Warns one subject that their verification is about to lapse.
    /// </summary>
    /// <remarks>
    /// Not wrapped in its own try/catch, unlike the other notification paths here.
    /// A reminder that fails to send has not happened, and the caller needs to know
    /// so the threshold is not recorded and tomorrow's run tries again. Swallowing it
    /// would mark the warning as delivered when it was not.
    /// </remarks>
    private async Task SendReminderAsync(VerificationCase verificationCase, int daysRemaining)
    {
        var customer = await _unitOfWork.CustomerQueries.GetByIdAsync(verificationCase.SubmittedByCustomerId)
            ?? throw new InvalidOperationException(
                $"Case {verificationCase.Id} has no submitter to remind.");

        var subject = await DescribeSubjectAsync(verificationCase);

        var notification = new Notification(
            customer.Id,
            NotificationType.VerificationExpiringSoon,
            "Verification expiring soon",
            $"Your verification for {subject} expires in {daysRemaining} "
            + $"day{(daysRemaining == 1 ? "" : "s")}. Upload a current document to keep your badge.",
            verificationCase.SubjectType == VerificationSubjectType.Property
                ? verificationCase.SubjectId
                : null);

        await _unitOfWork.NotificationCommands.InsertAsync(notification);
        await _unitOfWork.SaveAsync();

        await _realtimeNotifier.SendNotificationAsync(customer.Id, new NotificationDto(
            notification.Id, notification.DateCreated, notification.RecipientId,
            notification.InspectionId, notification.Type, notification.Title,
            notification.Message, notification.IsRead, notification.PropertyId));

        await _emailService.SendVerificationExpiringSoonAsync(
            customer.Email, customer.FirstName, subject, daysRemaining,
            verificationCase.ExpiresAt!.Value);
    }

    /// <summary>Human-readable name for what was verified, used in copy the subject reads.</summary>
    private async Task<string> DescribeSubjectAsync(VerificationCase verificationCase)
    {
        if (verificationCase.SubjectType != VerificationSubjectType.Property)
            return "your business";

        var property = await _unitOfWork.PropertyQueries.GetByIdAsync(verificationCase.SubjectId);
        return property?.Title is { Length: > 0 } title ? $"\"{title}\"" : "your listing";
    }

    /// <summary>
    /// Takes the tier back off the subject.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The case status alone is not enough. <c>Customer.IsBusinessVerified</c> checks
    /// the expiry date so a lapsed badge stops rendering immediately, but the stored
    /// tier still says BusinessVerified — and anything that reads the tier directly,
    /// now or in future, would be wrong. Clearing it makes the data honest rather
    /// than relying on every reader remembering to check a second field.
    /// </para>
    /// <para>
    /// Identity is deliberately excluded. It is verified against NIN or BVN, which do
    /// not lapse, so an identity case should never carry an expiry in the first
    /// place — and revoking KYC would lock someone out of publishing over a
    /// bookkeeping detail.
    /// </para>
    /// </remarks>
    private async Task<bool> RevokeTierAsync(VerificationCase verificationCase)
    {
        switch (verificationCase.SubjectType)
        {
            case VerificationSubjectType.Business:
            {
                var customer = await _unitOfWork.CustomerQueries.GetByIdAsync(verificationCase.SubjectId);
                if (customer is null) return false;

                customer.BusinessVerificationTier = VerificationTier.Unverified;

                // BusinessVerifiedAt is left in place on purpose. It records that the
                // check happened, which is still true and is worth keeping for the
                // audit trail — it is the tier, not the history, that stops applying.
                await _unitOfWork.CustomerCommands.UpdateAsync(customer);
                await _unitOfWork.SaveAsync();
                return true;
            }

            case VerificationSubjectType.Property:
            {
                var property = await _unitOfWork.PropertyQueries.GetByIdAsync(verificationCase.SubjectId);
                if (property is null) return false;

                property.TitleVerificationTier = VerificationTier.Unverified;

                await _unitOfWork.PropertyCommands.UpdateAsync(property);
                await _unitOfWork.SaveAsync();
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Tells the subject their verification lapsed, and why it is different from a
    /// rejection.
    /// </summary>
    /// <remarks>
    /// Nothing was wrong with their submission — it aged out — and the action they
    /// need to take is different, so the copy must not read as a rejection. Somebody
    /// who thinks they were rejected goes looking for what they did wrong.
    ///
    /// Best-effort, and after the revocation is saved. A mail outage must not leave a
    /// lapsed badge in place.
    /// </remarks>
    private async Task NotifyLapsedAsync(VerificationCase verificationCase)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByIdAsync(verificationCase.SubmittedByCustomerId);
            if (customer is null) return;

            var subject = await DescribeSubjectAsync(verificationCase);

            var notification = new Notification(
                customer.Id,
                NotificationType.VerificationExpired,
                "Verification expired",
                $"Your verification for {subject} has expired because one of the documents "
                + "reached its expiry date. Upload a current document to restore your badge.",
                verificationCase.SubjectType == VerificationSubjectType.Property
                    ? verificationCase.SubjectId
                    : null);

            await _unitOfWork.NotificationCommands.InsertAsync(notification);
            await _unitOfWork.SaveAsync();

            await _realtimeNotifier.SendNotificationAsync(customer.Id, new NotificationDto(
                notification.Id, notification.DateCreated, notification.RecipientId,
                notification.InspectionId, notification.Type, notification.Title,
                notification.Message, notification.IsRead, notification.PropertyId));

            await _emailService.SendVerificationExpiredAsync(customer.Email, customer.FirstName, subject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Verification case {CaseId} was expired, but notifying the subject failed",
                verificationCase.Id);
        }
    }
}
