using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Hubs;

namespace Ntk.Asterisk.WebApi.Services;

public sealed class AmiLiveEventFeed : ILiveEventFeed, IHostedService
{
    public const int Capacity = 2000;
    public const string HubGroup = "events";
    public const string HubMethod = "LiveEvent";

    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "secret", "password", "passwd", "token", "authorization", "auth", "apikey", "api_key", "md5secret"
    };

    private readonly IAmiSession _ami;
    private readonly IHubContext<AsteriskHub> _hub;
    private readonly ILogger<AmiLiveEventFeed> _logger;
    private readonly ConcurrentQueue<LiveEventDto> _buffer = new();
    private readonly object _trimGate = new();
    private int _count;

    public AmiLiveEventFeed(
        IAmiSession ami,
        IHubContext<AsteriskHub> hub,
        LiveEventSinkHolder sinkHolder,
        ILogger<AmiLiveEventFeed> logger)
    {
        _ami = ami;
        _hub = hub;
        _logger = logger;
        sinkHolder.Attach(this);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent += OnAmiEvent;
        _ami.ConnectionChanged += OnConnectionChanged;
        Publish(new LiveEventDto
        {
            Id = Guid.NewGuid().ToString("N"),
            AtUtc = DateTimeOffset.UtcNow,
            Source = "system",
            Category = "LiveEventFeed",
            Level = "Information",
            Message = "Live event feed started."
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent -= OnAmiEvent;
        _ami.ConnectionChanged -= OnConnectionChanged;
        return Task.CompletedTask;
    }

    public void Publish(LiveEventDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            item = CloneWithId(item, Guid.NewGuid().ToString("N"));
        }

        _buffer.Enqueue(item);
        Interlocked.Increment(ref _count);
        TrimIfNeeded();

        _ = _hub.Clients.Group(HubGroup).SendAsync(HubMethod, item);
    }

    public IReadOnlyList<LiveEventDto> Query(
        string? quickSearch,
        string? source,
        string? level,
        string? sortBy,
        string? sortDir)
    {
        IEnumerable<LiveEventDto> items = _buffer.ToArray();

        if (!string.IsNullOrWhiteSpace(source))
        {
            var s = source.Trim();
            items = items.Where(e => e.Source.Equals(s, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            var l = level.Trim();
            items = items.Where(e => e.Level.Equals(l, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(quickSearch))
        {
            var q = quickSearch.Trim();
            items = items.Where(e =>
                Contains(e.Id, q)
                || Contains(e.Source, q)
                || Contains(e.Category, q)
                || Contains(e.Level, q)
                || Contains(e.Message, q)
                || Contains(e.Channel, q)
                || Contains(e.UniqueId, q)
                || Contains(e.Privilege, q)
                || (e.Attributes?.Any(kv => Contains(kv.Key, q) || Contains(kv.Value, q)) == true));
        }

        items = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("source", "asc") => items.OrderBy(e => e.Source),
            ("source", _) => items.OrderByDescending(e => e.Source),
            ("level", "asc") => items.OrderBy(e => e.Level),
            ("level", _) => items.OrderByDescending(e => e.Level),
            ("category", "asc") => items.OrderBy(e => e.Category),
            ("category", _) => items.OrderByDescending(e => e.Category),
            ("atutc", "asc") => items.OrderBy(e => e.AtUtc),
            _ => items.OrderByDescending(e => e.AtUtc)
        };

        return items.ToList();
    }

    public void Clear()
    {
        while (_buffer.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _count, 0);
        Publish(new LiveEventDto
        {
            Id = Guid.NewGuid().ToString("N"),
            AtUtc = DateTimeOffset.UtcNow,
            Source = "system",
            Category = "LiveEventFeed",
            Level = "Information",
            Message = "Event buffer cleared."
        });
    }

    private void OnAmiEvent(object? sender, ManagerEvent e)
    {
        try
        {
            Publish(FromAmi(e));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish AMI live event");
        }
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        var status = _ami.GetStatus();
        var host = status.Host ?? "?";
        var port = status.Port?.ToString() ?? "?";
        Publish(new LiveEventDto
        {
            Id = Guid.NewGuid().ToString("N"),
            AtUtc = DateTimeOffset.UtcNow,
            Source = "system",
            Category = "AmiConnection",
            Level = status.Connected ? "Information" : "Warning",
            Message = status.Connected
                ? $"AMI connected to {host}:{port}" + (string.IsNullOrWhiteSpace(status.AsteriskVersion) ? "" : $" ({status.AsteriskVersion})")
                : $"AMI disconnected ({host}:{port})" + (string.IsNullOrWhiteSpace(status.LastError) ? "" : $": {status.LastError}")
        });
    }

    private static LiveEventDto FromAmi(ManagerEvent e)
    {
        var typeName = e.GetType().Name;
        if (typeName.EndsWith("Event", StringComparison.Ordinal))
            typeName = typeName[..^5];

        var attrs = SanitizeAttributes(e.Attributes);
        var parts = new List<string> { typeName };
        if (!string.IsNullOrWhiteSpace(e.Channel))
            parts.Add($"ch={e.Channel}");
        if (!string.IsNullOrWhiteSpace(e.UniqueId))
            parts.Add($"uid={e.UniqueId}");
        if (attrs is { Count: > 0 })
        {
            foreach (var kv in attrs.Take(6))
                parts.Add($"{kv.Key}={kv.Value}");
        }

        return new LiveEventDto
        {
            Id = Guid.NewGuid().ToString("N"),
            AtUtc = e.DateReceived == default
                ? DateTimeOffset.UtcNow
                : new DateTimeOffset(DateTime.SpecifyKind(e.DateReceived, DateTimeKind.Local)).ToUniversalTime(),
            Source = "ami",
            Category = typeName,
            Level = "Event",
            Message = string.Join(" · ", parts),
            Channel = string.IsNullOrWhiteSpace(e.Channel) ? null : e.Channel,
            UniqueId = string.IsNullOrWhiteSpace(e.UniqueId) ? null : e.UniqueId,
            Privilege = string.IsNullOrWhiteSpace(e.Privilege) ? null : e.Privilege,
            Attributes = attrs
        };
    }

    private static IReadOnlyDictionary<string, string>? SanitizeAttributes(Dictionary<string, string>? raw)
    {
        if (raw is null || raw.Count == 0)
            return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw.Take(24))
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;
            if (SecretKeys.Contains(kv.Key) || SecretKeys.Any(s => kv.Key.Contains(s, StringComparison.OrdinalIgnoreCase)))
            {
                result[kv.Key] = "***";
                continue;
            }
            var val = kv.Value ?? string.Empty;
            if (val.Length > 200)
                val = val[..200] + "…";
            result[kv.Key] = val;
        }

        return result.Count == 0 ? null : result;
    }

    private void TrimIfNeeded()
    {
        if (Volatile.Read(ref _count) <= Capacity)
            return;

        lock (_trimGate)
        {
            while (Volatile.Read(ref _count) > Capacity && _buffer.TryDequeue(out _))
                Interlocked.Decrement(ref _count);
        }
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static LiveEventDto CloneWithId(LiveEventDto item, string id) =>
        new()
        {
            Id = id,
            AtUtc = item.AtUtc,
            Source = item.Source,
            Category = item.Category,
            Level = item.Level,
            Message = item.Message,
            Channel = item.Channel,
            UniqueId = item.UniqueId,
            Privilege = item.Privilege,
            Attributes = item.Attributes
        };
}
