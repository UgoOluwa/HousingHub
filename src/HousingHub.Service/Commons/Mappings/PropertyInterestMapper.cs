using AutoMapper;
using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.Dtos.Notification;

namespace HousingHub.Service.Commons.Mappings;

public class InspectionMapper : Profile
{
    public InspectionMapper()
    {
        CreateMap<PropertyInspection, InspectionDto>();
        CreateMap<Notification, NotificationDto>();
    }
}
