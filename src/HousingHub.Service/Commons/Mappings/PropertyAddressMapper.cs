using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.PropertyAddress;

namespace HousingHub.Service.Commons.Mappings;

public class PropertyAddressMapper : Profile
{
    public PropertyAddressMapper()
    {
        CreateMap<PropertyAddress, PropertyAddressDto>().ReverseMap();
        CreateMap<CreatePropertyAddressDto, PropertyAddress>();
        CreateMap<UpdatePropertyAddressDto, PropertyAddress>();
    }
}
