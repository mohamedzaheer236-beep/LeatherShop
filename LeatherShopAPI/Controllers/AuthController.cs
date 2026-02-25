using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Auth;
using LeatherShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LeatherShopAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const int TokenExpiryHours = 24;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public AuthController(IConfiguration config, AppDbContext db)
    {
        _config = config;
        _db = db;
    }

    /// <summary>
    /// Admin login — validates credentials and returns a JWT token.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Case-sensitive exact match on username
        var admin = await _db.AdminUsers
            .FirstOrDefaultAsync(a => a.Username == request.Username);

        if (admin == null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
        {
            return Unauthorized(ApiResponse<object>.Fail("Invalid username or password."));
        }

        // Update last login timestamp
        admin.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Generate JWT
        var token = GenerateJwtToken(admin.Username);
        var expiresAt = DateTime.UtcNow.AddHours(TokenExpiryHours);

        return Ok(ApiResponse<LoginResponse>.Ok(
            new LoginResponse
            {
                Token = token,
                Username = admin.Username,
                ExpiresAt = expiresAt
            },
            "Login successful."));
    }

    /// <summary>
    /// Verify if the current token is still valid.
    /// </summary>
    [HttpGet("verify")]
    [Authorize]
    public IActionResult Verify()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        return Ok(ApiResponse<object>.Ok(
            new { Username = username },
            "Token is valid."));
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
            expires: DateTime.UtcNow.AddHours(TokenExpiryHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
