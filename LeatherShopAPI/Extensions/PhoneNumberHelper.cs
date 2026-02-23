namespace LeatherShopAPI.Extensions;

/// <summary>
/// Normalizes phone numbers to a consistent format without '+' prefix.
/// WhatsApp Cloud API sends/receives numbers as '919876543210' (no '+').
/// All phone numbers in the database use this format to prevent duplicates.
/// </summary>
public static class PhoneNumberHelper
{
    /// <summary>
    /// Normalize a phone number by stripping whitespace, dashes, parentheses, and the leading '+'.
    /// Returns a clean numeric string like "919876543210".
    /// </summary>
    public static string Normalize(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        return phone
            .Trim()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "")
            .TrimStart('+');
    }
}
