using System.ComponentModel;

namespace HousingHub.Model.Enums;

public enum PropertyAvailability
{
    [Description("Available")]
    Available = 1,

    [Description("Rented")]
    Rented = 2,

    [Description("Sold")]
    Sold = 3,

    [Description("Under Offer")]
    UnderOffer = 4,

    [Description("Unavailable")]
    Unavailable = 5
}
