namespace Ntk.Asterisk.WebApi.Contracts;

public sealed class ApiResult<T>
{
    public bool IsSuccess { get; init; }
    public T[] Data { get; init; } = Array.Empty<T>();
    public string? ErrorMessage { get; init; }

    public static ApiResult<T> Ok(params T[] items) =>
        new() { IsSuccess = true, Data = items ?? Array.Empty<T>(), ErrorMessage = null };

    public static ApiResult<T> Ok(IEnumerable<T> items) =>
        new() { IsSuccess = true, Data = items?.ToArray() ?? Array.Empty<T>(), ErrorMessage = null };

    public static ApiResult<T> Fail(string errorMessage) =>
        new() { IsSuccess = false, Data = Array.Empty<T>(), ErrorMessage = errorMessage };
}

public static class ApiResult
{
    public static ApiResult<object> OkEmpty() => ApiResult<object>.Ok();
    public static ApiResult<object> Fail(string errorMessage) => ApiResult<object>.Fail(errorMessage);
}
