using Microsoft.AspNetCore.SignalR;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Hubs;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Jobs;

public interface ICallJobEngine
{
    Task<CallJobDto> AddAsync(CallJobAddRequest request, CancellationToken cancellationToken = default);
    Task<CallJobDto?> CancelAsync(string id, CancellationToken cancellationToken = default);
    Task<CallJobDto> EnqueueHangupAsync(string channel, CancellationToken cancellationToken = default);
    Task<CallJobDto> EnqueueBridgeAsync(string channel1, string channel2, string tone, CancellationToken cancellationToken = default);
}

public sealed class CallJobEngine : ICallJobEngine, IHostedService, IDisposable
{
    private readonly ICallJobStore _store;
    private readonly IAmiSession _ami;
    private readonly IAsteriskSettingsService _settings;
    private readonly IHubContext<AsteriskHub> _hub;
    private readonly ILogger<CallJobEngine> _logger;
    private readonly System.Threading.Channels.Channel<string> _queue =
        System.Threading.Channels.Channel.CreateUnbounded<string>();
    private CancellationTokenSource? _workerCts;
    private Task? _worker;

    public CallJobEngine(
        ICallJobStore store,
        IAmiSession ami,
        IAsteriskSettingsService settings,
        IHubContext<AsteriskHub> hub,
        ILogger<CallJobEngine> logger)
    {
        _store = store;
        _ami = ami;
        _settings = settings;
        _hub = hub;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent += OnAmiEvent;
        _workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _worker = Task.Run(() => ProcessLoopAsync(_workerCts.Token), _workerCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent -= OnAmiEvent;
        if (_workerCts is not null)
        {
            _workerCts.Cancel();
            try
            {
                if (_worker is not null)
                    await _worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        }
    }

    public async Task<CallJobDto> AddAsync(CallJobAddRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryParseType(request.Type, out var type) || type is CallJobType.CommandHangup or CallJobType.CommandBridge)
            throw new ArgumentException("Invalid CallJob type. Use ExtToExt, MobileToExt, or MobileToMobile.");

        var opt = _settings.GetEffective();
        var job = new CallJob
        {
            Type = type,
            From = request.From?.Trim(),
            To = request.To?.Trim(),
            Mobile1 = request.Mobile1?.Trim(),
            Mobile2 = request.Mobile2?.Trim(),
            Trunk = string.IsNullOrWhiteSpace(request.Trunk) ? opt.DefaultTrunk : request.Trunk.Trim(),
            CallerId = string.IsNullOrWhiteSpace(request.CallerId) ? opt.DefaultCallerId : request.CallerId.Trim(),
            TimeoutMs = request.TimeoutSec is > 0 ? request.TimeoutSec.Value * 1000 : opt.DefaultTimeoutMs,
            ActionId = $"job_{Guid.NewGuid():N}",
            Cts = new CancellationTokenSource()
        };

        ValidateDialJob(job);
        _store.Add(job);
        await PublishJobAsync(job).ConfigureAwait(false);
        await _queue.Writer.WriteAsync(job.Id, cancellationToken).ConfigureAwait(false);
        return _store.ToDto(job);
    }

    public async Task<CallJobDto> EnqueueHangupAsync(string channel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Channel is required.");

        var job = new CallJob
        {
            Type = CallJobType.CommandHangup,
            IsCommandJob = true,
            Channel = channel.Trim(),
            ActionId = $"cmd_hangup_{Guid.NewGuid():N}",
            Cts = new CancellationTokenSource()
        };
        _store.Add(job);
        await PublishJobAsync(job).ConfigureAwait(false);
        await _queue.Writer.WriteAsync(job.Id, cancellationToken).ConfigureAwait(false);
        return _store.ToDto(job);
    }

    public async Task<CallJobDto> EnqueueBridgeAsync(
        string channel1,
        string channel2,
        string tone,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channel1) || string.IsNullOrWhiteSpace(channel2))
            throw new ArgumentException("Channel1 and Channel2 are required.");

        var job = new CallJob
        {
            Type = CallJobType.CommandBridge,
            IsCommandJob = true,
            From = channel1.Trim(),
            To = channel2.Trim(),
            CallerId = string.IsNullOrWhiteSpace(tone) ? "no" : tone.Trim(),
            ActionId = $"cmd_bridge_{Guid.NewGuid():N}",
            Cts = new CancellationTokenSource()
        };
        _store.Add(job);
        await PublishJobAsync(job).ConfigureAwait(false);
        await _queue.Writer.WriteAsync(job.Id, cancellationToken).ConfigureAwait(false);
        return _store.ToDto(job);
    }

    public async Task<CallJobDto?> CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGet(id, out var job) || job is null)
            return null;

        if (job.State is CallJobState.Completed or CallJobState.Failed or CallJobState.Cancelled)
            return _store.ToDto(job);

        try { job.Cts?.Cancel(); } catch { /* ignore */ }

        if (!string.IsNullOrWhiteSpace(job.Channel))
        {
            try
            {
                await _ami.SendActionAsync(new HangupAction(job.Channel), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cancel hangup failed for job {JobId} channel {Channel}", job.Id, job.Channel);
            }
        }

        SetState(job, CallJobState.Cancelled, null, "Cancelled by user");
        await PublishJobAsync(job).ConfigureAwait(false);
        return _store.ToDto(job);
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        await foreach (var jobId in _queue.Reader.ReadAllAsync(ct))
        {
            if (!_store.TryGet(jobId, out var job) || job is null)
                continue;

            try
            {
                await ExecuteJobAsync(job, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CallJob {JobId} failed", job.Id);
                SetState(job, CallJobState.Failed, ex.Message);
                await PublishJobAsync(job).ConfigureAwait(false);
            }
        }
    }

    private async Task ExecuteJobAsync(CallJob job, CancellationToken hostCt)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(hostCt, job.Cts?.Token ?? CancellationToken.None);
        var ct = linked.Token;

        if (job.Type == CallJobType.CommandHangup)
        {
            SetState(job, CallJobState.DialingLeg1, null);
            await PublishJobAsync(job).ConfigureAwait(false);
            var resp = await _ami.SendActionAsync(new HangupAction(job.Channel!), ct).ConfigureAwait(false);
            if (resp.IsSuccess())
            {
                SetState(job, CallJobState.Completed, null, "Hangup completed successfully");
            }
            else
            {
                SetState(job, CallJobState.Failed, resp.Message);
            }

            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        if (job.Type == CallJobType.CommandBridge)
        {
            SetState(job, CallJobState.DialingLeg1, null);
            await PublishJobAsync(job).ConfigureAwait(false);
            var resp = await _ami.SendActionAsync(
                new BridgeAction(job.From!, job.To!, job.CallerId ?? "no"), ct).ConfigureAwait(false);
            if (resp.IsSuccess())
            {
                SetState(job, CallJobState.Bridged, null);
                SetState(job, CallJobState.Completed, null, "Bridge completed successfully");
            }
            else
            {
                SetState(job, CallJobState.Failed, resp.Message);
            }

            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        var opt = _settings.GetEffective();
        var tech = string.IsNullOrWhiteSpace(opt.ChannelTech) ? "PJSIP" : opt.ChannelTech.Trim();
        var (channel, dialData) = BuildOriginate(job, tech);
        job.Channel = channel;

        SetState(job, CallJobState.DialingLeg1, null);
        await PublishJobAsync(job).ConfigureAwait(false);

        var originate = new OriginateAction
        {
            Channel = channel,
            Application = "Dial",
            Data = dialData,
            Timeout = job.TimeoutMs,
            CallerId = job.CallerId ?? opt.DefaultCallerId ?? "NtkAsterisk",
            Async = true,
            ActionId = job.ActionId
        };

        _logger.LogInformation(
            "CallJob {JobId} Originate Channel={Channel} Data={Data} Tech={Tech} Trunk={Trunk}",
            job.Id, channel, dialData, tech, job.Trunk);

        var response = await _ami.SendActionAsync(originate, ct).ConfigureAwait(false);
        if (!response.IsSuccess())
        {
            SetState(job, CallJobState.Failed, response.Message ?? "Originate rejected");
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        SetState(job, CallJobState.WaitingAnswer, null);
        await PublishJobAsync(job).ConfigureAwait(false);

        // Async Originate: completion via OriginateResponse / Hangup events.
        // Soft wait until terminal state or timeout.
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(job.TimeoutMs + 15_000);
        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (job.State is CallJobState.Completed or CallJobState.Failed or CallJobState.Cancelled or CallJobState.Bridged)
                break;
            await Task.Delay(250, ct).ConfigureAwait(false);
        }

        if (job.State is CallJobState.WaitingAnswer or CallJobState.DialingLeg1 or CallJobState.DialingLeg2)
        {
            SetState(job, CallJobState.Failed, "Originate timed out waiting for events");
            await PublishJobAsync(job).ConfigureAwait(false);
        }
        else if (job.State == CallJobState.Bridged)
        {
            SetState(job, CallJobState.Completed, null, "Call bridged and completed");
            await PublishJobAsync(job).ConfigureAwait(false);
        }
    }

    private void OnAmiEvent(object? sender, ManagerEvent e)
    {
        try
        {
            if (e is OriginateResponseEvent ore)
            {
                var job = FindByActionId(ore.ActionId) ?? FindByChannel(ore.Channel);
                if (job is null) return;

                job.Channel ??= ore.Channel;
                job.UniqueId ??= ore.UniqueId;
                var ok = string.Equals(ore.Response, "Success", StringComparison.OrdinalIgnoreCase);
                if (ok)
                {
                    SetState(job, CallJobState.Bridged, null, "Originate success — legs bridging");
                }
                else
                {
                    SetState(job, CallJobState.Failed, FormatOriginateFailure(ore, job));
                }

                _ = PublishJobAsync(job);
                return;
            }

            if (e is HangupEvent he)
            {
                var job = FindByChannel(he.Channel) ?? FindByUniqueId(he.UniqueId);
                if (job is null) return;
                if (job.State is CallJobState.Bridged or CallJobState.WaitingAnswer or CallJobState.DialingLeg2)
                {
                    var hangupReason = string.IsNullOrWhiteSpace(he.CauseTxt)
                        ? $"Hangup cause={he.Cause}"
                        : $"Hangup: {he.CauseTxt} (cause={he.Cause})";
                    SetState(job, CallJobState.Completed, null, hangupReason);
                    _ = PublishJobAsync(job);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AMI event job correlation error");
        }
    }

    private CallJob? FindByActionId(string? actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) return null;
        return _store.GetAll().FirstOrDefault(j =>
            string.Equals(j.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
    }

    private CallJob? FindByChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return null;
        return _store.GetAll().FirstOrDefault(j =>
            !string.IsNullOrWhiteSpace(j.Channel)
            && string.Equals(j.Channel, channel, StringComparison.OrdinalIgnoreCase)
            && j.State is not CallJobState.Completed and not CallJobState.Failed and not CallJobState.Cancelled);
    }

    private CallJob? FindByUniqueId(string? uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId)) return null;
        return _store.GetAll().FirstOrDefault(j =>
            string.Equals(j.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
    }

    private static (string channel, string dialData) BuildOriginate(CallJob job, string tech)
    {
        var timeoutSec = Math.Max(1, job.TimeoutMs / 1000);
        return job.Type switch
        {
            CallJobType.ExtToExt => (
                FormatEndpoint(tech, job.From!),
                $"{FormatEndpoint(tech, job.To!)},{timeoutSec}"),
            CallJobType.MobileToExt => (
                FormatTrunkDial(tech, job.Trunk!, job.Mobile1!),
                $"{FormatEndpoint(tech, job.To!)},{timeoutSec}"),
            CallJobType.MobileToMobile => (
                FormatTrunkDial(tech, job.Trunk!, job.Mobile1!),
                $"{FormatTrunkDial(tech, job.Trunk!, job.Mobile2!)},{timeoutSec}"),
            _ => throw new InvalidOperationException($"Unsupported dial job type {job.Type}")
        };
    }

    /// <summary>
    /// Extension / local endpoint: PJSIP/100 or SIP/100.
    /// </summary>
    private static string FormatEndpoint(string tech, string endpoint) =>
        $"{NormalizeTech(tech)}/{endpoint.Trim()}";

    /// <summary>
    /// Outbound via trunk:
    /// PJSIP uses number@endpoint (FreePBX / Asterisk PJSIP) — not trunk/number.
    /// Legacy SIP/IAX2 keeps trunk/number.
    /// </summary>
    private static string FormatTrunkDial(string tech, string trunk, string number)
    {
        var t = NormalizeTech(tech);
        var trunkName = trunk.Trim();
        var num = number.Trim();
        if (string.Equals(t, "PJSIP", StringComparison.OrdinalIgnoreCase))
            return $"{t}/{num}@{trunkName}";
        return $"{t}/{trunkName}/{num}";
    }

    private static string NormalizeTech(string tech)
    {
        var t = string.IsNullOrWhiteSpace(tech) ? "PJSIP" : tech.Trim();
        if (string.Equals(t, "SIP", StringComparison.OrdinalIgnoreCase)) return "SIP";
        if (string.Equals(t, "IAX2", StringComparison.OrdinalIgnoreCase)) return "IAX2";
        return "PJSIP";
    }

    private static string FormatOriginateFailure(OriginateResponseEvent ore, CallJob job)
    {
        var reasonText = MapOriginateReason(ore.Reason);
        var channel = string.IsNullOrWhiteSpace(ore.Channel) ? job.Channel : ore.Channel;
        return
            $"OriginateResponse: {ore.Response} reason={ore.Reason} ({reasonText}). " +
            $"Channel={channel ?? "—"}. " +
            "Hint: for PJSIP use number@trunk (e.g. PJSIP/0912…@trunk-out); " +
            "verify endpoint with CLI `pjsip show endpoints` and DefaultTrunk name.";
    }

    private static string MapOriginateReason(int reason) => reason switch
    {
        0 => "no such endpoint/number or invalid channel tech/trunk",
        1 => "hangup / no answer (AST_CONTROL_HANGUP)",
        2 => "local ring",
        3 => "remote ringing / often answer timeout",
        4 => "answered",
        5 => "busy",
        8 => "congestion / unavailable",
        _ => "see Asterisk control-frame reason"
    };

    private static void ValidateDialJob(CallJob job)
    {
        switch (job.Type)
        {
            case CallJobType.ExtToExt:
                if (string.IsNullOrWhiteSpace(job.From) || string.IsNullOrWhiteSpace(job.To))
                    throw new ArgumentException("ExtToExt requires From and To extensions.");
                break;
            case CallJobType.MobileToExt:
                if (string.IsNullOrWhiteSpace(job.Mobile1) || string.IsNullOrWhiteSpace(job.To))
                    throw new ArgumentException("MobileToExt requires Mobile1 and To extension.");
                if (string.IsNullOrWhiteSpace(job.Trunk))
                    throw new ArgumentException("MobileToExt requires Trunk (or DefaultTrunk in config).");
                break;
            case CallJobType.MobileToMobile:
                if (string.IsNullOrWhiteSpace(job.Mobile1) || string.IsNullOrWhiteSpace(job.Mobile2))
                    throw new ArgumentException("MobileToMobile requires Mobile1 and Mobile2.");
                if (string.IsNullOrWhiteSpace(job.Trunk))
                    throw new ArgumentException("MobileToMobile requires Trunk (or DefaultTrunk in config).");
                break;
            default:
                throw new ArgumentException($"Unsupported type {job.Type}");
        }
    }

    private static bool TryParseType(string? raw, out CallJobType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var normalized = raw.Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse(normalized, true, out type);
    }

    private void SetState(CallJob job, CallJobState state, string? error, string? successReason = null)
    {
        var now = DateTimeOffset.UtcNow;
        if (job.StartedAtUtc is null
            && state is CallJobState.DialingLeg1
                or CallJobState.WaitingAnswer
                or CallJobState.DialingLeg2
                or CallJobState.Bridged)
        {
            job.StartedAtUtc = now;
        }

        job.State = state;
        if (error is not null)
        {
            job.ErrorMessage = error;
            job.ResultReason = error;
        }

        if (state is CallJobState.Completed or CallJobState.Failed or CallJobState.Cancelled)
        {
            job.EndedAtUtc ??= now;
            if (job.StartedAtUtc is not null)
            {
                job.DurationSeconds = (int)Math.Max(
                    0,
                    (job.EndedAtUtc.Value - job.StartedAtUtc.Value).TotalSeconds);
            }

            if (error is null)
            {
                job.ResultReason = state switch
                {
                    CallJobState.Cancelled => successReason ?? "Cancelled",
                    CallJobState.Completed => successReason ?? "Completed successfully",
                    _ => successReason
                };
            }
        }

        _store.Update(job);
    }

    private Task PublishJobAsync(CallJob job) =>
        _hub.Clients.Group("jobs").SendAsync("jobUpdated", _store.ToDto(job));

    public void Dispose()
    {
        _workerCts?.Cancel();
        _workerCts?.Dispose();
    }
}
