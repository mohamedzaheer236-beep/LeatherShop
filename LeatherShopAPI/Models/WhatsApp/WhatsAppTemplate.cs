namespace LeatherShopAPI.Models.WhatsApp;

/// <summary>
/// Metadata for an approved WhatsApp message template fetched from Meta Business API.
/// Includes carousel detection, parameter requirements, and image header flags.
/// </summary>
public class WhatsAppTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    /// <summary>True if the template contains a CAROUSEL component.</summary>
    public bool IsCarousel { get; set; }

    /// <summary>Number of cards defined in the carousel (0 for non-carousel templates).</summary>
    public int CardCount { get; set; }

    /// <summary>True if the template has a HEADER component with IMAGE format.</summary>
    public bool HasImageHeader { get; set; }

    /// <summary>Number of body parameters expected (e.g., 2 means {{1}} and {{2}}).</summary>
    public int BodyParamCount { get; set; }

    /// <summary>Max characters allowed for a carousel card body parameter (160 - static text length). 0 for non-carousel.</summary>
    public int CardBodyMaxLength { get; set; }
}
