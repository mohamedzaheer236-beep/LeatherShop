using System.Security.Cryptography;
using System.Text;

namespace LeatherShopAPI.Helpers;

/// <summary>
/// Paytm checksum utility — matches the official Paytm Node.js SDK (v1.5.1).
/// https://github.com/paytm/Paytm_Node_Checksum
///
/// Algorithm:
///   1. Generate 3 random bytes → 4-char Base64 salt
///   2. SHA-256 hash of (body + "|" + salt) → 64 hex chars
///   3. Concatenate: sha256Hex + salt  (68 chars = "hashString")
///   4. AES-128-CBC encrypt hashString with Key = first 16 bytes of MerchantKey,
///      IV = fixed "@@@@&amp;&amp;&amp;&amp;####$$$$" (16 bytes)
///   5. Base64-encode the ciphertext
///
/// Verification reverses steps 4-5, extracts last 4 chars as salt,
/// re-computes step 2 and compares the full hashString.
/// </summary>
public static class PaytmChecksum
{
    // Fixed IV used by official Paytm SDK — NOT the merchant key
    private static readonly byte[] FixedIv = Encoding.UTF8.GetBytes("@@@@&&&&####$$$$");

    /// <summary>Generates a Paytm-compatible checksum for the given JSON body.</summary>
    public static string GenerateSignature(string body, string merchantKey)
    {
        var salt = GenerateSalt();
        var hashString = ComputeHashString(body, salt);
        return AesEncrypt(hashString, merchantKey);
    }

    /// <summary>Verifies a Paytm checksum against the expected body and merchant key.</summary>
    public static bool VerifySignature(string body, string merchantKey, string checksum)
    {
        try
        {
            var decrypted = AesDecrypt(checksum, merchantKey);

            // decrypted = sha256Hex (64 chars) + salt (4 chars) = 68 chars
            if (decrypted.Length < 65) return false;

            // Extract last 4 chars as salt (matches official SDK)
            var salt = decrypted[^4..];
            var recomputed = ComputeHashString(body, salt);

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(decrypted),
                Encoding.UTF8.GetBytes(recomputed));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Generates 3 random bytes → 4-char Base64 salt (matches official Paytm SDK).</summary>
    private static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(3);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>SHA-256(body + "|" + salt) → lowercase hex + salt</summary>
    private static string ComputeHashString(string body, string salt)
    {
        var data = body + "|" + salt;
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hashBytes).ToLowerInvariant() + salt;
    }

    /// <summary>AES-128-CBC encrypt with fixed IV "@@@@&amp;&amp;&amp;&amp;####$$$$".</summary>
    private static string AesEncrypt(string plainText, string merchantKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(merchantKey.PadRight(16, '\0')[..16]);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = keyBytes;
        aes.IV = FixedIv;
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.ASCII.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>AES-128-CBC decrypt with fixed IV "@@@@&amp;&amp;&amp;&amp;####$$$$".</summary>
    private static string AesDecrypt(string encryptedBase64, string merchantKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(merchantKey.PadRight(16, '\0')[..16]);
        var cipherBytes = Convert.FromBase64String(encryptedBase64);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = keyBytes;
        aes.IV = FixedIv;
        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.ASCII.GetString(decryptedBytes);
    }
}
