using System.Security.Claims;
using LeatherShopAPI.DTOs.Auth;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LeatherShopAPI.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "ls_refresh_token";
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService authService, IWebHostEnvironment env)
    {
        _authService = authService;
        _env = env;
    }

    /// <summary>
    /// Admin login - validates credentials, returns access token in body and sets refresh token as HttpOnly cookie.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);

        if (result == null)
            return Unauthorized(ApiResponse<object>.Fail("Invalid username or password."));

        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);

        return Ok(ApiResponse<LoginResponse>.Ok(
            new LoginResponse
            {
                Token = result.AccessToken,
                Username = result.Username,
                ExpiresAt = result.AccessTokenExpiresAt
            },
            "Login successful."));
    }

    /// <summary>
    /// Refresh access token using the HttpOnly refresh token cookie.
    /// Performs token rotation - old refresh token is revoked, new one is issued.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(ApiResponse<object>.Fail("No refresh token."));

        var result = await _authService.RefreshAsync(refreshToken, ct);
        if (result == null)
        {
            // Clear invalid cookie
            Response.Cookies.Delete(RefreshTokenCookieName);
            return Unauthorized(ApiResponse<object>.Fail("Invalid or expired refresh token."));
        }

        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);

        return Ok(ApiResponse<LoginResponse>.Ok(
            new LoginResponse
            {
                Token = result.AccessToken,
                Username = result.Username,
                ExpiresAt = result.AccessTokenExpiresAt
            },
            "Token refreshed."));
    }

    /// <summary>
    /// Logout - revokes the refresh token and clears the cookie.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.RevokeAsync(refreshToken, ct);
        }

        Response.Cookies.Delete(RefreshTokenCookieName);
        return Ok(ApiResponse.Ok("Logged out."));
    }

    /// <summary>
    /// Verify if the current access token is still valid.
    /// </summary>
    [HttpGet("verify")]
    [Authorize]
    public IActionResult Verify()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        return Ok(ApiResponse<VerifyResponse>.Ok(
            new VerifyResponse { Username = username ?? string.Empty },
            "Token is valid."));
    }

    private void SetRefreshTokenCookie(string token, DateTime expiresAt)
    {
        Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,                          // HTTPS only
            SameSite = SameSiteMode.None,           // Cross-origin (Vercel → Railway)
            Expires = expiresAt,
            Path = "/api"                           // Only sent with API requests
        });
    }
}
