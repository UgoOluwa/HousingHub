using System.Security.Claims;
using Asp.Versioning;
using HousingHub.Application.Auth.Commands.ChangePassword;
using HousingHub.Application.Auth.Commands.ForgotPassword;
using HousingHub.Application.Auth.Commands.GoogleSignIn;
using HousingHub.Application.Auth.Commands.Login;
using HousingHub.Application.Auth.Commands.Register;
using HousingHub.Application.Auth.Commands.ResetPassword;
using HousingHub.Application.Auth.Commands.VerifyEmail;
using HousingHub.Application.Commons.Bases;
using HousingHub.Service.AuthService.Interfaces;
using HousingHub.Service.Dtos.Auth;
using HousingHub.Service.Dtos.Customer;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HousingHub.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[Controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthService _authService;

    public AuthController(IMediator mediator, IAuthService authService)
    {
        _mediator = mediator;
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(BaseResponse<CustomerDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register(RegisterAuthCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(BaseResponse<LoginCustomerResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(BaseResponse<string?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePassword(ChangePasswordBodyDto body)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                       ?? User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var customerId))
            return Unauthorized();

        var command = new ChangePasswordCommand(customerId, body.CurrentPassword, body.NewPassword);
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Client-side flow: Frontend sends a Google ID token obtained from the Google Sign-In SDK.
    /// </summary>
    [HttpPost("google")]
    [ProducesResponseType(typeof(BaseResponse<LoginCustomerResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GoogleSignIn(GoogleSignInCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Server-side flow: Redirects the user to the Google consent screen.
    /// Pass a returnUrl so the callback knows where to send the JWT.
    /// </summary>
    [HttpGet("google-login")]
    public IActionResult GoogleLogin([FromQuery] string returnUrl)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback), new { returnUrl }),
            Items = { { "LoginProvider", GoogleDefaults.AuthenticationScheme } }
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Server-side flow: Google redirects here after consent.
    /// Reads the authenticated Google claims, registers or logs in the user, and
    /// redirects to the returnUrl with the JWT as a query parameter.
    /// </summary>
    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl)
    {
        var result = await HttpContext.AuthenticateAsync("ExternalAuth");
        if (result?.Principal == null)
            return Unauthorized(new { message = "Google authentication failed." });

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var googleId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var firstName = result.Principal.FindFirstValue(ClaimTypes.GivenName);
        var lastName = result.Principal.FindFirstValue(ClaimTypes.Surname);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(googleId))
            return BadRequest(new { message = "Could not retrieve email or Google ID from Google." });

        // Clean up the external cookie
        await HttpContext.SignOutAsync("ExternalAuth");

        var response = await _authService.GoogleSignInFromClaims(
            new GoogleClaimsDto(email, googleId, firstName, lastName));

        if (!response.IsSuccessful || response.Data == null)
            return BadRequest(new { message = response.Message });

        // Redirect to frontend with the JWT token
        if (!string.IsNullOrEmpty(returnUrl))
        {
            var separator = returnUrl.Contains('?') ? "&" : "?";
            return Redirect($"{returnUrl}{separator}token={response.Data.token}");
        }

        return Ok(response);
    }
}
