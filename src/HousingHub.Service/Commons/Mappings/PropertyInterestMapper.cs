using HousingHub.Model.Entities;
using HousingHub.Service.Dtos.Inspection;
using HousingHub.Service.Dtos.Notification;
using Mapster;

namespace HousingHub.Service.Commons.Mappings;

public class InspectionMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<PropertyInspection, InspectionDto>();
        config.NewConfig<Notification, NotificationDto>();
    }
}
