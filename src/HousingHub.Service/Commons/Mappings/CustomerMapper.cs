using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Customer;

namespace HousingHub.Service.Commons.Mappings;

public class CustomerMapper : Profile
{
    public CustomerMapper()
    {
        CreateMap<Customer, CustomerDto>().ReverseMap();
        CreateMap<Customer, LoginCustomerResponseDto>()
            .ForCtorParam("CustomerType", opt => opt.MapFrom(src => (int)src.CustomerType))
            .ForCtorParam("token", opt => opt.MapFrom(src => string.Empty));
        CreateMap<Customer, CustomerWithDetailsDto>().ReverseMap();
        CreateMap<CreateCustomerDto, Customer>();
        CreateMap<UpdateCustomerDto, Customer>();
    }
}
