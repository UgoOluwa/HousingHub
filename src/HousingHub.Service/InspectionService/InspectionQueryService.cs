using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.InspectionService;

public class InspectionQueryService : IInspectionQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly ILogger<InspectionQueryService> _logger;
    private const string ClassName = "inspection";

    public InspectionQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<InspectionQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<InspectionDto?>> GetInspectionAsync(Guid id)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(x => x.Id == id);
            if (inspection is null)
                return new BaseResponse<InspectionDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            return new BaseResponse<InspectionDto?>(_mapper.Map<InspectionDto>(inspection), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto?>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<InspectionDto>>> GetInspectionsByPropertyAsync(Guid propertyId, int pageNumber, int pageSize, InspectionStatus? status = null)
    {
        try
        {
            System.Linq.Expressions.Expression<Func<PropertyInspection, bool>> predicate = status.HasValue
                ? x => x.PropertyId == propertyId && x.Status == status.Value
                : x => x.PropertyId == propertyId;

            var (inspections, totalCount) = await _unitOfWOrk.PropertyInspectionQueries.GetPagedAsync(pageNumber, pageSize, predicate: predicate);
            var mappedItems = _mapper.Map<List<InspectionDto>>(inspections);

            return new BaseResponse<PaginatedResult<InspectionDto>>(
                new PaginatedResult<InspectionDto>(mappedItems, totalCount, pageNumber, pageSize),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionsByPropertyAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<InspectionDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<InspectionDto>>> GetInspectionsByCustomerAsync(Guid customerId, int pageNumber, int pageSize, InspectionStatus? status = null)
    {
        try
        {
            System.Linq.Expressions.Expression<Func<PropertyInspection, bool>> predicate = status.HasValue
                ? x => x.CustomerId == customerId && x.Status == status.Value
                : x => x.CustomerId == customerId;

            var (inspections, totalCount) = await _unitOfWOrk.PropertyInspectionQueries.GetPagedAsync(pageNumber, pageSize, predicate: predicate);
            var mappedItems = _mapper.Map<List<InspectionDto>>(inspections);

            return new BaseResponse<PaginatedResult<InspectionDto>>(
                new PaginatedResult<InspectionDto>(mappedItems, totalCount, pageNumber, pageSize),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionsByCustomerAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<InspectionDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<OwnerInspectionDto>>> GetInspectionsByOwnerAsync(Guid ownerId, int pageNumber, int pageSize, InspectionStatus? status = null)
    {
        try
        {
            var properties = await _unitOfWOrk.PropertyQueries.GetAllAsync(p => p.OwnerId == ownerId);
            var propertyIds = properties.Select(p => p.Id).ToHashSet();

            if (propertyIds.Count == 0)
                return new BaseResponse<PaginatedResult<OwnerInspectionDto>>(
                    new PaginatedResult<OwnerInspectionDto>(new List<OwnerInspectionDto>(), 0, pageNumber, pageSize),
                    true, string.Empty, ResponseMessages.Successful);

            var inspections = status.HasValue
                ? await _unitOfWOrk.PropertyInspectionQueries.GetAllAsync(
                    i => propertyIds.Contains(i.PropertyId) && i.Status == status.Value)
                : await _unitOfWOrk.PropertyInspectionQueries.GetAllAsync(
                    i => propertyIds.Contains(i.PropertyId));

            var ordered = inspections.OrderByDescending(i => i.DateCreated).ToList();
            var totalCount = ordered.Count;

            var pagedInspections = ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            var pagedPropertyIds = pagedInspections.Select(i => i.PropertyId).ToHashSet();
            var propertyMap = properties.Where(p => pagedPropertyIds.Contains(p.Id)).ToDictionary(p => p.Id);

            var items = pagedInspections.Select(i =>
            {
                var property = propertyMap[i.PropertyId];
                return new OwnerInspectionDto(i.InspectionId, property.Title, property.Latitude, property.Longitude,
                    i.ScheduledDate, i.ScheduledTime, i.DateCreated, i.Status);
            }).ToList();

            return new BaseResponse<PaginatedResult<OwnerInspectionDto>>(
                new PaginatedResult<OwnerInspectionDto>(items, totalCount, pageNumber, pageSize),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionsByOwnerAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<OwnerInspectionDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<AdminInspectionListDto>>> GetAllInspectionsPaginatedAsync(AdminInspectionFilterDto filter)
    {
        try
        {
            var all = await _unitOfWOrk.PropertyInspectionQueries.GetAllAsync();
            IEnumerable<PropertyInspection> query = all;

            if (filter.Status.HasValue)
                query = query.Where(i => i.Status == filter.Status.Value);

            if (filter.Date.HasValue)
                query = query.Where(i => i.ScheduledDate.Date == filter.Date.Value.Date);

            if (filter.PropertyId.HasValue)
                query = query.Where(i => i.PropertyId == filter.PropertyId.Value);

            if (filter.CustomerId.HasValue)
                query = query.Where(i => i.CustomerId == filter.CustomerId.Value);

            var ordered = query.OrderByDescending(i => i.DateCreated).ToList();
            var totalCount = ordered.Count;

            var paged = ordered.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToList();

            // Enrich with property and customer info
            var propertyIds = paged.Select(i => i.PropertyId).Distinct().ToList();
            var customerIds = paged.Select(i => i.CustomerId).Distinct().ToList();

            var propertiesTask = _unitOfWOrk.PropertyQueries.GetAllAsync(p => propertyIds.Contains(p.Id));
            var customersTask = _unitOfWOrk.CustomerQueries.GetAllAsync(c => customerIds.Contains(c.Id));
            var addressesTask = _unitOfWOrk.PropertyAddressQueries.GetAllAsync(a => propertyIds.Contains(a.PropertyId));

            await Task.WhenAll(propertiesTask, customersTask, addressesTask);

            var propertyMap = propertiesTask.Result.ToDictionary(p => p.Id);
            var customerMap = customersTask.Result.ToDictionary(c => c.Id);
            var addressMap = addressesTask.Result.ToDictionary(a => a.PropertyId);

            var items = paged.Select(i =>
            {
                propertyMap.TryGetValue(i.PropertyId, out var prop);
                customerMap.TryGetValue(i.CustomerId, out var cust);
                addressMap.TryGetValue(i.PropertyId, out var addr);

                var address = addr != null ? $"{addr.Place}, {addr.City}, {addr.State}" : "N/A";
                var customerName = cust != null ? $"{cust.FirstName} {cust.LastName}" : "N/A";

                return new AdminInspectionListDto(
                    i.Id,
                    i.InspectionId,
                    prop?.Title ?? "N/A",
                    address,
                    i.PropertyId,
                    i.CustomerId,
                    customerName,
                    i.ScheduledDate,
                    i.ScheduledTime,
                    i.DateCreated,
                    i.Status,
                    i.Note,
                    i.DeclineNote);
            }).ToList();

            return new BaseResponse<PaginatedResult<AdminInspectionListDto>>(
                new PaginatedResult<AdminInspectionListDto>(items, totalCount, filter.PageNumber, filter.PageSize),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllInspectionsPaginatedAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<AdminInspectionListDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<AdminTodayInspectionDto>>> GetTodaysInspectionsPaginatedAsync(int pageNumber, int pageSize)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var all = await _unitOfWOrk.PropertyInspectionQueries.GetAllAsync(
                i => i.ScheduledDate.Date == today);

            var ordered = all.OrderBy(i => i.ScheduledTime).ToList();
            var totalCount = ordered.Count;

            var paged = ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var propertyIds = paged.Select(i => i.PropertyId).Distinct().ToList();
            var customerIds = paged.Select(i => i.CustomerId).Distinct().ToList();

            var propertiesTask = _unitOfWOrk.PropertyQueries.GetAllAsync(p => propertyIds.Contains(p.Id));
            var customersTask = _unitOfWOrk.CustomerQueries.GetAllAsync(c => customerIds.Contains(c.Id));
            var addressesTask = _unitOfWOrk.PropertyAddressQueries.GetAllAsync(a => propertyIds.Contains(a.PropertyId));

            await Task.WhenAll(propertiesTask, customersTask, addressesTask);

            var propertyMap = propertiesTask.Result.ToDictionary(p => p.Id);
            var customerMap = customersTask.Result.ToDictionary(c => c.Id);
            var addressMap = addressesTask.Result.ToDictionary(a => a.PropertyId);

            var items = paged.Select(i =>
            {
                propertyMap.TryGetValue(i.PropertyId, out var prop);
                customerMap.TryGetValue(i.CustomerId, out var cust);
                addressMap.TryGetValue(i.PropertyId, out var addr);

                var address = addr != null ? $"{addr.Place}, {addr.City}, {addr.State}" : "N/A";
                var customerName = cust != null ? $"{cust.FirstName} {cust.LastName}" : "N/A";
                var customerPhone = cust?.PhoneNumber ?? "N/A";

                return new AdminTodayInspectionDto(
                    i.Id,
                    i.InspectionId,
                    prop?.Title ?? "N/A",
                    address,
                    customerName,
                    customerPhone,
                    i.ScheduledDate,
                    i.ScheduledTime,
                    i.DateCreated,
                    i.Status);
            }).ToList();

            return new BaseResponse<PaginatedResult<AdminTodayInspectionDto>>(
                new PaginatedResult<AdminTodayInspectionDto>(items, totalCount, pageNumber, pageSize),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetTodaysInspectionsPaginatedAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<AdminTodayInspectionDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<AdminRecentActivityDto>>> GetRecentActivityAsync(int count = 20)
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-7);

            var customersTask = _unitOfWOrk.CustomerQueries.GetAllAsync(c => c.DateCreated >= since);
            var inspectionsTask = _unitOfWOrk.PropertyInspectionQueries.GetAllAsync(i => i.DateCreated >= since);
            var propertiesTask = _unitOfWOrk.PropertyQueries.GetAllAsync(p => p.DateCreated >= since);

            await Task.WhenAll(customersTask, inspectionsTask, propertiesTask);

            var activities = new List<AdminRecentActivityDto>();

            foreach (var c in customersTask.Result)
            {
                activities.Add(new AdminRecentActivityDto(
                    "CustomerJoined",
                    $"{c.FirstName} {c.LastName} joined the platform",
                    c.DateCreated,
                    c.Id));

                if (c.KycSubmittedAt.HasValue && c.KycSubmittedAt.Value >= since)
                    activities.Add(new AdminRecentActivityDto(
                        "KycSubmitted",
                        $"{c.FirstName} {c.LastName} submitted KYC documents",
                        c.KycSubmittedAt.Value,
                        c.Id));
            }

            foreach (var i in inspectionsTask.Result)
                activities.Add(new AdminRecentActivityDto(
                    "InspectionScheduled",
                    $"Inspection {i.InspectionId} scheduled for {i.ScheduledDate:dd MMM yyyy}",
                    i.DateCreated,
                    i.Id));

            foreach (var p in propertiesTask.Result)
                activities.Add(new AdminRecentActivityDto(
                    "PropertyListed",
                    $"Property '{p.Title}' was listed",
                    p.DateCreated,
                    p.Id));

            var result = activities
                .OrderByDescending(a => a.OccurredAt)
                .Take(count)
                .ToList();

            return new BaseResponse<List<AdminRecentActivityDto>>(result, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetRecentActivityAsync: {Message}", ex.Message);
            return new BaseResponse<List<AdminRecentActivityDto>>(null, false, string.Empty, ex.Message);
        }
    }
}
