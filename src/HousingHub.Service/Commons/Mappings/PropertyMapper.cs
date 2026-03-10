using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Property;
using HousingHub.Service.Dtos.PropertyFile;

namespace HousingHub.Service.Commons.Mappings;

public class PropertyMapper : Profile
{
    public PropertyMapper()
    {
        CreateMap<Property, PropertyDto>()
            .ForMember(d => d.Files, opt => opt.MapFrom(s => s.Files))
            .ReverseMap();
        CreateMap<PropertyFile, PropertyFileDto>().ReverseMap();
        CreateMap<CreatePropertyDto, Property>();
        CreateMap<UpdatePropertyDto, Property>();
    }
}
