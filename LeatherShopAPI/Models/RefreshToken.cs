using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.Models;

/// <summary>
/// Stores refresh tokens for admin users. Each login creates a new refresh token.
/// Tokens can be revoked on logout or token rotation.
/// </summary>
public class RefreshToken
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(256)]
    public string Token { get; set; } = string.Empty;

    public int AdminUserId { get; set; }
    public AdminUser AdminUser { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Set to true when the token is explicitly revoked (logout) or rotated (refresh).
    /// Revoked tokens cannot be used even if not yet expired.
    /// </summary>
    public bool IsRevoked { get; set; }
}
