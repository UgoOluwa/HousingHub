using Amazon.DynamoDBv2.DataModel;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Core;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.AdminService;
using HousingHub.Service.ChatService.Interfaces;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Chat;
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
    private readonly IAdminAuthService _adminAuthService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly IDynamoDBContext _dynamoDb;
    private readonly ILogger<InspectionCommandService> _logger;
    private const string ClassName = "inspection";

    public InspectionCommandService(
        IUnitOfWOrk unitOfWOrk,
        IMapper mapper,
        IEmailService emailService,
        IAdminAuthService adminAuthService,
        IRealtimeNotifier realtimeNotifier,
        IChatRealtimeNotifier chatRealtimeNotifier,
        IDynamoDBContext dynamoDb,
        ILogger<InspectionCommandService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _emailService = emailService;
        _adminAuthService = adminAuthService;
        _realtimeNotifier = realtimeNotifier;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _dynamoDb = dynamoDb;
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

            bool hasPendingRequest = await _unitOfWOrk.PropertyInspectionQueries.AnyAsync(
                x => x.CustomerId == authenticatedUserId && x.PropertyId == request.PropertyId && x.Status == InspectionStatus.Pending);

            if (hasPendingRequest)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionAlreadyPending);

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

            // Notify customer (in-app) that their request was submitted
            var customerNotification = new Notification(
                customer.Id,
                inspection.Id,
                NotificationType.InspectionScheduled,
                "Inspection Request Submitted",
                $"Your inspection request for \"{property.Title}\" on {request.ScheduledDate:yyyy-MM-dd} at {request.ScheduledTime:hh\\:mm} has been submitted.");

            await _unitOfWOrk.NotificationCommands.InsertAsync(customerNotification);
            await PushRealtimeNotificationAsync(customerNotification);

            await _unitOfWOrk.SaveAsync();

            // Notify property owner (email - fire and forget)
            if (owner != null)
            {
                _ = _emailService.SendInspectionScheduledAsync(
                    owner.Email, owner.FirstName,
                    $"{customer.FirstName} {customer.LastName}",
                    property.Title, request.ScheduledDate, request.ScheduledTime, request.Note);
            }

            // Notify customer (email - fire and forget)
            _ = _emailService.SendInspectionBookingConfirmationAsync(
                customer.Email, customer.FirstName,
                property.Title, request.ScheduledDate, request.ScheduledTime, request.Note);

            return new BaseResponse<InspectionDto>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in ScheduleInspectionAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<InspectionDto>> RespondToInspectionAsync(RespondToInspectionDto request, Guid authenticatedUserId, bool isAdminAction = false)
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

            if (!isAdminAction && property.OwnerId != authenticatedUserId)
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
                x => x.Id == property.OwnerId);

            // Post an automated confirmation into the owner-customer conversation,
            // attributed to the platform (not personally to the owner) and visible
            // to both parties.
            if (request.Accept && owner != null && customer != null)
            {
                await PostSystemChatMessageAsync(
                    owner, customer,
                    $"Your inspection for \"{property.Title}\" has been confirmed for {inspection.ScheduledDate:yyyy-MM-dd} at {DateTime.Today.Add(inspection.ScheduledTime):hh:mm tt}. Please arrive on time and bring a valid ID. Contact the other party via this chat if you have any questions.");
            }

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
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<InspectionDto>> HandOffToHousingHubAsync(Guid inspectionId, Guid ownerId)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(
                x => x.Id == inspectionId);

            if (inspection == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId);

            if (property == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("property"));

            if (property.OwnerId != ownerId)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotOwner);

            if (inspection.HandedOffAt != null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionAlreadyHandedOff);

            if (inspection.Status is InspectionStatus.Declined or InspectionStatus.Completed or InspectionStatus.Cancelled)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionCannotHandOff);

            inspection.HandedOffAt = DateTime.UtcNow;
            await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);
            await _unitOfWOrk.SaveAsync();

            var owner = await _unitOfWOrk.CustomerQueries.GetByIdAsync(ownerId);
            string ownerName = owner != null ? $"{owner.FirstName} {owner.LastName}" : "A property owner";

            var staff = await _adminAuthService.GetAllStaffAsync();
            var superAdmins = staff.Where(a => a.Role == AdminRoles.SuperAdmin && a.IsActive);

            foreach (var admin in superAdmins)
            {
                try
                {
                    await _emailService.SendInspectionHandoffToAdminsAsync(
                        admin.Email, admin.FirstName, ownerName, property.Title,
                        inspection.ScheduledDate, inspection.ScheduledTime);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send handoff notification email to {Email}", admin.Email);
                }
            }

            return new BaseResponse<InspectionDto>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in HandOffToHousingHubAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<InspectionDto>> AssignInspectionToStaffAsync(Guid inspectionId, Guid staffAdminId, Guid callerAdminId)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(
                x => x.Id == inspectionId);

            if (inspection == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (inspection.HandedOffAt == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotHandedOff);

            var staff = await _adminAuthService.GetAllStaffAsync();
            var assignee = staff.FirstOrDefault(a => a.Id == staffAdminId);
            if (assignee == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("staff member"));

            inspection.AssignedStaffId = staffAdminId;
            await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);
            await _unitOfWOrk.SaveAsync();

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == inspection.PropertyId);
            var owner = property != null ? await _unitOfWOrk.CustomerQueries.GetByIdAsync(property.OwnerId) : null;
            string ownerName = owner != null ? $"{owner.FirstName} {owner.LastName}" : "the property owner";

            try
            {
                await _emailService.SendStaffAssignedToInspectionAsync(
                    assignee.Email, assignee.FirstName, property?.Title ?? "a property", ownerName,
                    inspection.ScheduledDate, inspection.ScheduledTime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send staff-assigned notification email to {Email}", assignee.Email);
            }

            return new BaseResponse<InspectionDto>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in AssignInspectionToStaffAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<InspectionDto>> RescheduleInspectionAsync(RescheduleInspectionDto request, Guid authenticatedUserId, bool isAdminAction = false)
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

            if (!isAdminAction && !isOwner && !isCustomer)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotParticipant);

            if (inspection.Status != InspectionStatus.Pending && inspection.Status != InspectionStatus.Confirmed)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionCannotReschedule);

            inspection.Status = InspectionStatus.Rescheduled;
            inspection.RescheduledDate = request.RescheduledDate;
            inspection.RescheduledTime = request.RescheduledTime;
            inspection.RescheduleNote = request.Note;

            await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);

            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == inspection.CustomerId);
            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == property.OwnerId);

            // A staff member isn't one of the inspection's two original participants,
            // so "notify the other party" doesn't apply — notify both instead.
            string initiatorName;
            var notifyRecipients = new List<Customer>();
            if (isAdminAction)
            {
                var admin = await _dynamoDb.LoadAsync<Admin>(authenticatedUserId);
                initiatorName = admin != null ? $"Admin - {admin.FirstName} {admin.LastName}" : "HousingHub";
                if (customer != null) notifyRecipients.Add(customer);
                if (owner != null) notifyRecipients.Add(owner);
            }
            else
            {
                var initiator = isOwner ? owner : customer;
                initiatorName = initiator != null ? $"{initiator.FirstName} {initiator.LastName}" : "A user";
                var otherParty = isOwner ? customer : owner;
                if (otherParty != null) notifyRecipients.Add(otherParty);
            }

            string role = isAdminAction ? "HousingHub team" : isOwner ? "property owner" : "requester";

            foreach (var recipient in notifyRecipients)
            {
                var notification = new Notification(
                    recipient.Id,
                    inspection.Id,
                    NotificationType.InspectionRescheduled,
                    "Inspection Rescheduled",
                    $"The {role} has rescheduled the inspection for \"{property.Title}\" to {request.RescheduledDate:yyyy-MM-dd} at {request.RescheduledTime:hh\\:mm}.{(string.IsNullOrWhiteSpace(request.Note) ? "" : $" Note: {request.Note}")}");

                await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
                await PushRealtimeNotificationAsync(notification);
            }

            await _unitOfWOrk.SaveAsync();

            foreach (var recipient in notifyRecipients)
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
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<InspectionDto>> RespondToRescheduleAsync(Guid inspectionId, bool accept, Guid authenticatedUserId, string? note = null)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(
                x => x.Id == inspectionId);

            if (inspection == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId);

            if (property == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("property"));

            bool isOwner = property.OwnerId == authenticatedUserId;
            bool isCustomer = inspection.CustomerId == authenticatedUserId;

            // Either party can propose a reschedule, so either party must be able to
            // respond to one — this used to be hardcoded to the customer only, which
            // left owners with no way to respond to a customer-proposed reschedule.
            if (!isOwner && !isCustomer)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotParticipant);

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
                // Rejecting the proposed time isn't the same as cancelling the
                // inspection — it reverts to the original (already-unchanged)
                // scheduled date/time, still confirmed.
                inspection.Status = InspectionStatus.Confirmed;
                inspection.DeclineNote = note;
            }

            await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);

            // Notify whichever party didn't just respond
            var responder = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == authenticatedUserId);
            Guid recipientId = isOwner ? inspection.CustomerId : property.OwnerId;
            var recipient = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == recipientId);

            if (responder != null && recipient != null)
            {
                string action = accept ? "accepted" : "declined";
                string responderRole = isOwner ? "property owner" : "customer";
                var notificationType = accept ? NotificationType.InspectionConfirmed : NotificationType.InspectionDeclined;
                var notification = new Notification(
                    recipient.Id,
                    inspection.Id,
                    notificationType,
                    $"Reschedule {(accept ? "Accepted" : "Declined")}",
                    $"The {responderRole} ({responder.FirstName} {responder.LastName}) has {action} the proposed reschedule for \"{property.Title}\"." +
                    (string.IsNullOrWhiteSpace(note) ? "" : $" Note: {note}"));

                await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
                await PushRealtimeNotificationAsync(notification);
            }

            await _unitOfWOrk.SaveAsync();

            if (recipient != null && responder != null)
            {
                _ = _emailService.SendInspectionResponseAsync(
                    recipient.Email, recipient.FirstName,
                    $"{responder.FirstName} {responder.LastName}",
                    property.Title, accept ? "Confirmed" : "Declined", note, null, null);
            }

            return new BaseResponse<InspectionDto>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in RespondToRescheduleAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> CancelInspectionAsync(Guid inspectionId, Guid authenticatedUserId, bool isAdminAction = false)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(
                x => x.Id == inspectionId);

            if (inspection == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (!isAdminAction && inspection.CustomerId != authenticatedUserId)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.InspectionNotCustomer);

            if (inspection.Status == InspectionStatus.Completed || inspection.Status == InspectionStatus.Cancelled)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.InspectionCannotCancel);

            inspection.Status = InspectionStatus.Cancelled;
            await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);

            // Notify property owner
            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId);

            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == inspection.CustomerId);

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
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<int>> SendDueInspectionRemindersAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var reminderCutoff = now.AddHours(24);

            var candidates = await _unitOfWOrk.PropertyInspectionQueries.GetAllAsync(
                i => i.Status == InspectionStatus.Confirmed && i.ReminderSentAt == null);

            int sentCount = 0;

            foreach (var inspection in candidates)
            {
                var scheduledAt = inspection.ScheduledDate.Date + inspection.ScheduledTime;

                // Due within the next 24 hours, and not already in the past.
                if (scheduledAt <= now || scheduledAt > reminderCutoff)
                    continue;

                var property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == inspection.PropertyId);
                if (property == null) continue;

                var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == inspection.CustomerId);
                var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == property.OwnerId);
                if (customer == null || owner == null) continue;

                string ownerName = $"{owner.FirstName} {owner.LastName}";
                string customerName = $"{customer.FirstName} {customer.LastName}";

                _ = _emailService.SendInspectionReminderAsync(
                    customer.Email, customer.FirstName, ownerName, property.Title, inspection.ScheduledDate, inspection.ScheduledTime);
                _ = _emailService.SendInspectionReminderAsync(
                    owner.Email, owner.FirstName, customerName, property.Title, inspection.ScheduledDate, inspection.ScheduledTime);

                await PostSystemChatMessageAsync(
                    owner, customer,
                    $"Reminder: your inspection for \"{property.Title}\" is scheduled for {inspection.ScheduledDate:yyyy-MM-dd} at {DateTime.Today.Add(inspection.ScheduledTime):hh:mm tt} — about 24 hours from now. Please arrive on time and bring a valid ID.");

                inspection.ReminderSentAt = now;
                await _unitOfWOrk.PropertyInspectionCommands.UpdateAsync(inspection);

                sentCount++;
            }

            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<int>(sentCount, true, string.Empty, ResponseMessages.InspectionRemindersSent(sentCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in SendDueInspectionRemindersAsync: {Message}", ex.Message);
            return new BaseResponse<int>(0, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>Posts an automated message (attributed to Admin, not either user) into the owner-customer conversation, live to both.</summary>
    private async Task PostSystemChatMessageAsync(Customer owner, Customer customer, string content)
    {
        var conversation = await _unitOfWOrk.ConversationQueries.GetByAsync(
            c => (c.ParticipantOneId == owner.Id && c.ParticipantTwoId == customer.Id) ||
                 (c.ParticipantOneId == customer.Id && c.ParticipantTwoId == owner.Id));

        if (conversation == null)
        {
            conversation = new Conversation(owner.Id, customer.Id);
            await _unitOfWOrk.ConversationCommands.InsertAsync(conversation);
        }

        var chatMessage = new ChatMessage(conversation.Id, SystemSender.Id, content);
        await _unitOfWOrk.ChatMessageCommands.InsertAsync(chatMessage);

        conversation.LastMessage = content.Length > 100 ? content[..100] + "..." : content;
        conversation.LastMessageAt = chatMessage.DateCreated;
        await _unitOfWOrk.ConversationCommands.UpdateAsync(conversation);

        var chatMessageDto = new ChatMessageDto(
            chatMessage.Id, chatMessage.ConversationId, chatMessage.SenderId,
            SystemSender.DisplayName, chatMessage.Content, chatMessage.IsRead, chatMessage.DateCreated,
            IsSystemMessage: true);

        _ = _chatRealtimeNotifier.SendMessageAsync(customer.Id, chatMessageDto);
        _ = _chatRealtimeNotifier.SendMessageAsync(owner.Id, chatMessageDto);

        string ownerName = $"{owner.FirstName} {owner.LastName}";
        string customerName = $"{customer.FirstName} {customer.LastName}";

        _ = _chatRealtimeNotifier.NotifyConversationUpdatedAsync(customer.Id, new ConversationDto(
            conversation.Id, owner.Id, ownerName, conversation.LastMessage, conversation.LastMessageAt, 1));
        _ = _chatRealtimeNotifier.NotifyConversationUpdatedAsync(owner.Id, new ConversationDto(
            conversation.Id, customer.Id, customerName, conversation.LastMessage, conversation.LastMessageAt, 1));
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
