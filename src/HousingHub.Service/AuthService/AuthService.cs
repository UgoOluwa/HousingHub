using System.Security.Cryptography;
using AutoMapper;
using Google.Apis.Auth;
using HousingHub.Core.CustomResponses;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Commons.Authentication;
using HousingHub.Service.Commons.Email;
using HousingHub.Service.Dtos.Auth;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.RepositoryInterfaces.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.AuthService;

public class AuthService : IAuthService
{
    private readonly IUnitOfWOrk _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenProvider _tokenProvider;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IEmailService _emailService;

    public AuthService(
        IUnitOfWOrk unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenProvider tokenProvider,
        IMapper mapper,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenProvider = tokenProvider;
        _mapper = mapper;
        _configuration = configuration;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<BaseResponse<CustomerDto>> Register(RegisterCustomerDto request)
    {
        try
        {
            bool exists = await _unitOfWork.CustomerQueries.AnyAsync(
                x => x.Email == request.Email || x.PhoneNumber == request.PhoneNumber);

            if (exists)
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.CustomerAlreadyExists);

            string passwordHash = _passwordHasher.Hash(request.Password);

            var customer = new Customer(
                request.FirstName, request.LastName, request.Email,
                request.PhoneNumber, request.CustomerType, passwordHash)
            {
                EmailVerificationToken = GenerateSecureToken(),
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
                AuthProvider = AuthProvider.Local
            };

            bool inserted = await _unitOfWork.CustomerCommands.InsertAsync(customer);
            if (!inserted)
                return new BaseResponse<CustomerDto>(null, false, string.Empty, ResponseMessages.SetCreationFailureMessage("customer"));

            await _unitOfWork.SaveAsync();

            await _emailService.SendEmailVerificationAsync(customer.Email, customer.FirstName, customer.EmailVerificationToken!);

            var dto = _mapper.Map<CustomerDto>(customer);
            return new BaseResponse<CustomerDto>(dto, true, string.Empty,
                ResponseMessages.SetCreationSuccessMessage("customer") + ". Please verify your email.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Register: {Message}", ex.Message);
            return new BaseResponse<CustomerDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<LoginCustomerResponseDto>> Login(LoginCustomerDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByAsync(
                x => x.Email == request.Email,
                new FindOptions { IsAsNoTracking = true, IsIgnoreAutoIncludes = true });

            if (customer == null || string.IsNullOrEmpty(customer.PasswordHash)
                || !_passwordHasher.Verify(request.Password, customer.PasswordHash))
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.InvalidCredentials);

            if (customer.AuthProvider == AuthProvider.Google)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.AccountUsesGoogleAuth);

            if (!customer.EmailVerified)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.EmailNotVerified);

            string token = _tokenProvider.Create(customer);
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Login: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> VerifyEmail(VerifyEmailRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByAsync(
                x => x.Email == request.Email,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (customer.EmailVerified)
                return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.EmailAlreadyVerified);

            if (customer.EmailVerificationToken != request.Token
                || customer.EmailVerificationTokenExpiry == null
                || customer.EmailVerificationTokenExpiry < DateTime.UtcNow)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.EmailVerificationFailed);

            customer.EmailVerified = true;
            customer.EmailVerificationToken = null;
            customer.EmailVerificationTokenExpiry = null;

            _unitOfWork.CustomerCommands.Update(customer);
            await _unitOfWork.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.EmailVerificationSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in VerifyEmail: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<string>> ForgotPassword(ForgotPasswordRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByAsync(
                x => x.Email == request.Email,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (customer == null)
                return new BaseResponse<string>(null, true, string.Empty, ResponseMessages.PasswordResetTokenSent);

            if (customer.AuthProvider == AuthProvider.Google)
                return new BaseResponse<string>(null, false, string.Empty, ResponseMessages.AccountUsesGoogleAuth);

            customer.PasswordResetToken = GenerateSecureToken();
            customer.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

            _unitOfWork.CustomerCommands.Update(customer);
            await _unitOfWork.SaveAsync();

            await _emailService.SendPasswordResetAsync(customer.Email, customer.FirstName, customer.PasswordResetToken!);

            return new BaseResponse<string>(null, true, string.Empty, ResponseMessages.PasswordResetTokenSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ForgotPassword: {Message}", ex.Message);
            return new BaseResponse<string>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> ResetPassword(ResetPasswordRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByAsync(
                x => x.Email == request.Email,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (customer.PasswordResetToken != request.Token
                || customer.PasswordResetTokenExpiry == null
                || customer.PasswordResetTokenExpiry < DateTime.UtcNow)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.PasswordResetFailed);

            customer.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            customer.PasswordResetToken = null;
            customer.PasswordResetTokenExpiry = null;

            _unitOfWork.CustomerCommands.Update(customer);
            await _unitOfWork.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.PasswordResetSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ResetPassword: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<bool>> ChangePassword(ChangePasswordRequestDto request)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByAsync(
                x => x.Id == request.CustomerId,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (customer == null)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.SetNotFoundMessage("customer"));

            if (customer.AuthProvider == AuthProvider.Google)
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.AccountUsesGoogleAuth);

            if (!_passwordHasher.Verify(request.CurrentPassword, customer.PasswordHash))
                return new BaseResponse<bool>(false, false, string.Empty, ResponseMessages.CurrentPasswordIncorrect);

            customer.PasswordHash = _passwordHasher.Hash(request.NewPassword);

            _unitOfWork.CustomerCommands.Update(customer);
            await _unitOfWork.SaveAsync();

            return new BaseResponse<bool>(true, true, string.Empty, ResponseMessages.PasswordChangeSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChangePassword: {Message}", ex.Message);
            return new BaseResponse<bool>(false, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<LoginCustomerResponseDto>> GoogleSignIn(GoogleSignInRequestDto request)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"]! }
            };

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            }
            catch
            {
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.GoogleSignInFailed);
            }

            var customer = await _unitOfWork.CustomerQueries.GetByAsync(
                x => x.Email == payload.Email,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (customer != null && customer.AuthProvider != AuthProvider.Google)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.AccountUsesLocalAuth);

            if (customer == null)
            {
                customer = new Customer(
                    payload.GivenName ?? string.Empty,
                    payload.FamilyName ?? string.Empty,
                    payload.Email,
                    string.Empty,
                    CustomerType.Customer,
                    string.Empty)
                {
                    GoogleId = payload.Subject,
                    AuthProvider = AuthProvider.Google,
                    EmailVerified = true
                };

                bool inserted = await _unitOfWork.CustomerCommands.InsertAsync(customer);
                if (!inserted)
                    return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty,
                        ResponseMessages.SetCreationFailureMessage("customer"));

                await _unitOfWork.SaveAsync();
            }

            string token = _tokenProvider.Create(customer);
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GoogleSignIn: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ex.Message);
        }
    }

    public async Task<BaseResponse<LoginCustomerResponseDto>> GoogleSignInFromClaims(GoogleClaimsDto claims)
    {
        try
        {
            var customer = await _unitOfWork.CustomerQueries.GetByAsync(
                x => x.Email == claims.Email,
                new FindOptions { IsAsNoTracking = false, IsIgnoreAutoIncludes = true });

            if (customer != null && customer.AuthProvider != AuthProvider.Google)
                return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ResponseMessages.AccountUsesLocalAuth);

            if (customer == null)
            {
                customer = new Customer(
                    claims.FirstName ?? string.Empty,
                    claims.LastName ?? string.Empty,
                    claims.Email,
                    string.Empty,
                    CustomerType.Customer,
                    string.Empty)
                {
                    GoogleId = claims.GoogleId,
                    AuthProvider = AuthProvider.Google,
                    EmailVerified = true
                };

                bool inserted = await _unitOfWork.CustomerCommands.InsertAsync(customer);
                if (!inserted)
                    return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty,
                        ResponseMessages.SetCreationFailureMessage("customer"));

                await _unitOfWork.SaveAsync();
            }

            string token = _tokenProvider.Create(customer);
            var response = _mapper.Map<LoginCustomerResponseDto>(customer);
            response = response with { token = token };

            return new BaseResponse<LoginCustomerResponseDto>(response, true, string.Empty, ResponseMessages.LoginSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GoogleSignInFromClaims: {Message}", ex.Message);
            return new BaseResponse<LoginCustomerResponseDto>(null, false, string.Empty, ex.Message);
        }
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
