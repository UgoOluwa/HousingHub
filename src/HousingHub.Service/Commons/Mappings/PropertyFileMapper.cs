using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyFile;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class PropertyFileMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<PropertyFile, PropertyFileDto>().TwoWays();
        config.NewConfig<CreatePropertyFileDto, PropertyFile>();
        config.NewConfig<UpdatePropertyFileDto, PropertyFile>();
    }
}
