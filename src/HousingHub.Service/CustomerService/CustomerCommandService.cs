using HousingHub.Service.Commons.Mappings;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using Microsoft.AspNetCore.Http;
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
    private readonly IFileStorageService _fileStorageService;
    private readonly IEmailService _emailService;

    public CustomerCommandService(ILogger<CustomerCommandService> logger, IUnitOfWOrk unitOfWOrk, IMapper mapper, IPasswordHasher passwordHasher, ITokenProvider tokenProvider, IFileStorageService fileStorageService, IEmailService emailService)
    {
        _logger = logger;
        _unitOfWOrk = unitOfWOrk;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
        _tokenProvider = tokenProvider;
        _fileStorageService = fileStorageService;
        _emailService = emailService;
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
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
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
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
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
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
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
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
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
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<string?>> UpdateProfilePhoto(Guid customerId, IFormFile? file)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByIdAsync(customerId);
            if (customer is null)
                return new BaseResponse<string?>(null, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            // A null file clears the picture; otherwise upload and replace.
            string? newUrl = null;
            if (file is not null)
            {
                // This path previously had no validation of any kind — no size cap, no
                // extension allow-list, no content check — so any authenticated user
                // could push arbitrary content into the public bucket.
                var validation = UploadedFileValidator.Validate(
                    file,
                    UploadedFileValidator.ImageExtensions,
                    maxBytes: 5 * 1024 * 1024);

                if (!validation.IsValid)
                    return new BaseResponse<string?>(null, false, string.Empty, validation.Error!);

                newUrl = await _fileStorageService.UploadFileAsync(
                    file, $"profile-photos/{customerId}", validation.ContentType);
            }

            // Best-effort cleanup of the previous object so we don't leak storage.
            if (!string.IsNullOrEmpty(customer.ProfileImageUrl))
            {
                try { await _fileStorageService.DeleteFileAsync(customer.ProfileImageUrl); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete old profile photo for {CustomerId}", customerId); }
            }

            customer.ProfileImageUrl = newUrl;
            await _unitOfWOrk.CustomerCommands.UpdateAsync(customer);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<string?>(newUrl, true, string.Empty, ResponseMessages.SetUpdateSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in UpdateProfilePhoto: {Message}", ex.Message);
            return new BaseResponse<string?>(null, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> SubmitKyc(Guid customerId, SubmitKycDto request)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (customer is null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            // The document reference comes from the request body, so a caller could
            // otherwise point their KYC record at any object — including another
            // user's already-approved document. Only accept a key that the upload
            // endpoint would have produced for this same customer.
            if (!string.IsNullOrWhiteSpace(request.IdDocumentUrl)
                && !IsOwnKycDocumentKey(request.IdDocumentUrl, customerId))
            {
                _logger.LogWarning(
                    "Rejected KYC submission for {CustomerId}: document reference is not scoped to this customer",
                    customerId);
                return new BaseResponse<bool>(false, false, string.Empty,
                    "The uploaded document could not be matched to your account. Please upload it again.");
            }

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

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.KycSubmitted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in SubmitKyc: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    /// <summary>
    /// Revokes every active refresh token for an account. Done here through the unit of
    /// work rather than by injecting IAuthService, to avoid a dependency cycle between
    /// the customer and auth services.
    /// </summary>
    private async Task RevokeAllSessionsAsync(Guid customerId)
    {
        var activeTokens = await _unitOfWOrk.RefreshTokenQueries.GetActiveByCustomerIdAsync(customerId);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            await _unitOfWOrk.RefreshTokenCommands.UpdateAsync(token);
        }
    }

    /// <summary>
    /// True when the reference is an object key produced by the KYC upload endpoint
    /// for this specific customer, i.e. <c>private/kyc/{customerId}/...</c>.
    /// </summary>
    private static bool IsOwnKycDocumentKey(string reference, Guid customerId) =>
        reference.StartsWith(
            $"{S3FileStorageService.PrivatePrefix}/kyc/{customerId}/",
            StringComparison.OrdinalIgnoreCase);

    public async Task<BaseResponse<bool>> VerifyKyc(Guid customerId, bool isApproved, string? rejectionReason = null)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (customer is null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            customer.UpdateKycStatus(isApproved, rejectionReason);

            await _unitOfWOrk.CustomerCommands.UpdateAsync(customer);
            await _unitOfWOrk.SaveAsync();

            // Best-effort — a failed notification shouldn't undo the KYC decision itself.
            if (isApproved)
                await _emailService.SendKycApprovedAsync(customer.Email, customer.FirstName);
            else
                await _emailService.SendKycRejectedAsync(customer.Email, customer.FirstName, rejectionReason ?? "No reason provided.");

            var message = isApproved ? "KYC verified successfully." : "KYC rejected.";
            return new BaseResponse<bool>(true, true, string.Empty, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in VerifyKyc: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    // Delete Customer
    public async Task<BaseResponse<bool>> DeleteCustomer(Guid customerId)
    {
        try
        {
            var existingCustomer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (existingCustomer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            await _unitOfWOrk.CustomerCommands.DeleteAsync(existingCustomer);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.SetDeletedSuccessMessage(ClassName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in DeleteCustomer: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> SuspendCustomer(Guid customerId)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            customer.IsActive = false;
            customer.DateModified = DateTime.UtcNow;

            // Suspension previously only set a flag. The account's refresh tokens stayed
            // valid, so a suspended user kept working until their token expired — up to
            // 30 days. Revoke the whole family so the suspension takes effect now.
            await RevokeAllSessionsAsync(customerId);

            await _unitOfWOrk.CustomerCommands.UpdateAsync(customer);
            await _unitOfWOrk.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.CustomerSuspended);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in SuspendCustomer: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> ReactivateCustomer(Guid customerId)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            customer.IsActive = true;
            customer.DateModified = DateTime.UtcNow;
            await _unitOfWOrk.CustomerCommands.UpdateAsync(customer);
            await _unitOfWOrk.SaveAsync();

            await _emailService.SendAccountReactivatedAsync(customer.Email, customer.FirstName);

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.CustomerReactivated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in ReactivateCustomer: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }

    public async Task<BaseResponse<bool>> SetManagedByHousingHubAsync(Guid customerId, bool isManaged)
    {
        try
        {
            var customer = await _unitOfWOrk.CustomerQueries.GetByAsync(x => x.Id == customerId);
            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage(ClassName));

            if (!customer.CustomerType.CanManageProperties())
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnauthorizedPropertyAction);

            customer.IsManagedByHousingHub = isManaged;
            customer.DateModified = DateTime.UtcNow;
            await _unitOfWOrk.CustomerCommands.UpdateAsync(customer);
            await _unitOfWOrk.SaveAsync();

            string message = isManaged
                ? "Owner is now managed by HousingHub."
                : "Owner is no longer managed by HousingHub.";
            return new BaseResponse<bool>(true, true, string.Empty, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in SetManagedByHousingHubAsync: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.UnexpectedError);
        }
    }
}
