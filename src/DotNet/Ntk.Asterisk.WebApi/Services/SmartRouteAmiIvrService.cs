using System.Collections.Concurrent;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Services.SmartRouting;

namespace Ntk.Asterisk.WebApi.Services;

/// <summary>
/// Hosted service that listens to Asterisk AMI events during IVR execution
/// and delegates live IVR channel intercept to Scenario2_LiveIvrAmiScenario.
/// </summary>
public sealed class SmartRouteAmiIvrService : IHostedService, IDisposable
{
    private readonly IAmiSession _ami;
    private readonly Scenario2_LiveIvrAmiScenario _scenario;
    private readonly ILogger<SmartRouteAmiIvrService> _logger;

    private readonly ConcurrentDictionary<string, string> _channelCallerIds =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, (string Channel, string Context, string UniqueId)> _pendingIvrChannels =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _processedIvrChannels =
        new(StringComparer.OrdinalIgnoreCase);

    public SmartRouteAmiIvrService(
        IAmiSession ami,
        Scenario2_LiveIvrAmiScenario scenario,
        ILogger<SmartRouteAmiIvrService> logger)
    {
        _ami = ami ?? throw new ArgumentNullException(nameof(ami));
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent += OnAmiEvent;
        _logger.LogInformation("SmartRouteAmiIvrService started (delegating to Scenario2_LiveIvrAmiScenario)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent -= OnAmiEvent;
        _logger.LogInformation("SmartRouteAmiIvrService stopped");
        return Task.CompletedTask;
    }

    private void OnAmiEvent(object? sender, ManagerEvent e)
    {
        // 1. Correlate CallerID from any channel lifecycle event
        TrackChannelCallerId(e);

        // 2. Handle Hangup to cleanup correlation cache
        if (e is HangupEvent he)
        {
            var chan = he.Channel ?? GetAttr(he, "channel");
            var uid = he.UniqueId ?? GetAttr(he, "uniqueid");
            if (!string.IsNullOrWhiteSpace(chan))
            {
                _channelCallerIds.TryRemove(chan, out _);
                _pendingIvrChannels.TryRemove(chan, out _);
            }
            if (!string.IsNullOrWhiteSpace(uid))
            {
                _channelCallerIds.TryRemove(uid, out _);
                _pendingIvrChannels.TryRemove(uid, out _);
            }
            return;
        }

        // 3. Inspect NewExtenEvent for IVR execution
        if (e is not NewExtenEvent ne) return;

        var context = ne.Context ?? GetAttr(ne, "context") ?? string.Empty;
        var app = ne.Application ?? GetAttr(ne, "application") ?? string.Empty;

        var isIvr = context.StartsWith("ivr-", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(app, "Background", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(app, "WaitExten", StringComparison.OrdinalIgnoreCase);

        if (!isIvr) return;

        var channel = ne.Channel ?? GetAttr(ne, "channel") ?? string.Empty;
        var uniqueId = ne.UniqueId ?? GetAttr(ne, "uniqueid") ?? channel;

        if (string.IsNullOrWhiteSpace(channel)) return;

        // Resolve CallerID from event or from correlated channel cache
        var callerId = ResolveCallerId(ne, channel, uniqueId);

        if (string.IsNullOrWhiteSpace(callerId))
        {
            _pendingIvrChannels[channel] = (channel, context, uniqueId);
            _pendingIvrChannels[uniqueId] = (channel, context, uniqueId);
            _logger.LogDebug("AMI Live IVR: Channel {Channel} entered IVR {Context}; waiting for CallerID event", channel, context);
            return;
        }

        TriggerScenarioEvaluation(channel, context, uniqueId, callerId);
    }

    private void TrackChannelCallerId(ManagerEvent me)
    {
        string? channel = me.Channel ?? GetAttr(me, "channel");
        string? uniqueId = me.UniqueId ?? GetAttr(me, "uniqueid");
        string? callerId = null;

        if (me is AbstractChannelEvent ace)
        {
            channel = ace.Channel ?? channel;
            uniqueId = ace.UniqueId ?? uniqueId;
            callerId = ace.CallerIdNum ?? ace.CallerId;
        }

        callerId ??= GetAttr(me, "calleridnum") ??
                     GetAttr(me, "callerid");

        if (string.IsNullOrWhiteSpace(callerId)) return;

        var cleanCaller = callerId.Trim();
        if (cleanCaller.Length > 0 && cleanCaller != "unknown" && cleanCaller != "<unknown>")
        {
            if (!string.IsNullOrWhiteSpace(channel))
            {
                _channelCallerIds[channel] = cleanCaller;
            }
            if (!string.IsNullOrWhiteSpace(uniqueId))
            {
                _channelCallerIds[uniqueId] = cleanCaller;
            }

            // Check if there is a pending IVR channel waiting for this CallerID
            if (!string.IsNullOrWhiteSpace(channel) && _pendingIvrChannels.TryRemove(channel, out var pending))
            {
                _pendingIvrChannels.TryRemove(pending.UniqueId, out _);
                TriggerScenarioEvaluation(pending.Channel, pending.Context, pending.UniqueId, cleanCaller);
            }
            else if (!string.IsNullOrWhiteSpace(uniqueId) && _pendingIvrChannels.TryRemove(uniqueId, out var pendingUid))
            {
                _pendingIvrChannels.TryRemove(pendingUid.Channel, out _);
                TriggerScenarioEvaluation(pendingUid.Channel, pendingUid.Context, pendingUid.UniqueId, cleanCaller);
            }
        }
    }

    private void TriggerScenarioEvaluation(string channel, string context, string uniqueId, string callerId)
    {
        var dedupeKey = $"{uniqueId}:{context}";
        if (!_processedIvrChannels.TryAdd(dedupeKey, DateTimeOffset.UtcNow))
        {
            return;
        }

        CleanupCache();

        _ = _scenario.ExecuteLiveIvrInterceptAsync(channel, context, uniqueId, callerId);
    }

    private string ResolveCallerId(NewExtenEvent ne, string channel, string uniqueId)
    {
        var callerId = GetAttr(ne, "calleridnum") ?? GetAttr(ne, "callerid");
        if (!string.IsNullOrWhiteSpace(callerId) && callerId != "unknown" && callerId != "<unknown>")
        {
            return callerId.Trim();
        }

        if (_channelCallerIds.TryGetValue(channel, out var cachedChanCaller))
        {
            return cachedChanCaller;
        }

        if (!string.IsNullOrWhiteSpace(uniqueId) && _channelCallerIds.TryGetValue(uniqueId, out var cachedUniCaller))
        {
            return cachedUniCaller;
        }

        return string.Empty;
    }

    private static string? GetAttr(ManagerEvent me, string key)
    {
        if (me.Attributes == null) return null;
        foreach (var (k, v) in me.Attributes)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                return v;
            }
        }
        return null;
    }

    private void CleanupCache()
    {
        if (_processedIvrChannels.Count <= 300) return;
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
        foreach (var (k, v) in _processedIvrChannels)
        {
            if (v < cutoff) _processedIvrChannels.TryRemove(k, out _);
        }
    }

    public void Dispose()
    {
        _ami.AmiEvent -= OnAmiEvent;
    }
}
