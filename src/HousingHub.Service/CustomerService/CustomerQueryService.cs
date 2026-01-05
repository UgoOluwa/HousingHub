using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.RepositoryInterfaces.Common;
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
            Customer? customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == id, new FindOptions() { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });
            if (customer is null) {
                return new BaseResponse<CustomerWithDetailsDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<CustomerWithDetailsDto?>(_mapper.Map<CustomerWithDetailsDto>(customer), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "An error occurred in GetCustomerAsync: {Message}", ex.Message);
            return new BaseResponse<CustomerWithDetailsDto?>(null, false, string.Empty, ex.Message);
        }       
    }

    public async Task<BaseResponse<List<CustomerDto>>> GetAllCustomersAsync()
    {
        try
        {
            IEnumerable<Customer> customers = await _unitOfWOrk.CustomerQueries.GetAllAsync(new FindOptions() { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });
            if (!customers.Any())
            {
                return new BaseResponse<List<CustomerDto>>(new List<CustomerDto>(), false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<List<CustomerDto>>(_mapper.Map<List<CustomerDto>>(customers), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex) { 
            _logger.LogError(ex, "An error occurred in GetAllCustomersAsync: {Message}", ex.Message);
            return new BaseResponse<List<CustomerDto>>(new List<CustomerDto>(), false, string.Empty, ex.Message);
        }
    }
}
