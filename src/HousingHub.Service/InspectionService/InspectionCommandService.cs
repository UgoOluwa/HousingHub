using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.InspectionService;

public class InspectionCommandService : IInspectionCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly IEmailService _emailService;
    private readonly ILogger<InspectionCommandService> _logger;
    private const string ClassName = "inspection";

    public InspectionCommandService(
        IUnitOfWOrk unitOfWOrk,
        IMapper mapper,
        IEmailService emailService,
        ILogger<InspectionCommandService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<BaseResponse<InspectionDto>> ScheduleInspectionAsync(ScheduleInspectionDto request, Guid authenticatedUserId)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (customer == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == request.PropertyId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

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
                x => x.Id == property.OwnerId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (owner != null)
            {
                var notification = new Notification(
                    owner.Id,
                    inspection.Id,
                    NotificationType.InspectionScheduled,
                    "New Inspection Request",
                    $"{customer.FirstName} {customer.LastName} has requested an inspection for your property \"{property.Title}\" on {request.ScheduledDate:yyyy-MM-dd} at {request.ScheduledTime:hh\\:mm}.");

                await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
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
                x => x.Id == request.InspectionId,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (inspection == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

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

            _unitOfWOrk.PropertyInspectionCommands.Update(inspection);

            // Notify customer (in-app)
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == inspection.CustomerId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

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
                x => x.Id == request.InspectionId,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (inspection == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (property == null)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage("property"));

            if (property.OwnerId != authenticatedUserId)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotOwner);

            if (inspection.Status != InspectionStatus.Pending)
                return new BaseResponse<InspectionDto>(null, false, string.Empty, ResponseMessages.InspectionNotPending);

            inspection.Status = InspectionStatus.Rescheduled;
            inspection.RescheduledDate = request.RescheduledDate;
            inspection.RescheduledTime = request.RescheduledTime;
            inspection.RescheduleNote = request.Note;

            _unitOfWOrk.PropertyInspectionCommands.Update(inspection);

            // Notify customer (in-app)
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == inspection.CustomerId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (customer != null)
            {
                var notification = new Notification(
                    customer.Id,
                    inspection.Id,
                    NotificationType.InspectionRescheduled,
                    "Inspection Rescheduled",
                    $"The property owner has rescheduled your inspection for \"{property.Title}\" to {request.RescheduledDate:yyyy-MM-dd} at {request.RescheduledTime:hh\\:mm}.{(string.IsNullOrWhiteSpace(request.Note) ? "" : $" Note: {request.Note}")}");

                await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
            }

            await _unitOfWOrk.SaveAsync();

            // Notify customer (email)
            if (customer != null && owner != null)
            {
                _ = _emailService.SendInspectionResponseAsync(
                    customer.Email, customer.FirstName,
                    $"{owner.FirstName} {owner.LastName}",
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
                x => x.Id == inspectionId,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

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

            _unitOfWOrk.PropertyInspectionCommands.Update(inspection);

            // Notify property owner (in-app)
            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (property != null && customer != null)
            {
                var owner = await _unitOfWOrk.CustomerQueries.GetByAsync(
                    x => x.Id == property.OwnerId,
                    new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

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
                x => x.Id == inspectionId,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (inspection == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (inspection.CustomerId != authenticatedUserId)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.InspectionNotCustomer);

            if (inspection.Status == InspectionStatus.Completed || inspection.Status == InspectionStatus.Cancelled)
                return new BaseResponse<bool>(false, false, string.Empty, "Cannot cancel a completed or already cancelled inspection.");

            inspection.Status = InspectionStatus.Cancelled;
            _unitOfWOrk.PropertyInspectionCommands.Update(inspection);

            // Notify property owner
            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(
                x => x.Id == inspection.PropertyId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(
                x => x.Id == authenticatedUserId,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (property != null && customer != null)
            {
                var notification = new Notification(
                    property.OwnerId,
                    inspection.Id,
                    NotificationType.InspectionCancelled,
                    "Inspection Cancelled",
                    $"{customer.FirstName} {customer.LastName} has cancelled their inspection for \"{property.Title}\".");

                await _unitOfWOrk.NotificationCommands.InsertAsync(notification);
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
}
