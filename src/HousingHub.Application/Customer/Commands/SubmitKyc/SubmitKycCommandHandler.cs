using HousingHub.Application.Commons.Bases;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Customer.Commands.SubmitKyc;

public class SubmitKycCommandHandler : IRequestHandler<SubmitKycCommand, BaseResponse<bool>>
{
    private readonly ICustomerCommandService _commandService;

    public SubmitKycCommandHandler(ICustomerCommandService commandService)
    {
        _commandService = commandService;
    }

    public async Task<BaseResponse<bool>> Handle(SubmitKycCommand request, CancellationToken cancellationToken)
    {
        var dto = new SubmitKycDto(
            request.DateOfBirth,
            request.NationalIdNumber,
            request.IdType,
            request.IdDocumentUrl,
            request.JobTitle,
            request.CompanyName,
            request.Industry);

        var response = await _commandService.SubmitKyc(request.CustomerId, dto);
        return new BaseResponse<bool>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
