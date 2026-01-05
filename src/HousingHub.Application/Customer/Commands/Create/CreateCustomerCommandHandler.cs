using AutoMapper;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Customer.Commands.Create;

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, BaseResponse<CustomerDto?>>
{
    public readonly ICustomerCommandService _commandService;
    public readonly IMapper _mapper;
    public CreateCustomerCommandHandler(ICustomerCommandService commandService, IMapper mapper)
    {
        _commandService = commandService;
        _mapper = mapper;
    }

    public async Task<BaseResponse<CustomerDto?>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var response = await _commandService.CreateCustomer(_mapper.Map<CreateCustomerDto>(request));
        
        return new BaseResponse<CustomerDto?>(response.IsSuccessful, response?.Data, response?.Message, null);
    }
}
