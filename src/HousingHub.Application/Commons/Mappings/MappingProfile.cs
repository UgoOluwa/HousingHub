using AutoMapper;
using HousingHub.Application.Auth.Commands.Login;
using HousingHub.Application.Auth.Commands.Register;
using HousingHub.Application.Customer.Commands.Create;
using HousingHub.Application.Customer.Commands.Register;
using HousingHub.Application.Customer.Commands.Update;
using HousingHub.Application.Property.Commands.Create;
using HousingHub.Application.Property.Commands.Update;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.Dtos.Property;

namespace HousingHub.Application.Commons.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CreateCustomerCommand, CreateCustomerDto>();
        CreateMap<RegisterCustomerCommand, RegisterCustomerDto>();
        CreateMap<UpdateCustomerCommand, UpdateCustomerDto>();

        // Auth
        CreateMap<RegisterAuthCommand, RegisterCustomerDto>();
        CreateMap<LoginCommand, LoginCustomerDto>();

        // Property
        CreateMap<CreatePropertyCommand, CreatePropertyDto>();
        CreateMap<UpdatePropertyCommand, UpdatePropertyDto>();
    }
}
