using AutoMapper;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.CustomerService.Interfaces;
using HousingHub.Service.Dtos.Customer;
using MediatR;

namespace HousingHub.Application.Customer.Commands.UpdateProfile;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, BaseResponse<CustomerDto?>>
{
    private readonly ICustomerCommandService _commandService;
    private readonly IMapper _mapper;

    public UpdateProfileCommandHandler(ICustomerCommandService commandService, IMapper mapper)
    {
        _commandService = commandService;
        _mapper = mapper;
    }

    public async Task<BaseResponse<CustomerDto?>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var dto = new UpdateProfileDto(
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.DateOfBirth,
            request.JobTitle,
            request.CompanyName,
            request.Industry);

        var response = await _commandService.UpdateProfile(request.CustomerId, dto);
        return new BaseResponse<CustomerDto?>(response.IsSuccessful, response.Data, response.Message, null);
    }
}
