using AutoMapper;
using HousingHub.Application.Commons.Bases;
using HousingHub.Application.Customer.Commands.Register;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Customer.Commands.Create;

public class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, BaseResponse<CustomerDto?>>
{
    public readonly ICustomerCommandService _commandService;
    public readonly IMapper _mapper;
    public RegisterCustomerCommandHandler(ICustomerCommandService commandService, IMapper mapper)
    {
        _commandService = commandService;
        _mapper = mapper;
    }

    public async Task<BaseResponse<CustomerDto?>> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        var response = await _commandService.RegisterCustomer(_mapper.Map<RegisterCustomerDto>(request));
        
        return new BaseResponse<CustomerDto?>(response.IsSuccessful, response?.Data, response?.Message, null);
    }
}
