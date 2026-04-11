using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.NotificationService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.InspectionService;

public class InspectionCommandService : IInspectionCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly IEmailService _emailService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<InspectionCommandService> _logger;
    private const string ClassName = "inspection";

    public InspectionCommandService(
        IUnitOfWOrk unitOfWOrk,
        IMapper mapper,
        IEmailService emailService,
        IRealtimeNotifier realtimeNotifier,
        ILogger<InspectionCommandService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _emailService = emailService;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task<BaseResponse<InspectionDto>> ScheduleInspectionAsync(ScheduleInspectionDto request, Guid authenticatedUserId)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId);

            if (customer == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == request.PropertyId);

            if (property == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("property"));

            if (property.OwnerId == authenticatedUserId)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.CannotInspectOwnProperty);

            var inspection = new PropertyInspection(authenticatedUserId, request.PropertyId, request.ScheduledDate, request.ScheduledTime, request.Note);

            bool isSuccessful = await _unitOfWOrk.PropertyInspectionCommands.InsertAsync(inspection);
            if (!isSuccessful)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));

            // Notify property owner (in-app)
            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == property.OwnerId);

            if (owner != null)
            {
                var notification = new Notification(
                    owner.Id,
                    inspection.Id,
                    NotificationType.InspectionScheduled,
                    "New Inspection Request",
                    $"{customer.FirstName} {customer.LastName} has requested an inspection for your property \"{property.Title}\" on {request.ScheduledDate:yyyy-MM-dd} at {request.ScheduledTime:hh\\:mm}.");

                await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
                await PushRealtimeNotificationAsync(notification);
            }

            await _unitOfWOrk.SaveAsync();

            // Notify property owner (email - fire and forget)
            if (owner != null)
            {
                _ = _emailService.SendInspectionScheduledAsync(
                    owner.Email, owner.FirstName,
                    $"{customer.FirstName} {customer.LastName}",
                    property.Title, request.ScheduledDate, request.ScheduledTime, request.Note);
            }

            return new BaseResponse<InspectionDto>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in ScheduleInspectionAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<InspectionDto>> RespondToInspectionAsync(RespondToInspectionDto request, Guid authenticatedUserId)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(
                x => x.Id == request.InspectionId);

            if (inspection == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId);

            if (property == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("property"));

            if (property.OwnerId != authenticatedUserId)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotOwner);

            if (inspection.Status != InspectionStatus.Pending)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotPending);

            if (request.Accept)
            {
                inspection.Status = InspectionStatus.Confirmed;
            }
            else
            {
                inspection.Status = InspectionStatus.Declined;
                inspection.DeclineNote = request.Note;
            }

            await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);

            // Notify customer (in-app)
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == inspection.CustomerId);

            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId);

            string action = request.Accept ? "Confirmed" : "Declined";

            if (customer != null)
            {
                var notificationType = request.Accept ? NotificationType.InspectionConfirmed : NotificationType.InspectionDeclined;
                var notification = new Notification(
                    customer.Id,
                    inspection.Id,
                    notificationType,
                    $"Inspection {action}",
                    $"Your inspection request for \"{property.Title}\" has been {action.ToLower()}.{(string.IsNullOrWhiteSpace(request.Note) ? "" : $" Note: {request.Note}")}");

                await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
                await PushRealtimeNotificationAsync(notification);
            }

            await _unitOfWOrk.SaveAsync();

            // Notify customer (email)
            if (customer != null && owner != null)
            {
                _ = _emailService.SendInspectionResponseAsync(
                    customer.Email, customer.FirstName,
                    $"{owner.FirstName} {owner.LastName}",
                    property.Title, action, request.Note, null, null);
            }

            return new BaseResponse<InspectionDto>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in RespondToInspectionAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<InspectionDto>> RescheduleInspectionAsync(RescheduleInspectionDto request, Guid authenticatedUserId)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(
                x => x.Id == request.InspectionId);

            if (inspection == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId);

            if (property == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("property"));

            bool isOwner = property.OwnerId == authenticatedUserId;
            bool isCustomer = inspection.CustomerId == authenticatedUserId;

            if (!isOwner && !isCustomer)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotParticipant);

            if (inspection.Status != InspectionStatus.Pending && inspection.Status != InspectionStatus.Confirmed)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionCannotReschedule);

            inspection.Status = InspectionStatus.Rescheduled;
            inspection.RescheduledDate = request.RescheduledDate;
            inspection.RescheduledTime = request.RescheduledTime;
            inspection.RescheduleNote = request.Note;

            await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);

            var initiator = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId);

            // Notify the other party
            Guid recipientId = isOwner ? inspection.CustomerId : property.OwnerId;
            string initiatorName = initiator != null ? $"{initiator.FirstName} {initiator.LastName}" : "A user";
            string role = isOwner ? "property owner" : "requester";

            var recipient = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == recipientId);

            var notification = new Notification(
                recipientId,
                inspection.Id,
                NotificationType.InspectionRescheduled,
                "Inspection Rescheduled",
                $"The {role} has rescheduled the inspection for \"{property.Title}\" to {request.RescheduledDate:yyyy-MM-dd} at {request.RescheduledTime:hh\\:mm}.{(string.IsNullOrWhiteSpace(request.Note) ? "" : $" Note: {request.Note}")}");

            await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
            await PushRealtimeNotificationAsync(notification);
            await _unitOfWOrk.SaveAsync();

            if (recipient != null && initiator != null)
            {
                _ = _emailService.SendInspectionResponseAsync(
                    recipient.Email, recipient.FirstName,
                    initiatorName,
                    property.Title, "Rescheduled", request.Note,
                    request.RescheduledDate, request.RescheduledTime);
            }

            return new BaseResponse<InspectionDto>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in RescheduleInspectionAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<InspectionDto>> RespondToRescheduleAsync(Guid inspectionId, bool accept, Guid authenticatedUserId)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(
                x => x.Id == inspectionId);

            if (inspection == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (inspection.CustomerId != authenticatedUserId)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotCustomer);

            if (inspection.Status != InspectionStatus.Rescheduled)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotPendingOrRescheduled);

            if (accept)
            {
                inspection.ScheduledDate = inspection.RescheduledDate!.Value;
                inspection.ScheduledTime = inspection.RescheduledTime!.Value;
                inspection.Status = InspectionStatus.Confirmed;
            }
            else
            {
                inspection.Status = InspectionStatus.Cancelled;
            }

            await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);

            // Notify property owner (in-app)
            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId);

            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId);

            if (property != null && customer != null)
            {
                var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                    x => x.Id == property.OwnerId);

                if (owner != null)
                {
                    string action = accept ? "accepted" : "declined";
                    var notificationType = accept ? NotificationType.InspectionConfirmed : NotificationType.InspectionCancelled;
                    var notification = new Notification(
                        owner.Id,
                        inspection.Id,
                        notificationType,
                        $"Reschedule {(accept ? "Accepted" : "Declined")}",
                        $"{customer.FirstName} {customer.LastName} has {action} the rescheduled inspection for \"{property.Title}\".");

                    await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
                    await PushRealtimeNotificationAsync(notification);
                }
            }

            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<InspectionDto>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in RespondToRescheduleAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> CancelInspectionAsync(Guid inspectionId, Guid authenticatedUserId)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(
                x => x.Id == inspectionId);

            if (inspection == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (inspection.CustomerId != authenticatedUserId)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.InspectionNotCustomer);

            if (inspection.Status == InspectionStatus.Completed || inspection.Status == InspectionStatus.Cancelled)
                return new BaseResponse<bool>(false, false, string.Empty, "Cannot cancel a completed or already cancelled inspection.");

            inspection.Status = InspectionStatus.Cancelled;
            await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);

            // Notify property owner
            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId);

            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId);

            if (property != null && customer != null)
            {
                var notification = new Notification(
                    property.OwnerId,
                    inspection.Id,
                    NotificationType.InspectionCancelled,
                    "Inspection Cancelled",
                    $"{customer.FirstName} {customer.LastName} has cancelled their inspection for \"{property.Title}\".");

                await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
                await PushRealtimeNotificationAsync(notification);
            }

            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.SetDeletedSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CancelInspectionAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }

    private async Task PushRealtimeNotificationAsync(Notification notification)
    {
        var dto = new NotificationDto(
            notification.Id,
            notification.DateCreated,
            notification.RecipientId,
            notification.InspectionId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.IsRead);

        await _realtimeNotifier.SendNotificationAsync(notification.RecipientId, dto);
    }
}
