using AutoMapper;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.CustomerService.Interfaces;
using MediatR;

namespace HousingHub.Application.Customer.Commands.Delete;

public class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, BaseResponse<bool>>
{
    public readonly ICustomerCommandService _commandService;
    public readonly IMapper _mapper;
    public DeleteCustomerCommandHandler(ICustomerCommandService commandService, IMapper mapper)
    {
        _commandService = commandService;
        _mapper = mapper;
    }

    public async Task<BaseResponse<bool>> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var response = await _commandService.DeleteCustomer(request.Id);

        return new BaseResponse<bool>(response.IsSuccessful, response.IsSuccessful, response?.Message, null);
    }
}
