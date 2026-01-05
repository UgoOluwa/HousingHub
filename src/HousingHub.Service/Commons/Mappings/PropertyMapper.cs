using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Property;

namespace HousingHub.Service.Commons.Mappings;

public class PropertyMapper : Profile
{
    public PropertyMapper()
    {
        CreateMap<Property, PropertyDto>().ReverseMap();
        CreateMap<CreatePropertyDto, Property>();
        CreateMap<UpdatePropertyDto, Property>();
    }
}
