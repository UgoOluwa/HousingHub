using System.Reflection;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HousingHub.Service.Commons.Web;

/// <summary>
/// Clamps paging parameters on every action before it runs.
/// </summary>
/// <remarks>
/// <para>
/// No endpoint validated its page size, so <c>?pageSize=100000000</c> was accepted.
/// Combined with a repository layer that scans the whole table and paginates in
/// memory, that is a cheap way to exhaust read capacity and Lambda memory — and to
/// run up an AWS bill. A negative page number reached <c>Skip(negative)</c> and
/// produced an unhandled exception, i.e. a 500 from a malformed query string.
/// </para>
/// <para>
/// Applied globally rather than per-endpoint deliberately: there are paging
/// parameters on seven controllers plus five filter DTOs, and a per-endpoint fix
/// would be forgotten on the next one added. Clamping silently rather than
/// rejecting keeps well-behaved clients working while bounding the damage.
/// </para>
/// </remarks>
public sealed class PaginationClampFilter : IActionFilter
{
    /// <summary>
    /// Upper bound on rows per request. Generous for any real UI, small enough that
    /// a single call cannot pull the table.
    /// </summary>
    public const int MaxPageSize = 100;

    private const string PageSizeName = "pageSize";
    private const string PageNumberName = "pageNumber";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Scalar arguments, e.g. GetAll([FromQuery] int pageNumber, int pageSize).
        foreach (var key in context.ActionArguments.Keys.ToList())
        {
            var value = context.ActionArguments[key];

            if (value is int intValue)
            {
                if (key.Equals(PageSizeName, StringComparison.OrdinalIgnoreCase))
                    context.ActionArguments[key] = ClampPageSize(intValue);
                else if (key.Equals(PageNumberName, StringComparison.OrdinalIgnoreCase))
                    context.ActionArguments[key] = ClampPageNumber(intValue);

                continue;
            }

            // Filter objects that carry paging as properties, e.g. AdminCustomerFilterDto.
            if (value is not null && !IsSimple(value.GetType()))
                ClampProperties(value);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }

    private static int ClampPageSize(int value) => value switch
    {
        < 1 => 1,
        > MaxPageSize => MaxPageSize,
        _ => value,
    };

    private static int ClampPageNumber(int value) => value < 1 ? 1 : value;

    /// <summary>
    /// Clamps PageSize/PageNumber on a bound filter object.
    /// </summary>
    /// <remarks>
    /// Several of these are positional records whose properties are init-only, so the
    /// public setter is absent. The backing field is written directly in that case —
    /// ugly, but the alternative is rewriting five DTOs and every construction site,
    /// and the clamp needs to hold regardless of how the type is declared.
    /// </remarks>
    private static void ClampProperties(object target)
    {
        var type = target.GetType();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType != typeof(int)) continue;

            bool isPageSize = property.Name.Equals(PageSizeName, StringComparison.OrdinalIgnoreCase);
            bool isPageNumber = property.Name.Equals(PageNumberName, StringComparison.OrdinalIgnoreCase);

            if (!isPageSize && !isPageNumber) continue;

            var current = (int)(property.GetValue(target) ?? 0);
            var clamped = isPageSize ? ClampPageSize(current) : ClampPageNumber(current);

            if (clamped == current) continue;

            if (property.CanWrite)
            {
                property.SetValue(target, clamped);
                continue;
            }

            var backingField = type.GetField(
                $"<{property.Name}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            backingField?.SetValue(target, clamped);
        }
    }

    private static bool IsSimple(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string)
        || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid);
}
