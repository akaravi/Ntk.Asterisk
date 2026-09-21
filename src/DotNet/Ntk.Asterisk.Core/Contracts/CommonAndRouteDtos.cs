namespace Ntk.Asterisk.Core.Contracts;

public class ApiResult<T>
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = [];

    public static ApiResult<T> Success(T data, string? message = null) =>
        new() { IsSuccess = true, Data = data, Message = message };

    public static ApiResult<T> Fail(string error) =>
        new() { IsSuccess = false, Message = error, Errors = [error] };

    public static ApiResult<T> Fail(List<string> errors, string? message = null) =>
        new() { IsSuccess = false, Message = message ?? "Validation or operation failed.", Errors = errors };
}

public class ApiResult : ApiResult<object>
{
    public static ApiResult Ok(string? message = null) =>
        new() { IsSuccess = true, Message = message };

    public static new ApiResult Fail(string error) =>
        new() { IsSuccess = false, Message = error, Errors = [error] };
}

public class CallRouteDto
{
    public string Id { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public string? ServerId { get; set; }
}

public class CreateCallRouteRequest
{
    public string Pattern { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? ServerId { get; set; }
}

public class UpdateCallRouteRequest
{
    public string Pattern { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? ServerId { get; set; }
}
