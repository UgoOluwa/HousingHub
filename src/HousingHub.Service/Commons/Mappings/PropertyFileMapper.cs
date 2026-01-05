using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyFile;

namespace HousingHub.Service.Commons.Mappings;

public class PropertyFileMapper : Profile
{
    public PropertyFileMapper()
    {
        CreateMap<PropertyFile, PropertyFileDto>().ReverseMap();
        CreateMap<CreatePropertyFileDto, PropertyFile>();
        CreateMap<UpdatePropertyFileDto, PropertyFile>();
    }
}
