using System.ComponentModel;

namespace HousingHub.Model.Enums;

[Flags]
public enum PropertyFeature
{
    None = 0,

    [Description("Parking")]
    Parking = 1,

    [Description("Swimming Pool")]
    SwimmingPool = 2,

    [Description("Garden")]
    Garden = 4,

    [Description("Gym")]
    Gym = 8,

    [Description("24/7 Security")]
    Security = 16,

    [Description("Furnished")]
    Furnished = 32,

    [Description("Air Conditioning")]
    AirConditioning = 64,

    [Description("Balcony")]
    Balcony = 128,

    [Description("CCTV")]
    CCTV = 256,

    [Description("Elevator")]
    Elevator = 512,

    [Description("Backup Generator")]
    BackupGenerator = 1024,

    [Description("Borehole Water")]
    BoreholeWater = 2048,

    [Description("Serviced")]
    Serviced = 4096,

    [Description("Pet Friendly")]
    PetFriendly = 8192
}
