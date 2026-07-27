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
using Microsoft.AspNetCore.Http;

namespace HousingHub.Application.Commons.Mappings;

public class MappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // IFormFile is an interface with no constructor. Left to its own devices Mapster
        // tries to deep-clone it when it appears on both sides of a map (e.g. the Files
        // list on CreatePropertyCommand -> CreatePropertyDto) and fails building the clone
        // on IHeaderDictionary - which surfaced as "The type initializer for
        // 'Mapster.TypeAdapter`2' threw an exception" (a 500) on every publish WITH files.
        // Uploads are passed straight through by reference instead.
        config.NewConfig<IFormFile, IFormFile>().MapWith(file => file);

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
