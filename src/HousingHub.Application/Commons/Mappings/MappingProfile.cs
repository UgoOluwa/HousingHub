using AutoMapper;
using HousingHub.Application.Customer.Commands.Create;
using HousingHub.Application.Customer.Commands.Update;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Application.Commons.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CreateCustomerCommand, CreateCustomerDto>();
        CreateMap<UpdateCustomerCommand, UpdateCustomerDto>();


    }
}
