using AutoMapper;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Customer.Commands.Update;

internal class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, BaseResponse<CustomerDto?>>
{
    public readonly ICustomerCommandService _commandService;
    public readonly IMapper _mapper;
    public UpdateCustomerCommandHandler(ICustomerCommandService commandService, IMapper mapper)
    {
        _commandService = commandService;
        _mapper = mapper;
    }

    public async Task<BaseResponse<CustomerDto?>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var response = await _commandService.UpdateCustomer(_mapper.Map<UpdateCustomerDto>(request));

        return new BaseResponse<CustomerDto?>(response.IsSuccessful, response?.Data, response?.Message, null);
    }
}
