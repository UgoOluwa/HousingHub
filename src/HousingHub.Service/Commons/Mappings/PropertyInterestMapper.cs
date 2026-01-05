using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyInterest;

namespace HousingHub.Service.Commons.Mappings;

public class PropertyInterestMapper : Profile
{
    public PropertyInterestMapper()
    {
        CreateMap<PropertyInterest, PropertyInterestDto>().ReverseMap();
        CreateMap<CreatePropertyInterestDto, PropertyInterest>();
        CreateMap<UpdatePropertyInterestDto, PropertyInterest>();
    }
}
