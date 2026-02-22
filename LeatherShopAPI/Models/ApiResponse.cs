namespace LeatherShopAPI.Models;

/// <summary>
/// Unified API response wrapper for consistent response shape.
/// Every API endpoint returns this envelope.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success")
    {
        return new ApiResponse<T> { Success = true, Message = message, Data = data };
    }

    public static ApiResponse<T> Fail(string message, List<string>? errors = null)
    {
        return new ApiResponse<T> { Success = false, Message = message, Errors = errors };
    }
}

/// <summary>
/// Non-generic version for endpoints that return no data.
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string>? Errors { get; set; }

    public static ApiResponse Ok(string message = "Success")
    {
        return new ApiResponse { Success = true, Message = message };
    }

    public static ApiResponse Fail(string message, List<string>? errors = null)
    {
        return new ApiResponse { Success = false, Message = message, Errors = errors };
    }
}
