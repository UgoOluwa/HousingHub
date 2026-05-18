using System.ComponentModel.DataAnnotations;
using System.Reflection;
using HousingHub.Service.Dtos.Utility;

namespace HousingHub.Service.Commons.Utilities;

public static class EnumUtility
{
    public static List<EnumDetailDto> GetEnumDetails<T>() where T : Enum
    {
        var enumType = typeof(T);
        var values = Enum.GetValues(enumType);
        var details = new List<EnumDetailDto>();

        foreach (var value in values)
        {
            if (value is null) continue;

            var enumValue = (T)value;
            var id = Convert.ToInt32(enumValue);
            var name = enumValue.ToString();
            var description = GetDescription(enumValue);

            details.Add(new EnumDetailDto(id, name, description));
        }

        return details.OrderBy(x => x.Id).ToList();
    }

    private static string GetDescription<T>(T enumValue) where T : Enum
    {
        var field = enumValue.GetType().GetField(enumValue.ToString());
        if (field is null) return enumValue.ToString();

        var displayAttribute = field.GetCustomAttribute<DisplayAttribute>();
        if (displayAttribute?.Description != null)
            return displayAttribute.Description;

        return enumValue.ToString();
    }
}
