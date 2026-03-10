using System.ComponentModel;

namespace HousingHub.Model.Enums;

public enum PropertyType
{
    [Description("Apartment")]
    Apartment = 1,

    [Description("House")]
    House = 2,

    [Description("Land")]
    Land = 3,

    [Description("Duplex")]
    Duplex = 4,

    [Description("Bungalow")]
    Bungalow = 5,

    [Description("Penthouse")]
    Penthouse = 6,

    [Description("Studio")]
    Studio = 7,

    [Description("Condo")]
    Condo = 8,

    [Description("Townhouse")]
    Townhouse = 9,

    [Description("Villa")]
    Villa = 10,

    [Description("Warehouse")]
    Warehouse = 11,

    [Description("Office")]
    Office = 12,

    [Description("Shop")]
    Shop = 13,

    [Description("Self Contain")]
    SelfContain = 14,

    [Description("Flat")]
    Flat = 15
}
