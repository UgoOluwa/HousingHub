using AutoMapper;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.CustomerService;

public class CustomerCommandService : ICustomerCommandService
{
    public readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<CustomerCommandService> _logger;
    private const string ClassName = "customer";
    private readonly IMapper _mapper;

    public CustomerCommandService(ILogger<CustomerCommandService> logger, IUnitOfWOrk unitOfWOrk, IMapper mapper)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
    }

    public async Task<BaseResponse<CustomerDto>> CreateCustomer(CreateCustomerDto request)
    {
        try
        {
            // Check for existing customer with same email or phone number could be added here
            bool existingCustomer = await _unitOfWOrk.CustomerQueries.AnyAsync(x => x.Email == request.Email || x.PhoneNumber == request.PhoneNumber);
            if (existingCustomer)
            {
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.CustomerAlreadyExists);
            }

            var newEntity = new Customer(request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.CustomerType, request.DateOfBirth);
            bool isSuccessful = await _unitOfWOrk.CustomerCommands.InsertAsync(newEntity);
            if (!isSuccessful)
            {
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage(ClassName));
            }

            await _unitOfWOrk.SaveAsync();

            CustomerDto response = _mapper.Map<CustomerDto>(newEntity);
            return new BaseResponse<CustomerDto>(response, true, string.Empty, ResponseMessages.SetCreationSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in CreateCustomer: {Message}", ex.Message);
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ex.Message);
        }
    }

    // Update Customer
    public async Task<BaseResponse<CustomerDto>> UpdateCustomer(UpdateCustomerDto request)
    {
        try
        {
            var existingCustomer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == request.Id, findOptions: new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });
            if (existingCustomer == null)
            {
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            // Update fields
            existingCustomer.FirstName = request.FirstName;
            existingCustomer.LastName = request.LastName;
            existingCustomer.Email = request.Email;
            existingCustomer.PhoneNumber = request.PhoneNumber;
            existingCustomer.CustomerType = request.CustomerType;
            existingCustomer.DateOfBirth = request.DateOfBirth;


            _unitOfWOrk.CustomerCommands.Update(existingCustomer);
            await _unitOfWOrk.SaveAsync();

            CustomerDto response = _mapper.Map<CustomerDto>(existingCustomer);
            return new BaseResponse<CustomerDto>(response, true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in UpdateCustomer: {Message}", ex.Message);
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ex.Message);
        }
    }

    // Delete Customer
    public async Task<BaseResponse<bool>> DeleteCustomer(Guid customerId)
    {
        try
        {
            var existingCustomer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId, findOptions: new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });
            if (existingCustomer == null)
            {
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            _unitOfWOrk.CustomerCommands.Delete(existingCustomer);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.SetDeletedSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in DeleteCustomer: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }
}
