using System.Security.Cryptography;
using System.Text;

namespace LeatherShopAPI.Helpers;

/// <summary>
/// Paytm checksum utility — implements their proprietary AES-128-CBC based
/// signature algorithm for Initiate Transaction and response verification.
///
/// Algorithm:
///   1. Generate a 4-byte random salt → 8 hex chars
///   2. SHA-256 hash of (body + "|" + salt) → 64 hex chars
///   3. Concatenate: sha256Hex + salt  (72 chars = "hashString")
///   4. AES-128-CBC encrypt hashString with Key = IV = first 16 bytes of MerchantKey
///   5. Base64-encode the ciphertext
///
/// Verification reverses steps 4-5, then re-computes step 2 and compares.
/// </summary>
public static class PaytmChecksum
{
    /// <summary>Generates a Paytm-compatible checksum for the given JSON body.</summary>
    public static string GenerateSignature(string body, string merchantKey)
    {
        var salt = GenerateSalt(4);
        var hashString = ComputeHashString(body, salt);
        var encrypted = AesEncrypt(hashString, merchantKey);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>Verifies a Paytm checksum against the expected body and merchant key.</summary>
    public static bool VerifySignature(string body, string merchantKey, string checksum)
    {
        try
        {
            var encrypted = Convert.FromBase64String(checksum);
            var decrypted = AesDecrypt(encrypted, merchantKey);

            // decrypted = sha256Hex (64 chars) + salt (variable length, typically 8 chars)
            if (decrypted.Length < 65) return false; // At minimum: 64 hash + 1 salt char

            var extractedHash = decrypted[..64];
            var extractedSalt = decrypted[64..];

            var recomputed = ComputeHashString(body, extractedSalt);
            var recomputedHash = recomputed[..64];

            // Constant-time comparison to prevent timing attacks
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(extractedHash),
                Encoding.UTF8.GetBytes(recomputedHash));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Generates a cryptographically secure random salt as lowercase hex.</summary>
    private static string GenerateSalt(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>SHA-256(body + "|" + salt) → hex + salt</summary>
    private static string ComputeHashString(string body, string salt)
    {
        var data = body + "|" + salt;
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hashBytes).ToLowerInvariant() + salt;
    }

    /// <summary>AES-128-CBC encrypt with Key = IV = first 16 bytes of merchantKey.</summary>
    private static byte[] AesEncrypt(string plainText, string merchantKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(merchantKey[..16]);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = keyBytes;
        aes.IV = keyBytes; // Paytm uses Key as IV
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
    }

    /// <summary>AES-128-CBC decrypt with Key = IV = first 16 bytes of merchantKey.</summary>
    private static string AesDecrypt(byte[] cipherBytes, string merchantKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(merchantKey[..16]);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = keyBytes;
        aes.IV = keyBytes;
        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}
