using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyFile;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class PropertyMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Property, PropertyDto>()
            .Map(dest => dest.Files, src => src.Files)
            .TwoWays();
        config.NewConfig<PropertyFile, PropertyFileDto>().TwoWays();
        config.NewConfig<CreatePropertyDto, Property>();
        config.NewConfig<UpdatePropertyDto, Property>();
    }
}
