using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.CustomerAddressService.Interfaces;
using HousingHub.Service.Dtos.CustomerAddress;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.CustomerAddressService;

public class CustomerAddressCommandService : ICustomerAddressCommandService
{
    public readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<CustomerAddressCommandService> _logger;
    private const string ClassName = "customer address";
    private readonly IMapper _mapper;

    public CustomerAddressCommandService(ILogger<CustomerAddressCommandService> logger, IUnitOfWOrk unitOfWOrk, IMapper mapper)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
    }

    public async Task<BaseResponse<CustomerAddressDto>> CreateCustomerAddress(CreateCustomerAddressDto request)
    {
        try
        {
            // Check for existing customer address could be added here
            bool existingAddress = await _unitOfWOrk.CustomerAddressQueries.AnyAsync(x => x.CustomerId == request.CustomerId);
            if (existingAddress)
            {
                return new BaseResponse<CustomerAddressDto>(null, false, string.Empty, ResponseMessages.SetAlreadyExistsMessage(ClassName));
            }

            var newEntity = new CustomerAddress(request.Street, request.City, request.State, request.Country, request.PostalCode, request.CustomerId);
            bool isSuccessful = await _unitOfWOrk.CustomerAddressCommands.InsertAsync(newEntity);
            if (!isSuccessful)
            {
                return new BaseResponse<CustomerAddressDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));
            }

            await _unitOfWOrk.SaveAsync();

            CustomerAddressDto response = _mapper.Map<CustomerAddressDto>(newEntity);
            return new BaseResponse<CustomerAddressDto>(response, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreateCustomerAddress: {Message}", ex.Message);
            return new BaseResponse<CustomerAddressDto>(null, false, string.Empty, ex.Message);
        }
    }
}
