using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Customer;
using HousingHub.Service.Dtos.CustomerAddress;

namespace HousingHub.Service.Commons.Mappings;

public class CustomerAddressMapper : Profile
{
    public CustomerAddressMapper()
    {
        CreateMap<CustomerAddress, CustomerAddressDto>().ReverseMap();
        CreateMap<CreateCustomerAddressDto, CustomerAddress>();
        CreateMap<UpdateCustomerDto, CustomerAddress>();
    }
}
