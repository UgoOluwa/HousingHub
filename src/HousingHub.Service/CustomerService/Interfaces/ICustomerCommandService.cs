using HousingHub.Core.CustomResponses;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Service.CustomerService.Interfaces;

public interface ICustomerCommandService
{
    Task<BaseResponse<CustomerDto>> CreateCustomer(CreateCustomerDto request);
    Task<BaseResponse<CustomerDto>> RegisterCustomer(RegisterCustomerDto request);
    Task<BaseResponse<LoginCustomerResponseDto>> LoginCustomer(LoginCustomerDto request);
    Task<BaseResponse<CustomerDto>> UpdateCustomer(UpdateCustomerDto request);
    Task<BaseResponse<CustomerDto>> UpdateProfile(Guid customerId, UpdateProfileDto request);
    Task<BaseResponse<bool>> SubmitKyc(Guid customerId, SubmitKycDto request);
    Task<BaseResponse<bool>> VerifyKyc(Guid customerId, bool isApproved);
    Task<BaseResponse<bool>> DeleteCustomer(Guid customerId);

    /// <summary>Admin: set IsActive=false without deleting the account.</summary>
    Task<BaseResponse<bool>> SuspendCustomer(Guid customerId);

    /// <summary>Admin: restore a previously suspended account.</summary>
    Task<BaseResponse<bool>> ReactivateCustomer(Guid customerId);
}
