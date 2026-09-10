using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HousingHub.Service.Commons.Payments;

/// <summary>
/// Paystack, over its REST API.
/// </summary>
/// <remarks>
/// <para>
/// Nigeria-first, and the reason it was chosen over Flutterwave is the webhook
/// scheme: Paystack signs each payload with an HMAC over the body, so a receiver
/// can prove the body is untampered. Flutterwave sends a static shared secret in a
/// header, which proves only that the sender knows the secret and says nothing
/// about the body.
/// </para>
/// <para>
/// The secret key is never logged, and never returned to a client. It is both the
/// API credential and the webhook signing key, so leaking it is both "someone can
/// charge as us" and "someone can forge a settlement".
/// </para>
/// </remarks>
public class PaystackPaymentGateway : IPaymentGateway
{
    /// <summary>Named client, so the base address and timeout are configured once in DI.</summary>
    public const string HttpClientName = "paystack";

    public const string SecretKeyConfigKey = "Payments:Paystack:SecretKey";
    public const string SignatureHeader = "x-paystack-signature";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<PaystackPaymentGateway> _logger;
    private readonly string _secretKey;

    public PaystackPaymentGateway(
        HttpClient http,
        IConfiguration configuration,
        ILogger<PaystackPaymentGateway> logger)
    {
        _http = http;
        _logger = logger;
        _secretKey = configuration[SecretKeyConfigKey] ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_secretKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _secretKey);
        }
    }

    public string Name => "paystack";

    /// <summary>
    /// True when a secret key is configured.
    /// </summary>
    /// <remarks>
    /// Checked rather than assumed because an unset key fails in the least helpful
    /// way available: Paystack answers 401 with its own JSON, which would surface to
    /// a payer as an unexplained failure at the moment they tried to pay.
    /// </remarks>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_secretKey);

    public async Task<GatewayInitialisation> InitialiseAsync(
        GatewayChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogError("Paystack secret key is not configured; cannot initialise a payment");
            return GatewayInitialisation.Failed("Payments are not configured on this environment.");
        }

        try
        {
            // amount is an integer of the minor unit — kobo. Sending naira here
            // undercharges by a factor of a hundred, and the request succeeds.
            var body = new
            {
                email = request.CustomerEmail,
                amount = request.AmountKobo,
                reference = request.Reference,
                currency = request.Currency,
                callback_url = request.CallbackUrl,
                metadata = request.Metadata,
            };

            using var response = await _http.PostAsJsonAsync("transaction/initialize", body, JsonOptions, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<PaystackEnvelope<InitialiseData>>(JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode || payload is null || !payload.Status || payload.Data is null)
            {
                // Paystack's own message is safe to log — it describes the request,
                // not the credential.
                _logger.LogError(
                    "Paystack initialise failed for {Reference}: {StatusCode} {Message}",
                    request.Reference, (int)response.StatusCode, payload?.Message);

                return GatewayInitialisation.Failed("We could not start that payment. Please try again.");
            }

            return GatewayInitialisation.Succeeded(payload.Data.AuthorizationUrl, payload.Data.Reference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack initialise threw for {Reference}", request.Reference);
            return GatewayInitialisation.Failed("We could not start that payment. Please try again.");
        }
    }

    public async Task<GatewayTransaction?> GetTransactionAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return null;

        try
        {
            using var response = await _http.GetAsync(
                $"transaction/verify/{Uri.EscapeDataString(reference)}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Paystack verify for {Reference} returned {StatusCode}", reference, (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<PaystackEnvelope<VerifyData>>(JsonOptions, cancellationToken);
            if (payload is null || !payload.Status || payload.Data is null) return null;

            return new GatewayTransaction(
                Reference: payload.Data.Reference ?? reference,
                Status: MapStatus(payload.Data.Status),
                AmountKobo: payload.Data.Amount,
                ProviderReference: payload.Data.Id?.ToString(),
                Channel: payload.Data.Channel,
                FailureReason: payload.Data.GatewayResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack verify threw for {Reference}", reference);
            return null;
        }
    }

    public async Task<GatewayRefund> RefundAsync(
        string transactionReference,
        long amountKobo,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogError("Paystack secret key is not configured; cannot refund");
            return GatewayRefund.Failed("Refunds are not configured on this environment.");
        }

        try
        {
            var body = new
            {
                transaction = transactionReference,
                amount = amountKobo,
                // Paystack shows this on the refund record. Deliberately the reason
                // an admin typed, so the provider's dashboard and our own row agree
                // about why the money went back.
                merchant_note = note,
            };

            using var response = await _http.PostAsJsonAsync("refund", body, JsonOptions, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<PaystackEnvelope<RefundData>>(JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode || payload is null || !payload.Status || payload.Data is null)
            {
                _logger.LogError(
                    "Paystack refund failed for {Reference}: {StatusCode} {Message}",
                    transactionReference, (int)response.StatusCode, payload?.Message);

                return GatewayRefund.Failed("The payment provider would not accept that refund.");
            }

            // "processed" means the money is already back. Anything else — normally
            // "pending" — means accepted and awaiting confirmation by webhook.
            bool isComplete = string.Equals(payload.Data.Status, "processed", StringComparison.OrdinalIgnoreCase);

            return new GatewayRefund(
                IsSuccessful: true,
                IsComplete: isComplete,
                AmountKobo: payload.Data.Amount ?? amountKobo,
                RefundReference: payload.Data.Id?.ToString(),
                Error: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack refund threw for {Reference}", transactionReference);
            return GatewayRefund.Failed("We could not reach the payment provider. Please try again.");
        }
    }

    /// <summary>
    /// HMAC-SHA512 of the raw body, keyed with the secret key, compared to the
    /// <c>x-paystack-signature</c> header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The comparison is fixed-time. A byte-by-byte string comparison that returns
    /// early leaks, through response timing, how much of a guessed signature was
    /// correct — which turns forging one from infeasible into a few thousand
    /// requests.
    /// </para>
    /// <para>
    /// Returns false when no key is configured. That is the safe direction: an
    /// environment with no key cannot verify anything, so it must not accept
    /// anything either.
    /// </para>
    /// </remarks>
    public bool IsWebhookAuthentic(string rawBody, string? signatureHeader)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(signatureHeader) || rawBody is null)
            return false;

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_secretKey));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));

        Span<byte> supplied = stackalloc byte[computed.Length];
        if (!TryParseHex(signatureHeader.Trim(), supplied)) return false;

        return CryptographicOperations.FixedTimeEquals(computed, supplied);
    }

    /// <summary>
    /// Hex to bytes, without allocating and without throwing on malformed input.
    /// </summary>
    /// <remarks>
    /// A length mismatch returns false rather than parsing what it can: a short
    /// signature that happens to prefix-match must not be treated as a partial
    /// success by anything downstream.
    /// </remarks>
    private static bool TryParseHex(string hex, Span<byte> destination)
    {
        if (hex.Length != destination.Length * 2) return false;

        for (int i = 0; i < destination.Length; i++)
        {
            if (!TryParseNibble(hex[i * 2], out int high)) return false;
            if (!TryParseNibble(hex[(i * 2) + 1], out int low)) return false;
            destination[i] = (byte)((high << 4) | low);
        }

        return true;
    }

    private static bool TryParseNibble(char c, out int value)
    {
        value = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };

        return value >= 0;
    }

    /// <summary>
    /// Anything that is not an explicit success or a recognised terminal failure is
    /// treated as still pending, so an unrecognised status never hands anything over.
    /// </summary>
    private static GatewayTransactionStatus MapStatus(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "success" => GatewayTransactionStatus.Successful,
            "failed" or "reversed" => GatewayTransactionStatus.Failed,
            "abandoned" => GatewayTransactionStatus.Abandoned,
            _ => GatewayTransactionStatus.Pending,
        };

    private sealed record PaystackEnvelope<T>(bool Status, string? Message, T? Data);

    private sealed record InitialiseData(
        [property: JsonPropertyName("authorization_url")] string AuthorizationUrl,
        [property: JsonPropertyName("access_code")] string? AccessCode,
        string? Reference);

    private sealed record RefundData(
        long? Id,
        string? Status,
        long? Amount);

    private sealed record VerifyData(
        long? Id,
        string? Reference,
        string? Status,
        long Amount,
        string? Channel,
        [property: JsonPropertyName("gateway_response")] string? GatewayResponse);
}
