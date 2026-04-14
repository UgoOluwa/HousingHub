using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.Dtos.CustomerAddress;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class CustomerAddressMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CustomerAddress, CustomerAddressDto>().TwoWays();
        config.NewConfig<CreateCustomerAddressDto, CustomerAddress>();
        config.NewConfig<UpdateCustomerDto, CustomerAddress>();
    }
}
