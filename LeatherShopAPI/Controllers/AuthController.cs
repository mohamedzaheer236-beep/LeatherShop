using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LeatherShopAPI.DTOs.Auth;
using LeatherShopAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace LeatherShopAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Admin login — validates credentials and returns a JWT token.
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var adminUsername = _config["Admin:Username"] ?? "admin";
        var adminPasswordHash = _config["Admin:PasswordHash"] ?? "";

        // Validate credentials
        if (!string.Equals(request.Username, adminUsername, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid username or password."
            });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, adminPasswordHash))
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid username or password."
            });
        }

        // Generate JWT
        var token = GenerateJwtToken(adminUsername);
        var expiresAt = DateTime.UtcNow.AddHours(24);

        return Ok(new ApiResponse<LoginResponse>
        {
            Success = true,
            Message = "Login successful.",
            Data = new LoginResponse
            {
                Token = token,
                Username = adminUsername,
                ExpiresAt = expiresAt
            }
        });
    }

    /// <summary>
    /// Verify if the current token is still valid.
    /// </summary>
    [HttpGet("verify")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public IActionResult Verify()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Token is valid.",
            Data = new { Username = username }
        });
    }

    private string GenerateJwtToken(string username)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured")));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
