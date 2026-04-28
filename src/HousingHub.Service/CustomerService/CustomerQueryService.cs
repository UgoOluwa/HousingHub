using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Admin;
using HousingHub.Service.Dtos.Customer;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.CustomerService;

public class CustomerQueryService : ICustomerQueryService
{
    public readonly IUnitOfWOrk _unitOfWOrk;
    public readonly IMapper _mapper;
    private readonly ILogger<CustomerQueryService> _logger;
    private const string ClassName = "customer";

    public CustomerQueryService(IUnitOfWOrk unitOfWork, IMapper mapper, ILogger<CustomerQueryService> logger)
    {
        _unitOfWOrk = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<CustomerWithDetailsDto?>> GetCustomerAsync(Guid id)
    {
        try
        {
            Customer? customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == id);
            if (customer is null)
                return new BaseResponse<CustomerWithDetailsDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            return new BaseResponse<CustomerWithDetailsDto?>(_mapper.Map<CustomerWithDetailsDto>(customer), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetCustomerAsync: {Message}", ex.Message);
            return new BaseResponse<CustomerWithDetailsDto?>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<List<CustomerDto>>> GetAllCustomersAsync()
    {
        try
        {
            IEnumerable<Customer> customers = await _unitOfWOrk.CustomerQueries.GetAllAsync();
            if (!customers.Any())
                return new BaseResponse<List<CustomerDto>>(new List<CustomerDto>(), false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            return new BaseResponse<List<CustomerDto>>(_mapper.Map<List<CustomerDto>>(customers), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllCustomersAsync: {Message}", ex.Message);
            return new BaseResponse<List<CustomerDto>>(new List<CustomerDto>(), false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<CustomerDto>>> GetAllCustomersPaginatedAsync(int pageNumber, int pageSize)
    {
        try
        {
            var (customers, totalCount) = await _unitOfWOrk.CustomerQueries.GetPagedAsync(pageNumber, pageSize);
            var mappedItems = _mapper.Map<List<CustomerDto>>(customers);
            var paginatedResult = new PaginatedResult<CustomerDto>(mappedItems, totalCount, pageNumber, pageSize);

            return new BaseResponse<PaginatedResult<CustomerDto>>(paginatedResult, true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllCustomersPaginatedAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<CustomerDto>>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<PaginatedResult<AdminCustomerListDto>>> GetCustomersFilteredAsync(
        AdminCustomerFilterDto filter, CustomerType? typeFlag = null)
    {
        try
        {
            // Load all customers (DynamoDB full scan) then filter in memory
            var all = await _unitOfWOrk.CustomerQueries.GetAllAsync();

            // Scope by CustomerType flag if provided
            IEnumerable<Customer> query = typeFlag.HasValue
                ? all.Where(c => c.CustomerType.HasFlag(typeFlag.Value) && !c.CustomerType.HasFlag(CustomerType.Admin))
                : all.Where(c => !c.CustomerType.HasFlag(CustomerType.Admin));

            // Search by name / email / phone
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLowerInvariant();
                query = query.Where(c =>
                    c.FirstName.ToLowerInvariant().Contains(term) ||
                    c.LastName.ToLowerInvariant().Contains(term) ||
                    c.Email.ToLowerInvariant().Contains(term) ||
                    (c.PhoneNumber != null && c.PhoneNumber.Contains(term)));
            }

            if (filter.IsVerified.HasValue)
                query = query.Where(c => c.IsKycVerified == filter.IsVerified.Value);

            if (filter.IsActive.HasValue)
                query = query.Where(c => c.IsActive == filter.IsActive.Value);

            var list = query.OrderByDescending(c => c.DateCreated).ToList();
            var totalCount = list.Count;

            // Load all pending inspections in one go to avoid N+1
            var pendingInspections = await _unitOfWOrk.PropertyInspectionQueries
                .GetAllAsync(i => i.Status == InspectionStatus.Pending);
            var pendingCountByCustomer = pendingInspections
                .GroupBy(i => i.CustomerId)
                .ToDictionary(g => g.Key, g => g.Count());

            var paged = list
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(c => new AdminCustomerListDto(
                    c.Id,
                    c.FirstName,
                    c.LastName,
                    c.Email,
                    c.PhoneNumber,
                    c.DateCreated,
                    c.IsActive,
                    c.IsKycVerified,
                    c.KycSubmittedAt.HasValue && !c.IsKycVerified,
                    (int)c.CustomerType,
                    pendingCountByCustomer.GetValueOrDefault(c.Id, 0)))
                .ToList();

            return new BaseResponse<PaginatedResult<AdminCustomerListDto>>(
                new PaginatedResult<AdminCustomerListDto>(paged, totalCount, filter.PageNumber, filter.PageSize),
                true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetCustomersFilteredAsync: {Message}", ex.Message);
            return new BaseResponse<PaginatedResult<AdminCustomerListDto>>(null, false, string.Empty, ex.Message);
        }
    }
}
