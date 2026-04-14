using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyAddress;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class PropertyAddressMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<PropertyAddress, PropertyAddressDto>().TwoWays();
        config.NewConfig<CreatePropertyAddressDto, PropertyAddress>();
        config.NewConfig<UpdatePropertyAddressDto, PropertyAddress>();
    }
}
