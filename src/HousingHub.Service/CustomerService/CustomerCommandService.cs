using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.CustomerService;

public class CustomerCommandService : ICustomerCommandService
{
    public readonly IUnitOfWOrk _unitOfWOrk;
    private readonly ILogger<CustomerCommandService> _logger;
    private const string ClassName = "customer";
    private readonly IMapper _mapper;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenProvider _tokenProvider;

    public CustomerCommandService(ILogger<CustomerCommandService> logger, IUnitOfWOrk unitOfWOrk, IMapper mapper, IPasswordHasher passwordHasher, ITokenProvider tokenProvider)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
        _tokenProvider = tokenProvider;
    }

    public async Task<BaseResponse<CustomerDto>> CreateCustomer(CreateCustomerDto request)
    {
        try
        {
            bool existingCustomer = await _unitOfWOrk.CustomerQueries.AnyAsync(x => x.Email == request.Email || x.PhoneNumber == request.PhoneNumber);
            if (existingCustomer)
            {
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.CustomerAlreadyExists);
            }

            var newEntity = _mapper.Map<Customer>(request);
            newEntity.Id = Guid.NewGuid();
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

    public async Task<BaseResponse<CustomerDto>> RegisterCustomer(RegisterCustomerDto request)
    {
        try
        {
            // Check for existing customer with same email or phone number could be added here
            bool existingCustomer = await _unitOfWOrk.CustomerQueries.AnyAsync(x => x.Email == request.Email || x.PhoneNumber == request.PhoneNumber);
            if (existingCustomer)
            {
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.CustomerAlreadyExists);
            }

            string passwordHash = _passwordHasher.Hash(request.Password);

            var newEntity = new Customer(request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.CustomerType, passwordHash);
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

    // login customer
    public async Task<BaseResponse<LoginCustomerResponseDto>> LoginCustomer(LoginCustomerDto request)
    {
        try
        {
            var emailOrPhone = request.EmailOrPhone.Trim();
            var existingCustomer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Email == emailOrPhone || x.PhoneNumber == emailOrPhone);
        if (existingCustomer == null || !_passwordHasher.Verify(request.Password, existingCustomer.PasswordHash))
            {
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.InvalidCredentials);
            }

            string token = _tokenProvider.Create(existingCustomer);

            LoginCustomerResponseDto response = _mapper.Map<LoginCustomerResponseDto>(existingCustomer);
            response = response with { token = token };
            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in LoginCustomer: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ex.Message);
        }
    }


    // Update Customer
    public async Task<BaseResponse<CustomerDto>> UpdateCustomer(UpdateCustomerDto request)
    {
        try
        {
            var existingCustomer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == request.Id);
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


            await _unitOfWOrk.CustomerCommands.UpdateAsync(existingCustomer);
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

    public async Task<BaseResponse<CustomerDto>> UpdateProfile(Guid customerId, UpdateProfileDto request)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (customer is null)
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            customer.FirstName = request.FirstName;
            customer.LastName = request.LastName;
            customer.PhoneNumber = request.PhoneNumber;
            customer.DateOfBirth = request.DateOfBirth;
            customer.JobTitle = request.JobTitle;
            customer.CompanyName = request.CompanyName;
            customer.Industry = request.Industry;

            await _unitOfWOrk.CustomerCommands.UpdateAsync(customer);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<CustomerDto>(_mapper.Map<CustomerDto>(customer), true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in UpdateProfile: {Message}", ex.Message);
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> SubmitKyc(Guid customerId, SubmitKycDto request)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (customer is null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            customer.AddKYCDetails(
                request.DateOfBirth,
                request.NationalIdNumber,
                request.IdType,
                request.IdDocumentUrl,
                DateTime.UtcNow,
                request.JobTitle,
                request.CompanyName,
                request.Industry);

            await _unitOfWOrk.CustomerCommands.UpdateAsync(customer);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, "KYC submitted successfully. Verification is pending.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in SubmitKyc: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> VerifyKyc(Guid customerId, bool isApproved)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (customer is null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            customer.UpdateKycStatus(isApproved);

            await _unitOfWOrk.CustomerCommands.UpdateAsync(customer);
            await _unitOfWOrk.SaveAsync();

            var message = isApproved ? "KYC verified successfully." : "KYC rejected.";
            return new BaseResponse<bool>(true, true, string.Empty, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in VerifyKyc: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }

    // Delete Customer
    public async Task<BaseResponse<bool>> DeleteCustomer(Guid customerId)
    {
        try
        {
            var existingCustomer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (existingCustomer == null)
            {
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));
            }

            await _unitOfWOrk.CustomerCommands.DeleteAsync(existingCustomer);
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
