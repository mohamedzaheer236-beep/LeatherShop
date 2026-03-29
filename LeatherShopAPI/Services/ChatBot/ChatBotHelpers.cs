namespace LeatherShopAPI.Services.ChatBot;

/// <summary>
/// Shared utility methods used across chatbot handlers.
/// </summary>
public static class ChatBotHelpers
{
    /// <summary>
    /// Returns the public base URL for constructing externally-reachable links (images, payment).
    /// Prefers App:BaseUrl config, falls back to RAILWAY_PUBLIC_DOMAIN env var.
    /// Skips localhost/placeholder values since WhatsApp servers can't reach them.
    /// </summary>
    public static string? GetPublicBaseUrl(IConfiguration config)
    {
        var baseUrl = config["App:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl) || baseUrl.Contains("WILL_BE_SET") || baseUrl.Contains("localhost"))
        {
            var railwayDomain = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN");
            if (!string.IsNullOrEmpty(railwayDomain))
                baseUrl = $"https://{railwayDomain}";
            else
                baseUrl = null;
        }
        return baseUrl;
    }

    /// <summary>
    /// Resolve a relative image path (e.g., /uploads/abc.jpg) to a full public URL.
    /// Returns null if the path is empty or no public base URL is available.
    /// </summary>
    public static string? ResolveImageUrl(string? relativePath, IConfiguration config)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        if (relativePath.StartsWith("http")) return relativePath;

        var baseUrl = GetPublicBaseUrl(config);
        return baseUrl != null ? $"{baseUrl}{relativePath}" : null;
    }

    /// <summary>
    /// Parses payload format: {prefix}{productId}_pi{imageId} or {prefix}{productId}
    /// Returns (productId, imageId) where imageId is null if not present, or 0 maps to null (primary).
    /// </summary>
    public static (int? productId, int? imageId) ParseProductImagePayload(string input, string prefix)
    {
        var remainder = input[prefix.Length..];

        // Check for _pi suffix: e.g. "3_pi16" or "3_pi0"
        var piIndex = remainder.IndexOf("_pi", StringComparison.Ordinal);
        if (piIndex >= 0)
        {
            var prodPart = remainder[..piIndex];
            var imgPart = remainder[(piIndex + 3)..]; // skip "_pi"
            if (int.TryParse(prodPart, out var prodId) && int.TryParse(imgPart, out var imgId))
            {
                // imgId 0 = primary image, store as null
                return (prodId, imgId == 0 ? null : imgId);
            }
            return (null, null);
        }

        // Legacy format: just productId
        if (int.TryParse(remainder, out var legacyProdId))
            return (legacyProdId, null);

        return (null, null);
    }
}
