using HousingHub.Model.Enums;
using HousingHub.Service.Dtos.Utility;

namespace HousingHub.Service.Commons.Utilities;

public interface IUtilityService
{
    Dictionary<string, List<EnumDetailDto>> GetAllEnums();
}

public class UtilityService : IUtilityService
{
    public Dictionary<string, List<EnumDetailDto>> GetAllEnums()
    {
        return new Dictionary<string, List<EnumDetailDto>>
        {
            { "PropertyType", EnumUtility.GetEnumDetails<PropertyType>() },
            { "PropertyAvailability", EnumUtility.GetEnumDetails<PropertyAvailability>() },
            { "PropertyLeaseType", EnumUtility.GetEnumDetails<PropertyLeaseType>() },
            { "PropertyFeature", EnumUtility.GetEnumDetails<PropertyFeature>() },
            { "PropertyFileType", EnumUtility.GetEnumDetails<PropertyFileType>() },
            { "CustomerType", EnumUtility.GetEnumDetails<CustomerType>() },
            { "InspectionStatus", EnumUtility.GetEnumDetails<InspectionStatus>() },
            { "NotificationType", EnumUtility.GetEnumDetails<NotificationType>() },
            { "AuthProvider", EnumUtility.GetEnumDetails<AuthProvider>() },
            { "IDType", EnumUtility.GetEnumDetails<IDType>() },
        };
    }
}
