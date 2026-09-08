using HousingHub.Application.Property.Queries.GetNearby;

namespace HousingHub.Test.Security;

/// <summary>
/// Bounds on the anonymous nearby search.
/// </summary>
/// <remarks>
/// A pen test found no bounds on any of the five parameters, and a 500 from a
/// non-finite latitude. The 500 is the interesting one: every comparison against
/// NaN is false, so a range rule alone would pass it.
/// </remarks>
public class NearbySearchValidationTests
{
    private static readonly GetNearbyPropertiesQueryValidator Validator = new();

    private static bool IsValid(
        double latitude = 6.5, double longitude = 3.4,
        double radiusKm = 10, int count = 10, int skip = 0) =>
        Validator.Validate(new GetNearbyPropertiesQuery(latitude, longitude, radiusKm, count, skip)).IsValid;

    [Fact]
    public void AnOrdinaryLagosSearch_IsAccepted()
    {
        Assert.True(IsValid());
    }

    /// <summary>The reported 500. `double` binds these happily from a query string.</summary>
    [Fact]
    public void ANonFiniteLatitude_IsRejected()
    {
        Assert.False(IsValid(latitude: double.NaN));
        Assert.False(IsValid(latitude: double.PositiveInfinity));
        Assert.False(IsValid(latitude: double.NegativeInfinity));
    }

    [Fact]
    public void ANonFiniteLongitude_IsRejected()
    {
        Assert.False(IsValid(longitude: double.NaN));
        Assert.False(IsValid(longitude: double.PositiveInfinity));
    }

    [Fact]
    public void ANonFiniteRadius_IsRejected()
    {
        Assert.False(IsValid(radiusKm: double.NaN));
        Assert.False(IsValid(radiusKm: double.PositiveInfinity));
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void ALatitudeOffTheGlobe_IsRejected(double latitude)
    {
        Assert.False(IsValid(latitude: latitude));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void ALongitudeOffTheGlobe_IsRejected(double longitude)
    {
        Assert.False(IsValid(longitude: longitude));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(40_000)]
    public void AnAbsurdRadius_IsRejected(double radiusKm)
    {
        Assert.False(IsValid(radiusKm: radiusKm));
    }

    [Fact]
    public void TheLargestSensibleRadius_IsAccepted()
    {
        Assert.True(IsValid(radiusKm: GetNearbyPropertiesQueryValidator.MaxRadiusKm));
    }

    /// <summary>
    /// Nearby measures every published listing, so count is how much work one
    /// request can ask for.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10_000)]
    public void AnOutOfRangeCount_IsRejected(int count)
    {
        Assert.False(IsValid(count: count));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public void AnOutOfRangeSkip_IsRejected(int skip)
    {
        Assert.False(IsValid(skip: skip));
    }

    [Fact]
    public void TheBoundariesThemselves_AreAccepted()
    {
        Assert.True(IsValid(latitude: -90, longitude: -180));
        Assert.True(IsValid(latitude: 90, longitude: 180));
        Assert.True(IsValid(count: GetNearbyPropertiesQueryValidator.MaxCount));
        Assert.True(IsValid(skip: GetNearbyPropertiesQueryValidator.MaxSkip));
    }
}
