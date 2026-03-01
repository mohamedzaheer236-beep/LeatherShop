namespace LeatherShopAPI.Extensions;

/// <summary>
/// Utility methods for safe SQL query construction.
/// </summary>
public static class SqlHelper
{
    /// <summary>
    /// Escapes SQL LIKE/ILIKE wildcard characters (%, _, \) in user input
    /// so they are treated as literal characters instead of pattern wildcards.
    /// </summary>
    public static string EscapeLikePattern(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
