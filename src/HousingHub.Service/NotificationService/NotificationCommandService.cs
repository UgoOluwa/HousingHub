using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.NotificationService;

public class NotificationCommandService : INotificationCommandService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<NotificationCommandService> _logger;

    public NotificationCommandService(IUnitOfWOrk unitOfWOrk, ILogger<NotificationCommandService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _logger = logger;
    }

    public async Task<BaseResponse<bool>> MarkAsReadAsync(Guid notificationId, Guid authenticatedUserId)
    {
        try
        {
            var notification = await _unitOfWOrk.NotificationQueries.GetByAsync(
                x => x.Id == notificationId,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (notification == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("notification"));

            if (notification.RecipientId != authenticatedUserId)
                return new BaseResponse<bool>(false, false, string.Empty, "You can only mark your own notifications as read.");

            notification.IsRead = true;
            _unitOfWOrk.NotificationCommands.Update(notification);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in MarkAsReadAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> MarkAllAsReadAsync(Guid authenticatedUserId)
    {
        try
        {
            var unread = await _unitOfWOrk.NotificationQueries.GetAllAsync(
                x => x.RecipientId == authenticatedUserId && !x.IsRead,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            foreach (var notification in unread)
            {
                notification.IsRead = true;
            }

            _unitOfWOrk.NotificationCommands.UpdateRange(unread);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in MarkAllAsReadAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }
}
