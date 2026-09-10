using HousingHub.Service.Commons.Payments;
using HousingHub.Service.PaymentService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.API.Controllers;

/// <summary>
/// Receives payment provider callbacks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Unversioned, and outside the versioned route on purpose.</b> This URL is
/// typed into a provider's dashboard by a human. Putting an API version in it would
/// mean that shipping v2 silently stops settling payments, with the only symptom
/// being that people who paid never get what they bought.
/// </para>
/// <para>
/// <b>Anonymous, because the caller is Paystack and has no session.</b> The
/// authentication is the HMAC signature over the request body, checked before the
/// body is parsed. That is a stronger guarantee than a bearer token would be — it
/// authenticates the payload, not just the connection.
/// </para>
/// </remarks>
[ApiController]
[Route("api/payments/webhook")]
[AllowAnonymous]
public class PaymentWebhookController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(IPaymentService payments, ILogger<PaymentWebhookController> logger)
    {
        _payments = payments;
        _logger = logger;
    }

    /// <summary>Handles a provider event.</summary>
    /// <remarks>
    /// <para>
    /// Takes no bound parameter, so the body is still unread when the action runs and
    /// can be read as raw text. A signature is computed over exact bytes: binding to
    /// a model and re-serialising changes whitespace and key order, and every genuine
    /// signature then fails to verify.
    /// </para>
    /// <para>
    /// <b>The status code is a protocol, not decoration.</b> 200 means "we have taken
    /// responsibility for this, do not send it again" — including for events we
    /// deliberately ignore. Anything else asks for redelivery, which is what we want
    /// when our own dependencies were briefly unavailable and emphatically not what
    /// we want for a body we will never be able to process.
    /// </para>
    /// </remarks>
    [HttpPost]
    // Webhook payloads are a few kilobytes. The cap costs nothing and means an
    // unauthenticated endpoint cannot be used to stream an arbitrary amount of data
    // into a Lambda's memory.
    [RequestSizeLimit(64 * 1024)]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        string rawBody;

        using (var reader = new StreamReader(Request.Body))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }

        var signature = Request.Headers[PaystackPaymentGateway.SignatureHeader].FirstOrDefault();

        var handled = await _payments.HandleWebhookAsync(rawBody, signature, cancellationToken);

        if (handled) return Ok();

        // No detail in the response. An unauthenticated caller learns only that we
        // did not accept it — not whether the signature was wrong, the reference was
        // unknown, or our own gateway call failed.
        _logger.LogWarning("A payment webhook was not accepted");
        return BadRequest();
    }
}
