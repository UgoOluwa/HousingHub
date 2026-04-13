using HousingHub.Application.Commons.Bases;
using HousingHub.Service.CustomerService.Interfaces;
using MediatR;

namespace HousingHub.Application.Customer.Commands.VerifyKyc;

public class VerifyKycCommandHandler : IRequestHandler<VerifyKycCommand, BaseResponse<bool>>
{
    private readonly ICustomerCommandService _commandService;

    public VerifyKycCommandHandler(ICustomerCommandService commandService)
    {
        _commandService = commandService;
    }

    public async Task<BaseResponse<bool>> Handle(VerifyKycCommand request, CancellationToken cancellationToken)
    {
        var response = await _commandService.VerifyKyc(request.CustomerId, request.IsApproved);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
