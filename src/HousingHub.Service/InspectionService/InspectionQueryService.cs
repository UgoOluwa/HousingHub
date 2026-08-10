using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.AdminService;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.InspectionService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.InspectionService;

public class InspectionQueryService : IInspectionQueryService
{
    private readonly IUnitOfWOrk _unitOfWOrk;
    private readonly IMapper _mapper;
    private readonly IAdminAuthService _adminAuthService;
    private readonly ILogger<InspectionQueryService> _logger;
    private const string ClassName = "inspection";

    public InspectionQueryService(
        IUnitOfWOrk unitOfWOrk, IMapper mapper, IAdminAuthService adminAuthService, ILogger<InspectionQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _adminAuthService = adminAuthService;
        _logger = logger;
    }

    public async Task<BaseResponse<InspectionDto?>> GetInspectionAsync(Guid id, Guid authenticatedUserId)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(x => x.Id == id);
            if (inspection is null)
                return new BaseResponse<InspectionDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == inspection.PropertyId);
            bool isParticipant = inspection.CustomerId == authenticatedUserId || property?.OwnerId == authenticatedUserId;

            if (!isParticipant)
                return new BaseResponse<InspectionDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var imageUrl = await GetFirstPropertyImageUrlAsync(inspection.PropertyId);

            var customer = await _unitOfWOrk.CustomerQueries.GetByIdAsync(inspection.CustomerId);
            var owner = property != null ? await _unitOfWOrk.CustomerQueries.GetByIdAsync(property.OwnerId) : null;
            var assignedStaffName = await ResolveStaffNameAsync(inspection.AssignedStaffId);

            var dto = _mapper.Map<InspectionDto>(inspection) with
            {
                PropertyImageUrl = imageUrl,
                PropertyOwnerId = property?.OwnerId,
                CustomerName = customer != null ? $"{customer.FirstName} {customer.LastName}" : null,
                PropertyOwnerName = owner != null ? $"{owner.FirstName} {owner.LastName}" : null,
                AssignedStaffName = assignedStaffName
            };

            return new BaseResponse<InspectionDto?>(dto, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto?>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    private async Task<string?> ResolveStaffNameAsync(Guid? staffId)
    {
        if (!staffId.HasValue) return null;
        var staff = await _adminAuthService.GetAllStaffAsync();
        var match = staff.FirstOrDefault(s => s.Id == staffId.Value);
        return match != null ? $"{match.FirstName} {match.LastName}" : null;
    }

    public async Task<BaseResponse<InspectionDto?>> GetInspectionAsync(Guid id)
    {
        try
        {
            var inspection = await _unitOfWOrk.PropertyInspectionQueries.GetByAsync(x => x.Id == id);
            if (inspection is null)
                return new BaseResponse<InspectionDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            var imageUrl = await GetFirstPropertyImageUrlAsync(inspection.PropertyId);
            var property = await _unitOfWOrk.PropertyQueries.GetByAsync(x => x.Id == inspection.PropertyId);
            var customer = await _unitOfWOrk.CustomerQueries.GetByIdAsync(inspection.CustomerId);
            var owner = property != null ? await _unitOfWOrk.CustomerQueries.GetByIdAsync(property.OwnerId) : null;
            var assignedStaffName = await ResolveStaffNameAsync(inspection.AssignedStaffId);

            var dto = _mapper.Map<InspectionDto>(inspection) with
            {
                PropertyImageUrl = imageUrl,
                PropertyOwnerId = property?.OwnerId,
                CustomerName = customer != null ? $"{customer.FirstName} {customer.LastName}" : null,
                PropertyOwnerName = owner != null ? $"{owner.FirstName} {owner.LastName}" : null,
                AssignedStaffName = assignedStaffName
            };

            return new BaseResponse<InspectionDto?>(dto, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionAsync: {Message}", ex.Message);
            return new BaseResponse<InspectionDto?>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<PaginatedResult<InspectionDto>>> GetInspectionsByPropertyAsync(Guid propertyId, Guid requestingUserId, int pageNumber, int pageSize, InspectionStatus? status = null)
    {
        try
        {
            // These records expose the booking customer's id, visit times and notes,
            // so only the property's owner may list them. Report 'not found' rather
            // than 'forbidden' so the endpoint doesn't confirm which property ids exist.
            var property = await _unitOfWOrk.PropertyQueries.GetByIdAsync(propertyId);
            if (property is null || property.OwnerId != requestingUserId)
            {
                _logger.LogWarning(
                    "Rejected inspection listing for property {PropertyId} requested by {UserId}",
                    propertyId, requestingUserId);
                return new BaseResponse<PaginatedResult<InspectionDto>>(
                    null, false, string.Empty, ResponseMessages.SetNotFoundMessage("Property"));
            }

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
            return new BaseResponse<PaginatedResult<InspectionDto>>(null, false, string.Empty, ResponseMessages.UnexpectedError);
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
            var imageUrlByProperty = await GetPropertyImageUrlsAsync(inspections.Select(i => i.PropertyId));
            var mappedItems = _mapper.Map<List<InspectionDto>>(inspections)
                .Select(dto => dto with { PropertyImageUrl = imageUrlByProperty.GetValueOrDefault(dto.PropertyId) })
                .ToList();

            return new BaseResponse<PaginatedResult<InspectionDto>>(
                new PaginatedResult<InspectionDto>(mappedItems, totalCount, pageNumber, pageSize),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionsByCustomerAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<InspectionDto>>(null, false, string.Empty, ResponseMessages.UnexpectedError);
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

            // One indexed read per property, in parallel, then filter by status here.
            // Status has no index, so folding it into the query would have forced the
            // whole thing back to a table scan.
            var byProperty = await _unitOfWOrk.PropertyInspectionQueries.GetManyByAsync(
                i => i.PropertyId, propertyIds);

            var inspections = status.HasValue
                ? byProperty.Where(i => i.Status == status.Value)
                : byProperty;

            var ordered = inspections.OrderByDescending(i => i.DateCreated).ToList();
            var totalCount = ordered.Count;

            var pagedInspections = ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            var pagedPropertyIds = pagedInspections.Select(i => i.PropertyId).ToHashSet();
            var propertyMap = properties.Where(p => pagedPropertyIds.Contains(p.Id)).ToDictionary(p => p.Id);
            var imageUrlByProperty = await GetPropertyImageUrlsAsync(pagedPropertyIds);

            var items = pagedInspections.Select(i =>
            {
                var property = propertyMap[i.PropertyId];
                return new OwnerInspectionDto(i.Id, i.InspectionId, property.Title, property.Latitude, property.Longitude,
                    i.ScheduledDate, i.ScheduledTime, i.DateCreated, i.Status, imageUrlByProperty.GetValueOrDefault(i.PropertyId));
            }).ToList();

            return new BaseResponse<PaginatedResult<OwnerInspectionDto>>(
                new PaginatedResult<OwnerInspectionDto>(items, totalCount, pageNumber, pageSize),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetInspectionsByOwnerAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<OwnerInspectionDto>>(null, false, string.Empty, ResponseMessages.UnexpectedError);
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

            if (filter.HandedOff.HasValue)
                query = query.Where(i => (i.HandedOffAt != null) == filter.HandedOff.Value);

            if (filter.AssignedStaffId.HasValue)
                query = query.Where(i => i.AssignedStaffId == filter.AssignedStaffId.Value);

            var ordered = query.OrderByDescending(i => i.DateCreated).ToList();
            var totalCount = ordered.Count;

            var paged = ordered.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToList();

            // Enrich with property and customer info. Deliberately uses per-ID
            // GetByIdAsync (a fast DynamoDB GetItem on the primary key) instead of
            // GetAllAsync(predicate) — the generic repository's GetAllAsync always
            // scans the *entire* table then filters in memory regardless of the
            // predicate, so for a bounded page of ~20 inspections that was three full
            // table scans (Properties/Customers/PropertyAddresses) on every request.
            var propertyIds = paged.Select(i => i.PropertyId).Distinct().ToList();
            var customerIds = paged.Select(i => i.CustomerId).Distinct().ToList();

            var propertyTasks = propertyIds.Select(id => _unitOfWOrk.PropertyQueries.GetByIdAsync(id)).ToList();
            var customerTasks = customerIds.Select(id => _unitOfWOrk.CustomerQueries.GetByIdAsync(id)).ToList();

            await Task.WhenAll(propertyTasks.Cast<Task>().Concat(customerTasks));

            var propertyMap = propertyTasks.Select(t => t.Result).Where(p => p != null).ToDictionary(p => p!.Id);
            var customerMap = customerTasks.Select(t => t.Result).Where(c => c != null).ToDictionary(c => c!.Id);

            // Addresses are looked up by each property's own AddressId (also a fast
            // primary-key read) rather than scanning PropertyAddresses for matches.
            var addressIds = propertyMap.Values.Select(p => p.AddressId).Distinct().ToList();
            var addressTasks = addressIds.Select(id => _unitOfWOrk.PropertyAddressQueries.GetByIdAsync(id)).ToList();
            await Task.WhenAll(addressTasks);
            var addressById = addressTasks.Select(t => t.Result).Where(a => a != null).ToDictionary(a => a!.Id);

            // Only fetched when at least one item on this page is actually assigned —
            // a full staff scan isn't worth paying for on every unassigned inspection list.
            Dictionary<Guid, string> staffNameById = new();
            if (paged.Any(i => i.AssignedStaffId.HasValue))
            {
                var staff = await _adminAuthService.GetAllStaffAsync();
                staffNameById = staff.ToDictionary(s => s.Id, s => $"{s.FirstName} {s.LastName}");
            }

            var items = paged.Select(i =>
            {
                propertyMap.TryGetValue(i.PropertyId, out var prop);
                customerMap.TryGetValue(i.CustomerId, out var cust);
                var addr = prop != null && addressById.TryGetValue(prop.AddressId, out var a) ? a : null;

                var address = addr != null ? $"{addr.Place}, {addr.City}, {addr.State}" : "N/A";
                var customerName = cust != null ? $"{cust.FirstName} {cust.LastName}" : "N/A";
                var assignedStaffName = i.AssignedStaffId.HasValue
                    ? staffNameById.GetValueOrDefault(i.AssignedStaffId.Value)
                    : null;

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
                    i.DeclineNote,
                    i.HandedOffAt,
                    i.AssignedStaffId,
                    assignedStaffName);
            }).ToList();

            return new BaseResponse<PaginatedResult<AdminInspectionListDto>>(
                new PaginatedResult<AdminInspectionListDto>(items, totalCount, filter.PageNumber, filter.PageSize),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllInspectionsPaginatedAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<AdminInspectionListDto>>(null, false, string.Empty, ResponseMessages.UnexpectedError);
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

            var propertiesTask = _unitOfWOrk.PropertyQueries.GetManyByAsync(p => p.Id, propertyIds);
            var customersTask = _unitOfWOrk.CustomerQueries.GetManyByAsync(c => c.Id, customerIds);
            var addressesTask = _unitOfWOrk.PropertyAddressQueries.GetManyByAsync(a => a.PropertyId, propertyIds);

            await Task.WhenAll(propertiesTask, customersTask, addressesTask);

            var propertyMap = propertiesTask.Result.ToDictionary(p => p.Id);
            var customerMap = customersTask.Result.ToDictionary(c => c.Id);
            // Nothing enforces one address row per property, and a straight ToDictionary
            // throws on the second one — turning a stray duplicate row into a 500 on the
            // admin inspection list. Group and take the first instead. (This was equally
            // true before; the read changed, the latent fragility did not.)
            var addressMap = addressesTask.Result
                .GroupBy(a => a.PropertyId)
                .ToDictionary(g => g.Key, g => g.First());

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
            return new BaseResponse<PaginatedResult<AdminTodayInspectionDto>>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    private async Task<string?> GetFirstPropertyImageUrlAsync(Guid propertyId)
    {
        var files = await _unitOfWOrk.PropertyFileQueries.GetAllAsync(f => f.PropertyId == propertyId);
        return files.OrderBy(f => f.DateUploaded).FirstOrDefault()?.FileUrl;
    }

    private async Task<Dictionary<Guid, string?>> GetPropertyImageUrlsAsync(IEnumerable<Guid> propertyIds)
    {
        var ids = propertyIds.ToHashSet();
        if (ids.Count == 0)
            return new Dictionary<Guid, string?>();

        var files = await _unitOfWOrk.PropertyFileQueries.GetManyByAsync(f => f.PropertyId, ids);
        return files
            .GroupBy(f => f.PropertyId)
            .ToDictionary(g => g.Key, g => (string?)g.OrderBy(f => f.DateUploaded).First().FileUrl);
    }

    public async Task<BaseResponse<List<AdminRecentActivityDto>>> GetRecentActivityAsync(int count = 20, int days = 7)
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-days);

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
            return new BaseResponse<List<AdminRecentActivityDto>>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }
}
