using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Notification;
using HousingHub.Service.NotificationService.Interfaces;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.NotificationService;

public class NotificationQueryService : INotificationQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly ILogger<NotificationQueryService> _logger;

    public NotificationQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<NotificationQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<PaginatedResult<NotificationDto>>> GetNotificationsAsync(Guid recipientId, int pageNumber, int pageSize, bool? unreadOnly = null)
    {
        try
        {
            System.Linq.Expressions.Expression<Func<Notification, bool>> predicate = unreadOnly == true
                ? x => x.RecipientId == recipientId && !x.IsRead
                : x => x.RecipientId == recipientId;

            var (notifications, totalCount) = await _unitOfWOrk.NotificationQueries.GetPagedAsync(
                pageNumber, pageSize,
                predicate: predicate,
                findOptions: new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            var mappedItems = _mapper.Map<List<NotificationDto>>(notifications);
            var paginatedResult = new PaginatedResult<NotificationDto>(mappedItems, totalCount, pageNumber, pageSize);

            return new BaseResponse<PaginatedResult<NotificationDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetNotificationsAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<NotificationDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<int>> GetUnreadCountAsync(Guid recipientId)
    {
        try
        {
            var count = await _unitOfWOrk.NotificationQueries.CountAsync(
                x => x.RecipientId == recipientId && !x.IsRead);

            return new BaseResponse<int>(count, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetUnreadCountAsync: {Message}", ex.Message);
            return new BaseResponse<int>(0, false, string.Empty, ex.Message);
        }
    }
}
