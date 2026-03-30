using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.DTOs.Auth;

public class LoginRequest
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Internal result from AuthService that includes both access and refresh tokens.
/// The controller decides how to deliver each token (body vs. cookie).
/// </summary>
public class AuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
}

public class VerifyResponse
{
    public string Username { get; set; } = string.Empty;
}
