using AutoMapper;
using Microsoft.Extensions.Logging;
using HousingHub.Core.CustomResponses;
using HousingHub.Service.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.CustomerAddressService.Interfaces;
using HousingHub.Service.Dtos.CustomerAddress;
using HousingHub.Data.RepositoryInterfaces.Common;

namespace HousingHub.Service.CustomerService;

public class CustomerAddressQueryService : ICustomerAddressQueryService
{
    public readonly IUnitOfWOrk _unitOfWOrk;
    public readonly IMapper _mapper;
    private readonly ILogger<CustomerAddressQueryService> _logger;
    private const string ClassName = "customer";

    public CustomerAddressQueryService(IUnitOfWOrk unitOfWOrk, IMapper mapper, ILogger<CustomerAddressQueryService> logger)
    {
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<CustomerAddressDto?>> GetAddressAsync(Guid id)
    {
        try
        {
            CustomerAddress? customerAddress = await _unitOfWOrk.CustomerAddressQueries.GetByAsync(x => x.Id == id, new FindOptions() { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });
            if (customerAddress is null) {
                return new BaseResponse<CustomerAddressDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<CustomerAddressDto?>(_mapper.Map<CustomerAddressDto>(customerAddress), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "An error occurred in GetAddressAsync: {Message}", ex.Message);
            return new BaseResponse<CustomerAddressDto?>(null, false, string.Empty, ex.Message);
        }       
    }

    public async Task<BaseResponse<List<CustomerAddressDto>>> GetAllCustomerAddressesAsync()
    {
        try
        {
            IEnumerable<CustomerAddress> customerAddresses = await _unitOfWOrk.CustomerAddressQueries.GetAllAsync(new FindOptions() { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });
            if (!customerAddresses.Any())
            {
                return new BaseResponse<List<CustomerAddressDto>>(new List<CustomerAddressDto>(), false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<List<CustomerAddressDto>>(_mapper.Map<List<CustomerAddressDto>>(customerAddresses), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetAllCustomerAddressesAsync: {Message}", ex.Message);
            return new BaseResponse<List<CustomerAddressDto>>(new List<CustomerAddressDto>(), false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<CustomerAddressDto?>> GetCustomerAddressByCustomerIdAsync(Guid customerId)
    {
        try
        {
            CustomerAddress? customerAddress = await _unitOfWOrk.CustomerAddressQueries.GetByAsync(x => x.CustomerId == customerId, new FindOptions() { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });
            if (customerAddress is null)
            {
                return new BaseResponse<CustomerAddressDto?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            return new BaseResponse<CustomerAddressDto?>(_mapper.Map<CustomerAddressDto>(customerAddress), true, string.Empty, ResponseMessages.Successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in GetCustomerAddressByCustomerIdAsync: {Message}", ex.Message);
            return new BaseResponse<CustomerAddressDto?>(null, false, string.Empty, ex.Message);
        }
    }
}
