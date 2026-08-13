using HousingHub.Service.VerificationService.Interfaces;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.VerificationService;

/// <summary>
/// The no-provider implementation: every lookup defers to the human reviewer.
/// </summary>
/// <remarks>
/// <para>
/// Registered until a provider account exists. It reports
/// <see cref="CacLookupResult.Performed"/> as false rather than inventing a
/// result, which is the important part — a stub that returned "passed" would
/// silently manufacture assurance nobody checked, and the review UI would show a
/// green tick for a lookup that never happened.
/// </para>
/// <para>
/// Named for what it does rather than what it lacks. "Stub" or "Null" invites
/// someone to assume it is a placeholder that can be removed; this is the correct
/// behaviour for the current state of the system, and it stays correct as a
/// fallback if a provider is later unreachable.
/// </para>
/// <para>
/// Swapping in Dojah, QoreID or Mono is one class implementing
/// <see cref="ICacLookupService"/> and one line in ConfigureServices. Nothing in
/// the review flow changes, because nothing in the review flow depends on the
/// lookup having happened.
/// </para>
/// </remarks>
public class DeferToReviewerCacLookupService : ICacLookupService
{
    private readonly ILogger<DeferToReviewerCacLookupService> _logger;

    public DeferToReviewerCacLookupService(ILogger<DeferToReviewerCacLookupService> logger)
    {
        _logger = logger;
    }

    /// <summary>Null — no provider ran, so no provider should be credited on the document.</summary>
    public string? ProviderName => null;

    public Task<CacLookupResult> LookupAsync(
        string registrationNumber, string? expectedCompanyName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "No CAC lookup provider is configured; registration {RegistrationNumber} will be verified by hand",
            registrationNumber);

        return Task.FromResult(CacLookupResult.NotPerformed());
    }
}
