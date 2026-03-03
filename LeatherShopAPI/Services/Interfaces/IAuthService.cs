using System.Threading;
using LeatherShopAPI.DTOs.Auth;

namespace LeatherShopAPI.Services.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Validates admin credentials and returns access + refresh tokens.
    /// Returns null if credentials are invalid.
    /// </summary>
    Task<AuthResult?> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>
    /// Validates the refresh token and returns new access + refresh tokens (token rotation).
    /// Returns null if the refresh token is invalid, expired, or revoked.
    /// </summary>
    Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revokes the specified refresh token (logout).
    /// </summary>
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
}
