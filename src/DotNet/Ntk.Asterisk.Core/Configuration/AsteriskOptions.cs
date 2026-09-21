using System.ComponentModel.DataAnnotations;

namespace Ntk.Asterisk.Core.Configuration;

public class AsteriskOptions
{
    public const string SectionName = "Asterisk";
    public List<AsteriskServerConfig> Servers { get; set; } = [];
    public AsteriskServerConfig? GetServer(string? serverId) => string.IsNullOrWhiteSpace(serverId) ? Servers.FirstOrDefault() : Servers.FirstOrDefault(s => s.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));
}

public class AsteriskServerConfig
{
    [Required] public string Id { get; set; } = string.Empty;
    [Required] public string Host { get; set; } = string.Empty;
    public AmiConfig Ami { get; set; } = new();
    public AriConfig Ari { get; set; } = new();
    public FastAgiConfig FastAgi { get; set; } = new();
}

public class AmiConfig
{
    [Range(1, 65535)] public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    [Range(1, 300000)] public int TimeoutMs { get; set; }
    [Range(0, 300000)] public int PingIntervalMs { get; set; }
    public bool KeepAlive { get; set; }
    public bool AutoReconnect { get; set; }
    public bool FireAllEvents { get; set; }
}

public class AriConfig
{
    [Required, Url] public string BaseUrl { get; set; } = string.Empty;
    [Required] public string Application { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class FastAgiConfig
{
    [Range(1, 65535)] public int ListenPort { get; set; }
    [Range(1, 10000)] public int MaxConcurrentConnections { get; set; }
}
