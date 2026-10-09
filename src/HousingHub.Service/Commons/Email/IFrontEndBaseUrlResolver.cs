namespace HousingHub.Service.Commons.Email;

/// <summary>
/// The address an email's links should point at.
/// </summary>
public interface IFrontEndBaseUrlResolver
{
    /// <summary>
    /// The front end the current request came from, or the configured default.
    /// </summary>
    /// <returns>An origin with no trailing slash, e.g. <c>https://housinghub.ng</c>.</returns>
    string Resolve();
}
