using FluentValidation;

namespace HousingHub.Application.Property.Queries.GetNearby;

/// <summary>
/// Bounds the nearby search.
/// </summary>
/// <remarks>
/// <para>
/// This endpoint is anonymous and takes five numbers straight off the query string.
/// A pen test found two things: no bounds at all on <c>radiusKm</c>, <c>count</c>,
/// <c>skip</c> or the coordinates, and a 500 from a non-finite latitude.
/// </para>
/// <para>
/// The 500 is the sharper of the two. <c>double</c> binds <c>NaN</c> and
/// <c>Infinity</c> from a query string quite happily, and every comparison against
/// NaN is false — so the distance filter silently matched nothing, the pagination
/// arithmetic produced NaN, and the request died somewhere downstream with a stack
/// trace instead of an answer. Rejecting it here turns an unhandled failure into a
/// sentence.
/// </para>
/// <para>
/// The bounds matter because nearby is the most expensive read in the product: it
/// loads published listings and measures each one. An unbounded radius with a large
/// count is a free way to make the API do maximum work per request, and there is no
/// legitimate caller asking for a 40,000km radius.
/// </para>
/// </remarks>
public class GetNearbyPropertiesQueryValidator : AbstractValidator<GetNearbyPropertiesQuery>
{
    /// <summary>Roughly Lagos to Maiduguri. Beyond this, "nearby" means nothing.</summary>
    public const double MaxRadiusKm = 500;

    /// <summary>One page. The caller pages with skip rather than asking for everything.</summary>
    public const int MaxCount = 50;

    /// <summary>
    /// Deep paging past this is scraping, not browsing, and every page costs a
    /// distance calculation over the whole published set.
    /// </summary>
    public const int MaxSkip = 10_000;

    public GetNearbyPropertiesQueryValidator()
    {
        // Finite first. Without this the range rules below pass for NaN — every
        // comparison against NaN is false, so "not greater than 90" is satisfied.
        RuleFor(x => x.Latitude)
            .Must(double.IsFinite).WithMessage("Latitude must be a number.")
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.Longitude)
            .Must(double.IsFinite).WithMessage("Longitude must be a number.")
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.");

        RuleFor(x => x.RadiusKm)
            .Must(double.IsFinite).WithMessage("Radius must be a number.")
            .GreaterThan(0).WithMessage("Radius must be greater than zero.")
            .LessThanOrEqualTo(MaxRadiusKm)
            .WithMessage($"Radius must be {MaxRadiusKm}km or less.");

        RuleFor(x => x.Count)
            .InclusiveBetween(1, MaxCount)
            .WithMessage($"Count must be between 1 and {MaxCount}.");

        RuleFor(x => x.Skip)
            .InclusiveBetween(0, MaxSkip)
            .WithMessage($"Skip must be between 0 and {MaxSkip}.");
    }
}
