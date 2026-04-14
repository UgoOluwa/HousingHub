using HousingHub.Application.Auth.Commands.Login;
using HousingHub.Application.Auth.Commands.Register;
using HousingHub.Application.Customer.Commands.Create;
using HousingHub.Application.Customer.Commands.Register;
using HousingHub.Application.Customer.Commands.Update;
using HousingHub.Application.Property.Commands.Create;
using HousingHub.Application.Property.Commands.Update;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.Dtos.Property;
using Mapster;

namespace HousingHub.Application.Commons.Mappings;

public class MappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateCustomerCommand, CreateCustomerDto>();
        config.NewConfig<RegisterCustomerCommand, RegisterCustomerDto>();
        config.NewConfig<UpdateCustomerCommand, UpdateCustomerDto>();

        // Auth
        config.NewConfig<RegisterAuthCommand, RegisterCustomerDto>();
        config.NewConfig<LoginCommand, LoginCustomerDto>();

        // Property
        config.NewConfig<CreatePropertyCommand, CreatePropertyDto>();
        config.NewConfig<UpdatePropertyCommand, UpdatePropertyDto>();
    }
}
