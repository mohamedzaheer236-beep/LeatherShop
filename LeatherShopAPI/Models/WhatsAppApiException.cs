namespace LeatherShopAPI.Models;

/// <summary>
/// Exception thrown when the WhatsApp Cloud API returns a non-success response.
/// Allows callers to catch WhatsApp-specific failures distinctly from other errors.
/// </summary>
public class WhatsAppApiException : Exception
{
    public WhatsAppApiException(string message) : base(message) { }
    public WhatsAppApiException(string message, Exception innerException) : base(message, innerException) { }
}
