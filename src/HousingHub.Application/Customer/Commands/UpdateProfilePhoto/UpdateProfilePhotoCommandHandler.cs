using HousingHub.Application.Commons.Bases;
using HousingHub.Service.CustomerService.Interfaces;
using MediatR;

namespace HousingHub.Application.Customer.Commands.UpdateProfilePhoto;

public class UpdateProfilePhotoCommandHandler : IRequestHandler<UpdateProfilePhotoCommand, BaseResponse<string?>>
{
    private readonly ICustomerCommandService _customerCommandService;

    public UpdateProfilePhotoCommandHandler(ICustomerCommandService customerCommandService)
    {
        _customerCommandService = customerCommandService;
    }

    public async Task<BaseResponse<string?>> Handle(UpdateProfilePhotoCommand request, CancellationToken cancellationToken)
    {
        var response = await _customerCommandService.UpdateProfilePhoto(request.CustomerId, request.File);
        return new BaseResponse<string?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
