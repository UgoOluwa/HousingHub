using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.Dtos.Tenancy;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.TenancyService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.TenancyService;

/// <summary>
/// Choosing a tenant, and backing out of having chosen one.
/// </summary>
/// <remarks>
/// <para>
/// The step between an inspection and an agreement, and the one that did not exist:
/// inspections ran to <c>Completed</c> and stopped, with nothing recording that an
/// owner had picked anybody. Documents, fees and the rent payment all attach to the
/// tenancy this creates.
/// </para>
/// <para>
/// There is exactly one counterparty to the tenant, taken from whoever listed the
/// property. Where an agent manages for a landlord, the agent is that party and the
/// landlord is not represented here — see docs/tenancy-lifecycle-plan.md.
/// </para>
/// </remarks>
public class TenancyService : ITenancyService
{
    private const string ClassName = "tenancy";
    private const string PropertyIndex = "PropertyId-index";
    private const string LandlordIndex = "LandlordCustomerId-index";
    private const string TenantIndex = "TenantCustomerId-index";

    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<TenancyService> _logger;

    public TenancyService(
        IUnitOfWOrk unitOfWork,
        IEmailService emailService,
        IRealtimeNotifier realtimeNotifier,
        ILogger<TenancyService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task<BaseResponse<List<TenancyCandidateDto>>> GetCandidatesAsync(
        Guid propertyId, Guid authenticatedUserId)
    {
        try
        {
            var property = await _unitOfWork.PropertyQueries.GetByIdAsync(propertyId);
            if (property is null || property.OwnerId != authenticatedUserId)
                return FailList(ResponseMessages.SetNotFoundMessage("property"));

            var inspections = await _unitOfWork.PropertyInspectionQueries.QueryByIndexAsync(
                "PropertyId-index", propertyId);

            // Completed only. Somebody who booked and never turned up has not seen the
            // property, and choosing them is not a decision anyone should be making
            // from this screen.
            var completed = inspections
                .Where(i => i.Status == InspectionStatus.Completed)
                .GroupBy(i => i.CustomerId)
                .Select(g => g.OrderByDescending(i => i.ScheduledDate).First())
                .ToList();

            if (completed.Count == 0)
                return OkList(new List<TenancyCandidateDto>());

            var customers = await _unitOfWork.CustomerQueries.GetManyByAsync(
                c => c.Id, completed.Select(i => i.CustomerId).Distinct());
            var byId = customers.ToDictionary(c => c.Id);

            var candidates = completed
                .Select(inspection =>
                {
                    byId.TryGetValue(inspection.CustomerId, out var customer);
                    return new TenancyCandidateDto(
                        inspection.CustomerId,
                        customer is null ? null : $"{customer.FirstName} {customer.LastName}".Trim(),
                        inspection.Id,
                        inspection.ScheduledDate,
                        customer?.IsKycVerified == true);
                })
                .OrderByDescending(c => c.InspectedOn)
                .ToList();

            return OkList(candidates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing candidates for property {PropertyId}", propertyId);
            return FailList(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<TenancyDto>> SelectCandidateAsync(
        Guid propertyId, Guid candidateCustomerId, Guid authenticatedUserId)
    {
        try
        {
            var property = await _unitOfWork.PropertyQueries.GetByIdAsync(propertyId);

            // Not-found rather than forbidden for a stranger: whether a given property
            // id exists and who owns it is not a question this endpoint should answer.
            if (property is null || property.OwnerId != authenticatedUserId)
                return Fail(ResponseMessages.SetNotFoundMessage("property"));

            // The agreement and the payment are both built on the rent, so a listing
            // with no price set cannot start one. "Contact for price" is a listing
            // state, not a tenancy state.
            if (property.Price <= 0)
                return Fail(ResponseMessages.TenancyPriceNotSet);

            if (await FindLiveTenancyAsync(propertyId) is not null)
                return Fail(ResponseMessages.TenancyAlreadyLive);

            var inspection = await FindCompletedInspectionAsync(propertyId, candidateCustomerId);
            if (inspection is null)
                return Fail(ResponseMessages.TenancyCandidateNotInspected);

            var tenancy = new Tenancy(
                propertyId,
                landlordCustomerId: property.OwnerId,
                tenantCustomerId: candidateCustomerId,
                selectedFromInspectionId: inspection.Id,
                agreedRentKobo: ToKobo(property.Price),
                leaseType: property.PropertyLeaseType);

            if (!await _unitOfWork.TenancyCommands.InsertAsync(tenancy))
                return Fail(ResponseMessages.SetCreationFailureMessage(ClassName));

            // Under offer, not Rented. Nothing has been agreed or paid — this stops
            // new inspection requests arriving for a property that is effectively
            // spoken for, and reverses cleanly if the deal falls through.
            property.Availability = PropertyAvailability.UnderOffer;
            await _unitOfWork.PropertyCommands.UpdateAsync(property);

            await _unitOfWork.SaveAsync();

            await NotifyCandidateSelectedAsync(tenancy, property);

            return Ok(await ToDtoAsync(tenancy, property), ResponseMessages.TenancyCandidateSelected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting candidate for property {PropertyId}", propertyId);
            return Fail(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<TenancyDto>> WithdrawAsync(
        Guid tenancyId, string? reason, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await _unitOfWork.TenancyQueries.GetByIdAsync(tenancyId);
            if (tenancy is null || tenancy.LandlordCustomerId != authenticatedUserId)
                return Fail(ResponseMessages.SetNotFoundMessage(ClassName));

            if (!tenancy.TryWithdraw(reason?.Trim()))
                return Fail(ResponseMessages.TenancyNotCancellable);

            await _unitOfWork.TenancyCommands.UpdateAsync(tenancy);

            var property = await ReleasePropertyAsync(tenancy.PropertyId);
            await _unitOfWork.SaveAsync();

            await NotifyAsync(
                tenancy.TenantCustomerId,
                tenancy.Id,
                NotificationType.TenancyWithdrawn,
                "The owner has withdrawn",
                string.IsNullOrWhiteSpace(tenancy.WithdrawnReason)
                    ? $"The owner is no longer going ahead with you for \"{property?.Title ?? "this property"}\"."
                    : $"The owner is no longer going ahead with you for \"{property?.Title ?? "this property"}\": {tenancy.WithdrawnReason}");

            return Ok(await ToDtoAsync(tenancy, property), ResponseMessages.TenancyWithdrawn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing tenancy {TenancyId}", tenancyId);
            return Fail(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<TenancyDto>> DeclineAsync(Guid tenancyId, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await _unitOfWork.TenancyQueries.GetByIdAsync(tenancyId);
            if (tenancy is null || tenancy.TenantCustomerId != authenticatedUserId)
                return Fail(ResponseMessages.SetNotFoundMessage(ClassName));

            if (!tenancy.TryDecline())
                return Fail(ResponseMessages.TenancyNotCancellable);

            await _unitOfWork.TenancyCommands.UpdateAsync(tenancy);

            var property = await ReleasePropertyAsync(tenancy.PropertyId);
            await _unitOfWork.SaveAsync();

            var tenant = await _unitOfWork.CustomerQueries.GetByIdAsync(tenancy.TenantCustomerId);
            var who = tenant is null ? "The person you chose" : $"{tenant.FirstName} {tenant.LastName}".Trim();

            await NotifyAsync(
                tenancy.LandlordCustomerId,
                tenancy.Id,
                NotificationType.TenancyDeclinedByCandidate,
                "Your chosen tenant has declined",
                $"{who} is not going ahead with \"{property?.Title ?? "your property"}\". It's available again and you can choose someone else.");

            return Ok(await ToDtoAsync(tenancy, property), ResponseMessages.TenancyDeclined);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining tenancy {TenancyId}", tenancyId);
            return Fail(ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<List<TenancyDto>>> GetMyTenanciesAsync(Guid customerId)
    {
        try
        {
            // Both sides in one call: somebody can be letting one property and renting
            // another, and making the client ask twice to find that out is pointless.
            var asLandlord = await _unitOfWork.TenancyQueries.QueryByIndexAsync(LandlordIndex, customerId);
            var asTenant = await _unitOfWork.TenancyQueries.QueryByIndexAsync(TenantIndex, customerId);

            var all = asLandlord.Concat(asTenant)
                .DistinctBy(t => t.Id)
                .OrderByDescending(t => t.SelectedAt)
                .ToList();

            return OkDtoList(await ToDtosAsync(all));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing tenancies for customer {CustomerId}", customerId);
            return new BaseResponse<List<TenancyDto>>(
                new List<TenancyDto>(), false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<TenancyDto>> GetAsync(Guid tenancyId, Guid authenticatedUserId)
    {
        try
        {
            var tenancy = await _unitOfWork.TenancyQueries.GetByIdAsync(tenancyId);

            // Readable by its two parties and nobody else. Same answer for "no such
            // tenancy" and "not yours".
            if (tenancy is null
                || (tenancy.LandlordCustomerId != authenticatedUserId
                    && tenancy.TenantCustomerId != authenticatedUserId))
            {
                return Fail(ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return Ok(await ToDtoAsync(tenancy, null), ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading tenancy {TenancyId}", tenancyId);
            return Fail(ResponseMessages.UnexpectedError);
        }
    }

    // ── internals ────────────────────────────────────────────────

    /// <summary>
    /// Naira to kobo, rounded away from zero.
    /// </summary>
    /// <remarks>
    /// The listing holds a decimal of naira; the payment rail works in whole kobo.
    /// Converting once, here, means the figure agreed at selection is the figure that
    /// later reaches Paystack with nothing in between to round it differently.
    /// </remarks>
    private static long ToKobo(decimal naira) =>
        (long)Math.Round(naira * 100m, MidpointRounding.AwayFromZero);

    private async Task<Tenancy?> FindLiveTenancyAsync(Guid propertyId)
    {
        var existing = await _unitOfWork.TenancyQueries.QueryByIndexAsync(PropertyIndex, propertyId);
        return existing.FirstOrDefault(t => t.IsLive);
    }

    private async Task<PropertyInspection?> FindCompletedInspectionAsync(Guid propertyId, Guid customerId)
    {
        var inspections = await _unitOfWork.PropertyInspectionQueries.QueryByIndexAsync(
            "PropertyId-index", propertyId);

        return inspections
            .Where(i => i.CustomerId == customerId && i.Status == InspectionStatus.Completed)
            .OrderByDescending(i => i.ScheduledDate)
            .FirstOrDefault();
    }

    /// <summary>
    /// Puts the listing back on the market after a selection falls through.
    /// </summary>
    /// <remarks>
    /// Only from <see cref="PropertyAvailability.UnderOffer"/>. A property that has
    /// since been marked Rented or Sold by its owner must not be dragged back to
    /// Available by a stale tenancy closing.
    /// </remarks>
    private async Task<Property?> ReleasePropertyAsync(Guid propertyId)
    {
        var property = await _unitOfWork.PropertyQueries.GetByIdAsync(propertyId);
        if (property is null) return null;

        if (property.Availability == PropertyAvailability.UnderOffer)
        {
            property.Availability = PropertyAvailability.Available;
            await _unitOfWork.PropertyCommands.UpdateAsync(property);
        }

        return property;
    }

    private async Task NotifyCandidateSelectedAsync(Tenancy tenancy, Property property)
    {
        await NotifyAsync(
            tenancy.TenantCustomerId,
            tenancy.Id,
            NotificationType.TenancyCandidateSelected,
            "You've been chosen for a property",
            $"The owner of \"{property.Title}\" has chosen you. You'll hear from them shortly about what's needed next.");

        // Email as well as in-app. This is the one notification in the product that
        // changes somebody's housing situation, and relying on them happening to open
        // the app is how a candidate loses a flat to a faster reply.
        var tenant = await _unitOfWork.CustomerQueries.GetByIdAsync(tenancy.TenantCustomerId);
        if (tenant is null || string.IsNullOrWhiteSpace(tenant.Email))
        {
            _logger.LogWarning(
                "Selected candidate {CustomerId} for tenancy {TenancyId} but found no address to email",
                tenancy.TenantCustomerId, tenancy.Id);
            return;
        }

        await _emailService.SendTenancyCandidateSelectedAsync(
            tenant.Email, tenant.FirstName, property.Title, tenancy.AgreedRentKobo);
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
                    notification.Id,
                    notification.DateCreated,
                    notification.RecipientId,
                    notification.InspectionId,
                    notification.Type,
                    notification.Title,
                    notification.Message,
                    notification.IsRead));
        }
        catch (Exception ex)
        {
            // A notification that fails must not undo the decision it was describing.
            // The tenancy is the record; this is how somebody finds out about it.
            _logger.LogError(ex, "Could not notify {RecipientId} about tenancy {SubjectId}", recipientId, subjectId);
        }
    }

    private async Task<TenancyDto> ToDtoAsync(Tenancy tenancy, Property? property)
    {
        property ??= await _unitOfWork.PropertyQueries.GetByIdAsync(tenancy.PropertyId);

        var landlord = await _unitOfWork.CustomerQueries.GetByIdAsync(tenancy.LandlordCustomerId);
        var tenant = await _unitOfWork.CustomerQueries.GetByIdAsync(tenancy.TenantCustomerId);

        return Map(tenancy, property?.Title, Name(landlord), Name(tenant));
    }

    private async Task<List<TenancyDto>> ToDtosAsync(IReadOnlyList<Tenancy> tenancies)
    {
        if (tenancies.Count == 0) return [];

        // One batched read per distinct id rather than one per row inside a loop.
        var properties = await _unitOfWork.PropertyQueries.GetManyByAsync(
            p => p.Id, tenancies.Select(t => t.PropertyId).Distinct());
        var customers = await _unitOfWork.CustomerQueries.GetManyByAsync(
            c => c.Id,
            tenancies.SelectMany(t => new[] { t.LandlordCustomerId, t.TenantCustomerId }).Distinct());

        var propertyById = properties.ToDictionary(p => p.Id);
        var customerById = customers.ToDictionary(c => c.Id);

        return tenancies.Select(t =>
        {
            propertyById.TryGetValue(t.PropertyId, out var property);
            customerById.TryGetValue(t.LandlordCustomerId, out var landlord);
            customerById.TryGetValue(t.TenantCustomerId, out var tenant);
            return Map(t, property?.Title, Name(landlord), Name(tenant));
        }).ToList();
    }

    private static string? Name(Customer? customer) =>
        customer is null ? null : $"{customer.FirstName} {customer.LastName}".Trim();

    private static TenancyDto Map(Tenancy t, string? propertyTitle, string? landlordName, string? tenantName) =>
        new(t.Id, t.PropertyId, propertyTitle, t.LandlordCustomerId, landlordName,
            t.TenantCustomerId, tenantName, t.Status, t.AgreedRentKobo, t.LeaseType,
            t.SelectedFromInspectionId, t.SelectedAt, t.WithdrawnReason, t.ClosedAt, t.DateCreated);

    private static BaseResponse<TenancyDto> Ok(TenancyDto dto, string message) =>
        new(dto, true, string.Empty, message);

    private static BaseResponse<TenancyDto> Fail(string message) =>
        new(default, false, string.Empty, message);

    private static BaseResponse<List<TenancyCandidateDto>> OkList(List<TenancyCandidateDto> data) =>
        new(data, true, string.Empty, ResponseMessages.Successful);

    private static BaseResponse<List<TenancyCandidateDto>> FailList(string message) =>
        new(new List<TenancyCandidateDto>(), false, string.Empty, message);

    private static BaseResponse<List<TenancyDto>> OkDtoList(List<TenancyDto> data) =>
        new(data, true, string.Empty, ResponseMessages.Successful);
}
