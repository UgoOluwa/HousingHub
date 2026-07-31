namespace HousingHub.Core;

/// <summary>
/// Identifies automated chat messages (e.g. an inspection confirmation) as coming
/// from the platform itself rather than personally from whichever user triggered
/// them. Not a real Customer record — sender-name lookups fall back to
/// <see cref="DisplayName"/> for this id instead of resolving a customer.
/// </summary>
public static class SystemSender
{
    public static readonly Guid Id = new("00000000-0000-0000-0000-000000000001");
    public const string DisplayName = "Admin";
}
