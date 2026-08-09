using HousingHub.Application.Commons.Bases;
using MediatR;

namespace HousingHub.Application.Auth.Commands.Logout;

/// <summary>
/// Ends a session server-side by revoking its refresh token.
/// </summary>
/// <param name="RefreshToken">The refresh token held by the client.</param>
/// <param name="AllSessions">
/// Revoke every active token on the account rather than just this one, for
/// "sign out everywhere".
/// </param>
public record LogoutCommand(string RefreshToken, bool AllSessions = false)
    : IRequest<BaseResponse<bool>>;
