using System.Text;

namespace HousingHub.Core.CustomResponses;

/// <summary>
/// Turns model-binding and model-state failures into copy a user can act on.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core validates the model before the action runs — before MediatR, before
/// ValidationBehaviour, before the exception middleware — so none of the app's own
/// error handling sees it. Left alone, the response is the framework's
/// ValidationProblemDetails: a <c>title</c>, a <c>status</c>, and an <c>errors</c>
/// object keyed by C# property name. Notably it has no <c>message</c>, which every
/// other response from these APIs does have.
/// </para>
/// <para>
/// The consequence was concrete. A KYC submission missing a phone number returned
/// that shape; the frontend looked for <c>data.message</c>, found nothing, and fell
/// through to axios's own text — so the user was shown "Request failed with status
/// code 400". A property submission with an unselected type showed the raw
/// <c>{"PropertyType":["The value '0' is invalid."]}</c>.
/// </para>
/// <para>
/// Deliberately framework-free: HousingHub.Core does not reference ASP.NET Core, and
/// adding a framework reference for one formatter is a poor trade. Each API converts
/// its ModelStateDictionary into the pairs this takes.
/// </para>
/// </remarks>
public static class ValidationMessageFormatter
{
    /// <summary>Field names whose humanised form needs help.</summary>
    private static readonly Dictionary<string, string> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NationalIdNumber"] = "ID number",
        ["NIN"] = "NIN",
        ["Dsn"] = "DSN",
        ["Url"] = "link",
        ["DateOfBirth"] = "date of birth",
        ["CustomerId"] = "account reference",
        ["PropertyId"] = "listing reference",
        ["CacNumber"] = "CAC number",
        ["LasreraPermitNumber"] = "LASRERA permit number",
    };

    /// <summary>
    /// "PhoneNumber" becomes "phone number"; "IDType" becomes "id type". Field names
    /// reach users through these messages, so a raw C# identifier is not acceptable
    /// output.
    /// </summary>
    public static string Humanise(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return "details";

        // Model state keys can be paths — "Address.City", "Files[0].Name". Only the
        // leaf means anything to a user.
        var leaf = fieldName.Split('.', '[').Last().Trim(']', ' ');
        if (string.IsNullOrWhiteSpace(leaf)) leaf = fieldName;

        if (Overrides.TryGetValue(leaf, out var known)) return known;

        var builder = new StringBuilder(leaf.Length + 8);
        for (var i = 0; i < leaf.Length; i++)
        {
            var c = leaf[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(leaf[i - 1]))
                builder.Append(' ');
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// One readable sentence per offending field.
    /// </summary>
    /// <remarks>
    /// The framework's own text is inspected rather than reproduced: "is required"
    /// and "is invalid" are the two shapes model binding actually produces, and they
    /// call for different copy — one asks for something missing, the other says what
    /// was supplied does not fit.
    /// </remarks>
    public static IReadOnlyList<string> DescribeEach(IEnumerable<KeyValuePair<string, string[]>> failures)
    {
        var described = new List<string>();

        foreach (var (field, errors) in failures)
        {
            var name = Humanise(field);
            var sentenceName = char.ToUpperInvariant(name[0]) + name[1..];
            var missing = errors.Any(e =>
                e.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("must not be empty", StringComparison.OrdinalIgnoreCase));

            described.Add(missing
                ? $"{sentenceName} is required."
                : $"{sentenceName} isn't valid.");
        }

        return described;
    }

    /// <summary>
    /// The single message shown when a form is rejected. Names the field when there
    /// is only one, because that is the case where being specific actually helps.
    /// </summary>
    public static string Summarise(IEnumerable<KeyValuePair<string, string[]>> failures)
    {
        var list = failures.ToList();

        if (list.Count == 0) return "Please check the details you entered and try again.";

        if (list.Count == 1)
        {
            var (field, errors) = list[0];
            var name = Humanise(field);
            var missing = errors.Any(e =>
                e.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("must not be empty", StringComparison.OrdinalIgnoreCase));

            return missing
                ? $"Please add your {name}."
                : $"That {name} doesn't look right. Please check it and try again.";
        }

        var names = list.Select(f => Humanise(f.Key)).Distinct().ToList();
        return $"Please check these details: {string.Join(", ", names)}.";
    }
}
