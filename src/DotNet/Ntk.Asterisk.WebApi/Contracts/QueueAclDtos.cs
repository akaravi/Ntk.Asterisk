namespace Ntk.Asterisk.WebApi.Contracts;

public sealed class QueueAclUserDto
{
    public string Id { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    /// <summary>viewer | admin</summary>
    public string Role { get; init; } = "viewer";
    /// <summary>Display queue names (after rename). Empty + non-admin → no queues.</summary>
    public IReadOnlyList<string> AllowedQueues { get; init; } = Array.Empty<string>();
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class QueueAclUserUpsertRequest
{
    public string? Id { get; set; }
    public string Username { get; set; } = string.Empty;
    /// <summary>Blank on update keeps existing password.</summary>
    public string? Password { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Role { get; set; } = "viewer";
    public List<string>? AllowedQueues { get; set; }
}

public sealed class QueueAclLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class QueueAclSessionDto
{
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = "viewer";
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public IReadOnlyList<string> AllowedQueues { get; init; } = Array.Empty<string>();
    public bool IsAdmin { get; init; }
}

public sealed class QueueAclStatusDto
{
    public bool RequireLogin { get; init; }
    public bool GateActive { get; init; }
    public bool IsAuthenticated { get; init; }
    public string? Username { get; init; }
    public string? Role { get; init; }
    public IReadOnlyList<string> AllowedQueues { get; init; } = Array.Empty<string>();
    public bool IsAdmin { get; init; }
    public int UserCount { get; init; }
}

public sealed class QueueAclOptionsUpdateRequest
{
    public bool? RequireLogin { get; set; }
}
