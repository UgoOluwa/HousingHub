using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Customer;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class CustomerMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Customer, CustomerDto>().TwoWays();
        config.NewConfig<Customer, LoginCustomerResponseDto>()
            .Map(dest => dest.CustomerType, src => (int)src.CustomerType)
            .Map(dest => dest.token, src => string.Empty);
        config.NewConfig<Customer, CustomerWithDetailsDto>().TwoWays();
        config.NewConfig<CreateCustomerDto, Customer>();
        config.NewConfig<UpdateCustomerDto, Customer>();
    }
}
