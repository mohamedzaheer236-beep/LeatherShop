using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Auth;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LeatherShopAPI.Services;

public class AuthService : IAuthService
{
    private const int AccessTokenExpiryMinutes = 15;
    private const int RefreshTokenExpiryDays = 7;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public AuthService(IConfiguration config, AppDbContext db)
    {
        _config = config;
        _db = db;
    }

    /// <inheritdoc />
    public async Task<AuthResult?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers
            .FirstOrDefaultAsync(a => a.Username == request.Username, ct);

        if (admin == null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
            return null;

        // Update last login timestamp
        admin.LastLoginAt = DateTime.UtcNow;

        var result = await GenerateTokenPairAsync(admin, ct);
        await _db.SaveChangesAsync(ct);

        return result;
    }

    /// <inheritdoc />
    public async Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens
            .Include(rt => rt.AdminUser)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (stored == null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
            return null;

        // Revoke the old refresh token (token rotation - prevents reuse)
        stored.IsRevoked = true;

        var result = await GenerateTokenPairAsync(stored.AdminUser, ct);
        await _db.SaveChangesAsync(ct);

        return result;
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsRevoked, ct);

        if (stored != null)
        {
            stored.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResult> GenerateTokenPairAsync(AdminUser admin, CancellationToken ct)
    {
        var accessToken = GenerateJwtToken(admin.Username);
        var refreshToken = GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken,
            AdminUserId = admin.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays)
        });

        // Clean up expired/revoked tokens for this admin (housekeeping)
        await _db.RefreshTokens
            .Where(rt => rt.AdminUserId == admin.Id && (rt.IsRevoked || rt.ExpiresAt <= DateTime.UtcNow))
            .ExecuteDeleteAsync(ct);

        return new AuthResult
        {
            AccessToken = accessToken,
            Username = admin.Username,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes),
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays)
        };
    }

    private string GenerateJwtToken(string username)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT key not configured")));

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
            expires: DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}

