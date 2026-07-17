using System.Collections.Concurrent;
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
    private readonly ICallRecordingService _recording;
    private readonly IHubContext<AsteriskHub> _hub;
    private readonly ILogger<CallJobEngine> _logger;
    private readonly System.Threading.Channels.Channel<string> _queue =
        System.Threading.Channels.Channel.CreateUnbounded<string>();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<OriginateResponseEvent>> _originateWaiters =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _workerCts;
    private Task? _worker;

    public CallJobEngine(
        ICallJobStore store,
        IAmiSession ami,
        IAsteriskSettingsService settings,
        ICallRecordingService recording,
        IHubContext<AsteriskHub> hub,
        ILogger<CallJobEngine> logger)
    {
        _store = store;
        _ami = ami;
        _settings = settings;
        _recording = recording;
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

        ValidateDialJob(job, opt);
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

        if (!string.IsNullOrWhiteSpace(job.Channel2))
        {
            try
            {
                await _ami.SendActionAsync(new HangupAction(job.Channel2), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cancel hangup failed for job {JobId} channel2 {Channel}", job.Id, job.Channel2);
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
        if (IsLocalContext(opt))
        {
            // Application=Dial Local/leg2 — Asterisk stays B2BUA (two-way RTP). Context/Exten click-to-call
            // marks "bridged" when leg1 answers only; FreePBX dialplan often yields silent mobile↔mobile.
            await ExecuteLocalDialAppAsync(job, opt, ct).ConfigureAwait(false);
            return;
        }

        await ExecuteDirectTechDialAsync(job, opt, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// FreePBX Local + Application Dial: ring leg1, MOH until leg2 answers, then Dial owns the bridge/RTP.
    /// </summary>
    private async Task ExecuteLocalDialAppAsync(CallJob job, AsteriskOptions opt, CancellationToken ct)
    {
        var ctx = string.IsNullOrWhiteSpace(opt.OriginateContext) ? "from-internal" : opt.OriginateContext.Trim();
        var moh = string.IsNullOrWhiteSpace(opt.MusicOnHoldClass) ? "default" : opt.MusicOnHoldClass.Trim();
        var (leg1Number, leg2Number) = ResolveLegNumbers(job);
        var channel1Spec = FormatLocalChannel(leg1Number, ctx);
        var channel2Spec = FormatLocalChannel(leg2Number, ctx);
        var timeoutSec = Math.Max(1, job.TimeoutMs / 1000);
        // m(class)=MOH to leg1 while leg2 rings; tT=attended transfer keys (harmless); g=continue after Dial if needed
        var dialData = $"{channel2Spec},{timeoutSec},m({moh})tT";
        job.Channel = channel1Spec;
        job.OriginateDialData = $"LocalDialApp;Channel={channel1Spec};Dial={dialData}";

        SetState(job, CallJobState.DialingLeg1, null, $"Dialing leg1 via Local; then Dial({leg2Number})");
        await PublishJobAsync(job).ConfigureAwait(false);

        var originate = new OriginateAction
        {
            Channel = channel1Spec,
            Application = "Dial",
            Data = dialData,
            Timeout = job.TimeoutMs,
            CallerId = job.CallerId ?? opt.DefaultCallerId ?? "NtkCall",
            Async = true,
            ActionId = job.ActionId
        };
        // Keep Asterisk in media path where dialplan honors these (FreePBX outbound may still override).
        originate.SetVariable("DIAL_OPTIONS", $"m({moh})tT");

        var leg1 = await OriginateAndWaitAsync(
            job, job.ActionId!, originate, job.TimeoutMs + 5_000, ct).ConfigureAwait(false);

        if (!leg1.Ok)
        {
            SetState(job, CallJobState.Failed, leg1.Error ?? "Leg1 Originate/Dial failed");
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        job.Channel = leg1.Channel ?? channel1Spec;
        job.UniqueId = leg1.UniqueId ?? job.UniqueId;
        job.LinkedId ??= job.UniqueId;
        SetState(job, CallJobState.WaitingAnswer, null, $"Leg1 answered — MOH; ringing leg2 ({leg2Number})");
        await PublishJobAsync(job).ConfigureAwait(false);

        // DialEnd ANSWER (or BridgeEnter) promotes to Bridged — do not assume audio yet.
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(job.TimeoutMs + 30_000);
        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (job.State is CallJobState.Bridged or CallJobState.Completed or CallJobState.Failed or CallJobState.Cancelled)
                break;
            await Task.Delay(250, ct).ConfigureAwait(false);
        }

        if (job.State == CallJobState.WaitingAnswer)
        {
            await TryHangupAsync(job.Channel, ct).ConfigureAwait(false);
            SetState(job, CallJobState.Failed, "Leg2 did not answer (Dial timeout)");
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        if (job.State != CallJobState.Bridged)
            return;

        // Local path often yields silent mobile↔mobile (trunk directmedia + Local).
        // After both legs answer, Bridge the real SIP/PJSIP peers for two-way RTP.
        await PromoteSipMediaBridgeAsync(job, ct).ConfigureAwait(false);

        var talkDeadline = DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(job.TimeoutMs, 60_000) + 30_000);
        while (DateTimeOffset.UtcNow < talkDeadline && !ct.IsCancellationRequested)
        {
            if (job.State is CallJobState.Completed or CallJobState.Failed or CallJobState.Cancelled)
                break;
            await Task.Delay(250, ct).ConfigureAwait(false);
        }

        if (job.State == CallJobState.Bridged)
        {
            SetState(job, CallJobState.Completed, null, "Call bridged and completed");
            await PublishJobAsync(job).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Move RTP onto trunk SIP/PJSIP channels (B2BUA native bridge). Leave Local stubs alone.
    /// </summary>
    private async Task PromoteSipMediaBridgeAsync(CallJob job, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(job.Channel))
            return;

        string? sip1 = null;
        string? sip2 = null;
        string? leg2Local = job.Channel2;

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            await Task.Delay(attempt == 1 ? 400 : 350, ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(job.LinkedId))
            {
                job.LinkedId = await GetChannelVarAsync(job.Channel, "CHANNEL(linkedid)", ct).ConfigureAwait(false)
                    ?? job.UniqueId;
            }

            sip1 = await ResolveSipMediaChannelAsync(job.Channel, ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(leg2Local))
                leg2Local = await GetChannelVarAsync(job.Channel, "BRIDGEPEER", ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(leg2Local))
            {
                job.Channel2 ??= leg2Local;
                sip2 = await ResolveSipMediaChannelAsync(leg2Local, ct).ConfigureAwait(false);
            }

            // Observed BridgeEnter SIPs + Status scan by Linkedid (fixes sip1=null when Local;2 GetVar races).
            var fromStatus = await FindSipChannelsByLinkedIdAsync(job.LinkedId ?? job.UniqueId, ct)
                .ConfigureAwait(false);
            foreach (var s in fromStatus)
                job.ObservedSipChannels.Add(s);

            var pool = job.ObservedSipChannels
                .Where(IsSipLikeChannel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.IsNullOrWhiteSpace(sip1))
                sip1 = pool.FirstOrDefault(s =>
                    !string.Equals(s, sip2, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(sip2))
                sip2 = pool.FirstOrDefault(s =>
                    !string.Equals(s, sip1, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(sip1) && string.IsNullOrWhiteSpace(sip2) && pool.Count >= 2)
            {
                sip1 = pool[0];
                sip2 = pool[1];
            }

            _logger.LogInformation(
                "CallJob {JobId} PromoteSip try={Try} local1={L1} sip1={S1} local2={L2} sip2={S2} observed={Obs}",
                job.Id, attempt, job.Channel, sip1, leg2Local, sip2, string.Join(",", pool));

            if (!string.IsNullOrWhiteSpace(sip1)
                && !string.IsNullOrWhiteSpace(sip2)
                && !string.Equals(sip1, sip2, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(sip1) || string.IsNullOrWhiteSpace(sip2))
        {
            _logger.LogWarning(
                "CallJob {JobId} SIP promote skipped — peers incomplete (audio may stay on Local path)",
                job.Id);
            SetState(job, CallJobState.Bridged, null, "Dial connected (SIP promote skipped — check trunk directmedia)");
            await TryStartRecordingAsync(job, ct).ConfigureAwait(false);
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        if (string.Equals(sip1, sip2, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("CallJob {JobId} SIP promote aborted — both peers resolved to {Sip}", job.Id, sip1);
            return;
        }

        job.MediaChannel1 = sip1;
        job.MediaChannel2 = sip2;

        var bridge = await _ami.SendActionAsync(
            new BridgeAction(sip1, sip2, "no"), ct, timeoutMs: 10_000).ConfigureAwait(false);
        _logger.LogInformation(
            "CallJob {JobId} Promote SIP Bridge {Sip1} <-> {Sip2} success={Ok} msg={Msg}",
            job.Id, sip1, sip2, bridge.IsSuccess(), bridge.Message);

        if (!bridge.IsSuccess())
        {
            SetState(job, CallJobState.Bridged, null, $"Dial connected; SIP Bridge failed: {bridge.Message}");
            await TryStartRecordingAsync(job, ct).ConfigureAwait(false);
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        var linked = await WaitSipPeersLinkedAsync(sip1, sip2, 2_500, ct).ConfigureAwait(false);
        SetState(
            job,
            CallJobState.Bridged,
            null,
            linked
                ? $"Two-way SIP bridge {sip1} <-> {sip2}"
                : $"SIP Bridge accepted {sip1} <-> {sip2} (BRIDGEPEER pending)");
        await TryStartRecordingAsync(job, ct).ConfigureAwait(false);
        await PublishJobAsync(job).ConfigureAwait(false);
    }

    /// <summary>
    /// FreePBX Local legs: phone RTP lives on the SIP trunk channel (BRIDGEPEER of Local;2),
    /// not on Local;1 sitting in ConfBridge/MusicOnHold. Bridge the two SIP peers for audio.
    /// </summary>
    private async Task ExecuteLocalTwoLegSipBridgeAsync(CallJob job, AsteriskOptions opt, CancellationToken ct)
    {
        var ctx = string.IsNullOrWhiteSpace(opt.OriginateContext) ? "from-internal" : opt.OriginateContext.Trim();
        var moh = string.IsNullOrWhiteSpace(opt.MusicOnHoldClass) ? "default" : opt.MusicOnHoldClass.Trim();
        var (leg1Number, leg2Number) = ResolveLegNumbers(job);
        var channel1Spec = FormatLocalChannel(leg1Number, ctx);
        var channel2Spec = FormatLocalChannel(leg2Number, ctx);
        job.ActionIdLeg2 = $"{job.ActionId}_L2";
        job.Channel = channel1Spec;
        job.OriginateDialData = $"TwoLeg+SipBridge;L1={channel1Spec};L2={channel2Spec};MOH={moh}";

        SetState(job, CallJobState.DialingLeg1, null, $"Dialing leg1; MOH={moh}");
        await PublishJobAsync(job).ConfigureAwait(false);

        var leg1 = await OriginateAndWaitAsync(
            job,
            job.ActionId!,
            new OriginateAction
            {
                Channel = channel1Spec,
                Application = "MusicOnHold",
                Data = moh,
                Timeout = job.TimeoutMs,
                CallerId = job.CallerId ?? opt.DefaultCallerId ?? "NtkCall",
                Async = true,
                ActionId = job.ActionId
            },
            job.TimeoutMs + 5_000,
            ct).ConfigureAwait(false);

        if (!leg1.Ok)
        {
            SetState(job, CallJobState.Failed, leg1.Error ?? "Leg1 Originate failed");
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        job.Channel = leg1.Channel ?? channel1Spec;
        job.UniqueId = leg1.UniqueId ?? job.UniqueId;
        SetState(job, CallJobState.DialingLeg2, null, "Leg1 answered — MOH; dialing leg2");
        await PublishJobAsync(job).ConfigureAwait(false);

        var leg2 = await OriginateAndWaitAsync(
            job,
            job.ActionIdLeg2!,
            new OriginateAction
            {
                Channel = channel2Spec,
                Application = "Wait",
                Data = "3600",
                Timeout = job.TimeoutMs,
                CallerId = job.CallerId ?? opt.DefaultCallerId ?? "NtkCall",
                Async = true,
                ActionId = job.ActionIdLeg2
            },
            job.TimeoutMs + 5_000,
            ct).ConfigureAwait(false);

        if (!leg2.Ok)
        {
            await TryHangupAsync(job.Channel, ct).ConfigureAwait(false);
            SetState(job, CallJobState.Failed, leg2.Error ?? "Leg2 Originate failed");
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        job.Channel2 = leg2.Channel ?? channel2Spec;
        job.UniqueId2 = leg2.UniqueId;
        SetState(job, CallJobState.WaitingAnswer, null, "Leg2 answered — resolving SIP peers for Bridge");
        await PublishJobAsync(job).ConfigureAwait(false);

        // Allow dialplan/Local to settle BRIDGEPEER → SIP/…
        await Task.Delay(400, ct).ConfigureAwait(false);

        var sip1 = await ResolveSipMediaChannelAsync(job.Channel!, ct).ConfigureAwait(false);
        var sip2 = await ResolveSipMediaChannelAsync(job.Channel2!, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "CallJob {JobId} Bridge peers local1={Local1} sip1={Sip1} local2={Local2} sip2={Sip2}",
            job.Id, job.Channel, sip1, job.Channel2, sip2);

        if (string.IsNullOrWhiteSpace(sip1) || string.IsNullOrWhiteSpace(sip2))
        {
            _logger.LogWarning(
                "CallJob {JobId} SIP peers incomplete — falling back to click-to-call Context/Exten",
                job.Id);
            await TryHangupAsync(job.Channel, ct).ConfigureAwait(false);
            await TryHangupAsync(job.Channel2, ct).ConfigureAwait(false);
            await ExecuteLocalClickToCallAsync(job, opt, leg1Number, leg2Number, ctx, ct).ConfigureAwait(false);
            return;
        }

        job.MediaChannel1 = sip1;
        job.MediaChannel2 = sip2;

        var bridge = await _ami.SendActionAsync(
            new BridgeAction(sip1, sip2, "no"), ct, timeoutMs: 10_000).ConfigureAwait(false);
        _logger.LogInformation(
            "CallJob {JobId} AMI Bridge {Sip1} <-> {Sip2} success={Success} msg={Message}",
            job.Id, sip1, sip2, bridge.IsSuccess(), bridge.Message);
        if (!bridge.IsSuccess())
        {
            await TryHangupAsync(job.Channel, ct).ConfigureAwait(false);
            await TryHangupAsync(job.Channel2, ct).ConfigureAwait(false);
            await TryHangupAsync(sip1, ct).ConfigureAwait(false);
            await TryHangupAsync(sip2, ct).ConfigureAwait(false);
            SetState(job, CallJobState.Failed, bridge.Message ?? $"Bridge failed ({sip1} <-> {sip2})");
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        // Do NOT hang Local halves after SIP Bridge.
        // Hanging Local;1 often tears down the Local pair and can cascade Hangup to trunk RTP.
        // Leave MOH/Wait stubs; they clear when SIP peers hang up.
        var linked = await WaitSipPeersLinkedAsync(sip1, sip2, 3_000, ct).ConfigureAwait(false);
        if (!linked)
        {
            _logger.LogWarning(
                "CallJob {JobId} SIP Bridge accepted but BRIDGEPEER not mutual yet — media may still work",
                job.Id);
        }

        SetState(job, CallJobState.Bridged, null, $"Bridged SIP {sip1} <-> {sip2}");
        await TryStartRecordingAsync(job, ct).ConfigureAwait(false);
        await PublishJobAsync(job).ConfigureAwait(false);

        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(job.TimeoutMs, 60_000) + 30_000);
        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (job.State is CallJobState.Completed or CallJobState.Failed or CallJobState.Cancelled)
                break;
            await Task.Delay(250, ct).ConfigureAwait(false);
        }

        if (job.State == CallJobState.Bridged)
        {
            SetState(job, CallJobState.Completed, null, "Call bridged and completed");
            await PublishJobAsync(job).ConfigureAwait(false);
        }
    }

    /// <summary>Single Originate click-to-call — FreePBX Dial owns RTP (fallback).</summary>
    private async Task ExecuteLocalClickToCallAsync(
        CallJob job,
        AsteriskOptions opt,
        string leg1Number,
        string leg2Number,
        string ctx,
        CancellationToken ct)
    {
        var channel = FormatLocalChannel(leg1Number, ctx);
        job.Channel = channel;
        job.OriginateDialData = $"ClickToCall;Channel={channel};Context={ctx};Exten={leg2Number}";
        SetState(job, CallJobState.DialingLeg1, null, "Fallback click-to-call Context/Exten");
        await PublishJobAsync(job).ConfigureAwait(false);

        var originate = new OriginateAction
        {
            Channel = channel,
            Context = ctx,
            Exten = leg2Number,
            Priority = "1",
            Timeout = job.TimeoutMs,
            CallerId = job.CallerId ?? opt.DefaultCallerId ?? "NtkCall",
            Async = true,
            ActionId = job.ActionId
        };

        var result = await OriginateAndWaitAsync(
            job, job.ActionId!, originate, job.TimeoutMs + 5_000, ct).ConfigureAwait(false);
        if (!result.Ok)
        {
            SetState(job, CallJobState.Failed, result.Error ?? "Click-to-call Originate failed");
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        job.Channel = result.Channel ?? channel;
        job.UniqueId = result.UniqueId ?? job.UniqueId;
        SetState(job, CallJobState.Bridged, null, "Click-to-call connected via FreePBX Dial");
        await TryStartRecordingAsync(job, ct).ConfigureAwait(false);
        await PublishJobAsync(job).ConfigureAwait(false);

        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(job.TimeoutMs, 60_000) + 30_000);
        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (job.State is CallJobState.Completed or CallJobState.Failed or CallJobState.Cancelled)
                break;
            await Task.Delay(250, ct).ConfigureAwait(false);
        }

        if (job.State == CallJobState.Bridged)
        {
            SetState(job, CallJobState.Completed, null, "Call bridged and completed");
            await PublishJobAsync(job).ConfigureAwait(false);
        }
    }

    private async Task<string?> ResolveSipMediaChannelAsync(string localOrAnyChannel, CancellationToken ct)
    {
        if (IsSipLikeChannel(localOrAnyChannel))
            return localOrAnyChannel;

        // Prefer Local;2 (dial half → trunk). Never follow Dial's other Local leg (different pair).
        foreach (var candidate in ExpandLocalChannelCandidates(localOrAnyChannel))
        {
            var peer = await GetChannelVarAsync(candidate, "BRIDGEPEER", ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(peer))
                continue;

            if (IsSipLikeChannel(peer))
                return peer;

            if (peer.StartsWith("Local/", StringComparison.OrdinalIgnoreCase)
                && IsSameLocalPair(candidate, peer))
            {
                var nested = await GetChannelVarAsync(peer, "BRIDGEPEER", ct).ConfigureAwait(false);
                if (IsSipLikeChannel(nested))
                    return nested;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<string>> FindSipChannelsByLinkedIdAsync(
        string? linkedId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(linkedId))
            return Array.Empty<string>();

        try
        {
            var events = await _ami.SendEventGeneratingActionAsync(new StatusAction(), 8_000, ct)
                .ConfigureAwait(false);
            var found = new List<string>();
            foreach (var e in events.Events.OfType<StatusEvent>())
            {
                if (!IsSipLikeChannel(e.Channel))
                    continue;

                var lid = ReadEventAttr(e, "Linkedid", "LinkedId", "linkedid")
                    ?? e.UniqueId;
                if (string.Equals(lid, linkedId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e.UniqueId, linkedId, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(e.Channel!);
                }
            }

            return found;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "StatusAction SIP scan failed for linkedId={LinkedId}", linkedId);
            return Array.Empty<string>();
        }
    }

    private static string? ReadEventAttr(ManagerEvent e, params string[] keys)
    {
        if (e.Attributes is null) return null;
        foreach (var key in keys)
        {
            if (e.Attributes.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }

    private static bool IsSameLocalPair(string a, string b)
    {
        static string Base(string ch)
        {
            var i = ch.LastIndexOf(';');
            return i > 0 ? ch[..i] : ch;
        }

        return string.Equals(Base(a), Base(b), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> WaitSipPeersLinkedAsync(
        string sip1, string sip2, int timeoutMs, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var p1 = await GetChannelVarAsync(sip1, "BRIDGEPEER", ct).ConfigureAwait(false);
            var p2 = await GetChannelVarAsync(sip2, "BRIDGEPEER", ct).ConfigureAwait(false);
            var ok1 = string.Equals(p1, sip2, StringComparison.OrdinalIgnoreCase);
            var ok2 = string.Equals(p2, sip1, StringComparison.OrdinalIgnoreCase);
            if (ok1 || ok2)
                return true;
            await Task.Delay(200, ct).ConfigureAwait(false);
        }

        return false;
    }

    private async Task<string?> GetChannelVarAsync(string channel, string variable, CancellationToken ct)
    {
        try
        {
            var resp = await _ami.SendActionAsync(
                new GetVarAction(channel, variable), ct, timeoutMs: 1_500).ConfigureAwait(false);
            if (!resp.IsSuccess())
                return null;
            var value = resp.GetAttribute("Value") ?? resp.GetAttribute("value");
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GetVar {Variable} failed on {Channel}", variable, channel);
            return null;
        }
    }

    private static IEnumerable<string> ExpandLocalChannelCandidates(string channel)
    {
        // Dial half (;2) first — holds BRIDGEPEER to SIP/PJSIP.
        var opposite = OppositeLocalHalf(channel);
        if (!string.IsNullOrWhiteSpace(opposite)
            && opposite.EndsWith(";2", StringComparison.Ordinal)
            && !string.Equals(opposite, channel, StringComparison.Ordinal))
        {
            yield return opposite!;
        }

        yield return channel;

        if (!string.IsNullOrWhiteSpace(opposite)
            && !opposite.EndsWith(";2", StringComparison.Ordinal)
            && !string.Equals(opposite, channel, StringComparison.Ordinal))
        {
            yield return opposite!;
        }
    }

    private static string? OppositeLocalHalf(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return null;
        if (channel.EndsWith(";1", StringComparison.Ordinal))
            return channel[..^2] + ";2";
        if (channel.EndsWith(";2", StringComparison.Ordinal))
            return channel[..^2] + ";1";
        return null;
    }

    private static bool IsSipLikeChannel(string? channel) =>
        !string.IsNullOrWhiteSpace(channel)
        && (channel.StartsWith("SIP/", StringComparison.OrdinalIgnoreCase)
            || channel.StartsWith("PJSIP/", StringComparison.OrdinalIgnoreCase)
            || channel.StartsWith("IAX2/", StringComparison.OrdinalIgnoreCase));

    private async Task ExecuteDirectTechDialAsync(CallJob job, AsteriskOptions opt, CancellationToken ct)
    {
        var tech = string.IsNullOrWhiteSpace(opt.ChannelTech) ? "PJSIP" : opt.ChannelTech.Trim();
        var moh = string.IsNullOrWhiteSpace(opt.MusicOnHoldClass) ? "default" : opt.MusicOnHoldClass.Trim();
        var timeoutSec = Math.Max(1, job.TimeoutMs / 1000);
        var plan = BuildDirectTechPlan(job, tech, timeoutSec, moh);
        job.Channel = plan.Channel;
        job.OriginateDialData = plan.Diagnostic;

        SetState(job, CallJobState.DialingLeg1, null);
        await PublishJobAsync(job).ConfigureAwait(false);

        var originate = new OriginateAction
        {
            Channel = plan.Channel,
            Application = plan.Application,
            Data = plan.Data,
            Timeout = job.TimeoutMs,
            CallerId = job.CallerId ?? opt.DefaultCallerId ?? "NtkCall",
            Async = true,
            ActionId = job.ActionId
        };

        _logger.LogInformation(
            "CallJob {JobId} Originate Via=DirectTech Channel={Channel} Data={Data}",
            job.Id, plan.Channel, plan.Data);

        var response = await _ami.SendActionAsync(originate, ct).ConfigureAwait(false);
        if (!response.IsSuccess())
        {
            SetState(job, CallJobState.Failed, response.Message ?? "Originate rejected");
            await PublishJobAsync(job).ConfigureAwait(false);
            return;
        }

        SetState(job, CallJobState.WaitingAnswer, null, $"Waiting; MOH={moh} until leg2 answers");
        await PublishJobAsync(job).ConfigureAwait(false);

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

    private async Task<(bool Ok, string? Channel, string? UniqueId, string? Error)> OriginateAndWaitAsync(
        CallJob job,
        string actionId,
        OriginateAction action,
        int waitMs,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<OriginateResponseEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        _originateWaiters[actionId] = tcs;
        try
        {
            _logger.LogInformation(
                "CallJob {JobId} Originate ActionId={ActionId} Channel={Channel} App={App} Data={Data}",
                job.Id, actionId, action.Channel, action.Application, action.Data);

            var queued = await _ami.SendActionAsync(action, ct, timeoutMs: 10_000).ConfigureAwait(false);
            if (!queued.IsSuccess())
                return (false, null, null, queued.Message ?? "Originate rejected");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(waitMs);
            try
            {
                var ore = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                var ok = string.Equals(ore.Response, "Success", StringComparison.OrdinalIgnoreCase);
                if (!ok)
                    return (false, ore.Channel, ore.UniqueId, FormatOriginateFailure(ore, job));
                return (true, ore.Channel, ore.UniqueId, null);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return (false, null, null, "OriginateResponse timed out");
            }
        }
        finally
        {
            _originateWaiters.TryRemove(actionId, out _);
        }
    }

    private async Task TryHangupAsync(string? channel, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channel)) return;
        try
        {
            await _ami.SendActionAsync(new HangupAction(channel), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Hangup ignored for {Channel}", channel);
        }
    }

    private void OnAmiEvent(object? sender, ManagerEvent e)
    {
        try
        {
            if (e is OriginateResponseEvent ore)
            {
                if (!string.IsNullOrWhiteSpace(ore.ActionId)
                    && _originateWaiters.TryGetValue(ore.ActionId, out var waiter))
                {
                    waiter.TrySetResult(ore);
                    return;
                }

                var job = FindByActionId(ore.ActionId) ?? FindByChannel(ore.Channel);
                if (job is null) return;

                job.Channel ??= ore.Channel;
                job.UniqueId ??= ore.UniqueId;
                job.LinkedId ??= ore.UniqueId;
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

            if (e is DialEndEvent de)
            {
                var job = FindByChannel(de.Channel)
                    ?? FindByUniqueId(de.UniqueId)
                    ?? FindByUniqueId(de.SrcUniqueId)
                    ?? FindByUniqueId(de.DestUniqueId);
                if (job is null) return;
                // Only after leg1 answered and Application=Dial is ringing leg2.
                if (job.State is not CallJobState.WaitingAnswer and not CallJobState.DialingLeg2)
                    return;

                // DialEnd for FreePBX's outbound to leg1 uses Local;2 / SIP — ignore those.
                // Application=Dial runs on Local;1 (job.Channel from OriginateResponse).
                if (string.IsNullOrWhiteSpace(job.Channel)
                    || !string.Equals(de.Channel, job.Channel, StringComparison.OrdinalIgnoreCase))
                    return;

                var status = (de.DialStatus ?? string.Empty).Trim().ToUpperInvariant();
                if (status == "ANSWER")
                {
                    var dest = de.Destination;
                    if (string.IsNullOrWhiteSpace(dest) && de.Attributes is not null)
                    {
                        if (!de.Attributes.TryGetValue("destchannel", out dest))
                            de.Attributes.TryGetValue("DestChannel", out dest);
                    }
                    if (!string.IsNullOrWhiteSpace(dest))
                        job.Channel2 = dest;
                    if (!string.IsNullOrWhiteSpace(de.DestUniqueId))
                        job.UniqueId2 = de.DestUniqueId;
                    SetState(
                        job,
                        CallJobState.Bridged,
                        null,
                        $"Dial ANSWER — promoting SIP peers for media ({de.Channel} <-> {dest})");
                    _ = PublishJobAsync(job);
                }
                else if (status is "NOANSWER" or "BUSY" or "CANCEL" or "CONGESTION" or "CHANUNAVAIL" or "DONTCALL")
                {
                    SetState(job, CallJobState.Failed, $"Dial {status}");
                    _ = PublishJobAsync(job);
                }

                return;
            }

            if (e is BridgeEnterEvent be && IsSipLikeChannel(be.Channel))
            {
                var linked = ReadEventAttr(be, "Linkedid", "LinkedId", "linkedid") ?? be.UniqueId;
                var job = FindByChannel(be.Channel)
                    ?? FindByUniqueId(be.UniqueId)
                    ?? FindByUniqueId(linked)
                    ?? FindByLinkedId(linked);
                if (job is null) return;

                job.ObservedSipChannels.Add(be.Channel!);
                job.LinkedId ??= linked;

                if (job.State is CallJobState.WaitingAnswer or CallJobState.DialingLeg2 or CallJobState.Bridged)
                {
                    if (string.IsNullOrWhiteSpace(job.MediaChannel1))
                        job.MediaChannel1 = be.Channel;
                    else if (!string.Equals(job.MediaChannel1, be.Channel, StringComparison.OrdinalIgnoreCase)
                             && string.IsNullOrWhiteSpace(job.MediaChannel2))
                        job.MediaChannel2 = be.Channel;
                }

                return;
            }

            if (e is NewChannelEvent nce && IsSipLikeChannel(nce.Channel))
            {
                var linked = ReadEventAttr(nce, "Linkedid", "LinkedId", "linkedid") ?? nce.UniqueId;
                var job = FindByUniqueId(linked) ?? FindByLinkedId(linked);
                if (job is null) return;
                job.ObservedSipChannels.Add(nce.Channel!);
                job.LinkedId ??= linked;
                return;
            }

            if (e is HangupEvent he)
            {
                var job = FindByChannel(he.Channel) ?? FindByUniqueId(he.UniqueId);
                if (job is null) return;
                if (job.State is not (CallJobState.Bridged or CallJobState.WaitingAnswer or CallJobState.DialingLeg2 or CallJobState.DialingLeg1))
                    return;

                // Once SIP media channels are known, ignore Local stub hangups (Bridge moves SIP off Local).
                if (!string.IsNullOrWhiteSpace(job.MediaChannel1)
                    && !string.IsNullOrWhiteSpace(job.MediaChannel2)
                    && !IsSipLikeChannel(he.Channel))
                {
                    return;
                }

                var hangupReason = string.IsNullOrWhiteSpace(he.CauseTxt)
                    ? $"Hangup cause={he.Cause}"
                    : $"Hangup: {he.CauseTxt} (cause={he.Cause})";

                // Early hangup before SIP peers resolved = failure.
                if ((job.State is CallJobState.DialingLeg1 or CallJobState.DialingLeg2 or CallJobState.WaitingAnswer)
                    && string.IsNullOrWhiteSpace(job.MediaChannel1)
                    && job.State != CallJobState.Bridged)
                {
                    SetState(job, CallJobState.Failed, hangupReason);
                }
                else
                {
                    SetState(job, CallJobState.Completed, null, hangupReason);
                }

                _ = PublishJobAsync(job);
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
            string.Equals(j.ActionId, actionId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(j.ActionIdLeg2, actionId, StringComparison.OrdinalIgnoreCase));
    }

    private CallJob? FindByChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return null;
        return _store.GetAll().FirstOrDefault(j =>
            j.State is not CallJobState.Completed and not CallJobState.Failed and not CallJobState.Cancelled
            && ((!string.IsNullOrWhiteSpace(j.Channel)
                 && string.Equals(j.Channel, channel, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(j.Channel2)
                    && string.Equals(j.Channel2, channel, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(j.MediaChannel1)
                    && string.Equals(j.MediaChannel1, channel, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(j.MediaChannel2)
                    && string.Equals(j.MediaChannel2, channel, StringComparison.OrdinalIgnoreCase))));
    }

    private CallJob? FindByUniqueId(string? uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId)) return null;
        return _store.GetAll().FirstOrDefault(j =>
            string.Equals(j.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(j.UniqueId2, uniqueId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(j.LinkedId, uniqueId, StringComparison.OrdinalIgnoreCase));
    }

    private CallJob? FindByLinkedId(string? linkedId)
    {
        if (string.IsNullOrWhiteSpace(linkedId)) return null;
        return _store.GetAll().FirstOrDefault(j =>
            j.State is not CallJobState.Completed and not CallJobState.Failed and not CallJobState.Cancelled
            && (string.Equals(j.LinkedId, linkedId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(j.UniqueId, linkedId, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class OriginatePlan
    {
        public required string Channel { get; init; }
        public string? Application { get; init; }
        public string? Data { get; init; }
        public required string Diagnostic { get; init; }
    }

    private static (string leg1, string leg2) ResolveLegNumbers(CallJob job) =>
        job.Type switch
        {
            CallJobType.ExtToExt => (job.From!.Trim(), job.To!.Trim()),
            CallJobType.MobileToExt => (job.Mobile1!.Trim(), job.To!.Trim()),
            CallJobType.MobileToMobile => (job.Mobile1!.Trim(), job.Mobile2!.Trim()),
            _ => throw new InvalidOperationException($"Unsupported dial job type {job.Type}")
        };

    private static OriginatePlan BuildDirectTechPlan(CallJob job, string tech, int timeoutSec, string mohClass)
    {
        var dialOpts = $",m({mohClass})";
        var (channel, dialData) = job.Type switch
        {
            CallJobType.ExtToExt => (
                FormatEndpoint(tech, job.From!),
                $"{FormatEndpoint(tech, job.To!)},{timeoutSec}{dialOpts}"),
            CallJobType.MobileToExt => (
                FormatTrunkDial(tech, job.Trunk!, job.Mobile1!),
                $"{FormatEndpoint(tech, job.To!)},{timeoutSec}{dialOpts}"),
            CallJobType.MobileToMobile => (
                FormatTrunkDial(tech, job.Trunk!, job.Mobile1!),
                $"{FormatTrunkDial(tech, job.Trunk!, job.Mobile2!)},{timeoutSec}{dialOpts}"),
            _ => throw new InvalidOperationException($"Unsupported dial job type {job.Type}")
        };

        return new OriginatePlan
        {
            Channel = channel,
            Application = "Dial",
            Data = dialData,
            Diagnostic = $"Application=Dial;Data={dialData}"
        };
    }

    private static bool IsLocalContext(AsteriskOptions opt) =>
        !string.Equals(opt.OriginateVia?.Trim(), "DirectTech", StringComparison.OrdinalIgnoreCase);

    private static string FormatLocalChannel(string numberOrExt, string context) =>
        $"Local/{numberOrExt.Trim()}@{context}/n";

    private static string FormatEndpoint(string tech, string endpoint) =>
        $"{NormalizeTech(tech)}/{endpoint.Trim()}";

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
        var dial = string.IsNullOrWhiteSpace(job.OriginateDialData) ? "—" : job.OriginateDialData;
        return
            $"OriginateResponse: {ore.Response} reason={ore.Reason} ({reasonText}). " +
            $"Channel={channel ?? "—"} Data={dial}. " +
            "Hint: LocalContext bridges SIP/PJSIP peers (not Local/ConfBridge); check FreePBX outbound for both numbers.";
    }

    private static string MapOriginateReason(int reason) => reason switch
    {
        0 => "no such endpoint/number or invalid channel / context / trunk",
        1 => "hangup / no answer (AST_CONTROL_HANGUP)",
        2 => "local ring",
        3 => "remote ringing / often answer timeout",
        4 => "answered",
        5 => "busy",
        8 => "congestion / unavailable",
        _ => "see Asterisk control-frame reason"
    };

    private static void ValidateDialJob(CallJob job, AsteriskOptions opt)
    {
        var requireTrunk = !IsLocalContext(opt);
        switch (job.Type)
        {
            case CallJobType.ExtToExt:
                if (string.IsNullOrWhiteSpace(job.From) || string.IsNullOrWhiteSpace(job.To))
                    throw new ArgumentException("ExtToExt requires From and To extensions.");
                break;
            case CallJobType.MobileToExt:
                if (string.IsNullOrWhiteSpace(job.Mobile1) || string.IsNullOrWhiteSpace(job.To))
                    throw new ArgumentException("MobileToExt requires Mobile1 and To extension.");
                if (requireTrunk && string.IsNullOrWhiteSpace(job.Trunk))
                    throw new ArgumentException("MobileToExt DirectTech requires Trunk (or DefaultTrunk).");
                break;
            case CallJobType.MobileToMobile:
                if (string.IsNullOrWhiteSpace(job.Mobile1) || string.IsNullOrWhiteSpace(job.Mobile2))
                    throw new ArgumentException("MobileToMobile requires Mobile1 and Mobile2.");
                if (requireTrunk && string.IsNullOrWhiteSpace(job.Trunk))
                    throw new ArgumentException("MobileToMobile DirectTech requires Trunk (or DefaultTrunk).");
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
        else if (!string.IsNullOrWhiteSpace(successReason))
        {
            job.ResultReason = successReason;
            if (state is not (CallJobState.Failed or CallJobState.Cancelled))
                job.ErrorMessage = null;
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

            if (error is null && string.IsNullOrWhiteSpace(successReason) && string.IsNullOrWhiteSpace(job.ResultReason))
            {
                job.ResultReason = state switch
                {
                    CallJobState.Cancelled => "Cancelled",
                    CallJobState.Completed => "Completed successfully",
                    _ => job.ResultReason
                };
            }

            if (job.RecordingStarted)
                _ = FinalizeRecordingAsync(job);
        }

        _store.Update(job);
    }

    private async Task TryStartRecordingAsync(CallJob job, CancellationToken ct)
    {
        if (job.RecordingStarted)
            return;

        var opt = _settings.GetEffective();
        if (!opt.RecordingEnabled)
            return;

        var channel = job.MediaChannel1
            ?? job.MediaChannel2
            ?? job.Channel
            ?? job.Channel2;
        if (string.IsNullOrWhiteSpace(channel))
            return;

        var format = string.IsNullOrWhiteSpace(opt.RecordingFormat) ? "wav" : opt.RecordingFormat.Trim().Trim('.');
        var amiFile = _recording.BuildAsteriskFilePath(job.Id, opt);
        var baseName = $"ntk-{job.Id}.{format}";

        try
        {
            var resp = await _ami.SendActionAsync(
                new MixMonitorAction
                {
                    Channel = channel,
                    File = amiFile,
                    Options = "b"
                },
                ct,
                timeoutMs: 8_000).ConfigureAwait(false);

            if (!resp.IsSuccess())
            {
                _logger.LogWarning(
                    "CallJob {JobId} MixMonitor failed on {Channel}: {Msg}",
                    job.Id, channel, resp.Message);
                return;
            }

            job.RecordingChannel = channel;
            job.RecordingFileName = baseName;
            job.RecordingStarted = true;
            _store.Update(job);
            _logger.LogInformation(
                "CallJob {JobId} MixMonitor started on {Channel} file={File}",
                job.Id, channel, amiFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CallJob {JobId} MixMonitor exception on {Channel}", job.Id, channel);
        }
    }

    private async Task TryStopRecordingAsync(CallJob job, CancellationToken ct)
    {
        if (!job.RecordingStarted || string.IsNullOrWhiteSpace(job.RecordingChannel))
            return;

        try
        {
            await _ami.SendActionAsync(
                new StopMixMonitorAction(job.RecordingChannel),
                ct,
                timeoutMs: 5_000).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "StopMixMonitor ignored for job {JobId}", job.Id);
        }
    }

    private async Task FinalizeRecordingAsync(CallJob job)
    {
        try
        {
            await TryStopRecordingAsync(job, CancellationToken.None).ConfigureAwait(false);
            await Task.Delay(800).ConfigureAwait(false);
            await _recording.TryRefreshAvailabilityAsync(job).ConfigureAwait(false);
            _store.Update(job);
            await PublishJobAsync(job).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Finalize recording failed for {JobId}", job.Id);
        }
    }

    private Task PublishJobAsync(CallJob job) =>
        _hub.Clients.Group("jobs").SendAsync("jobUpdated", _store.ToDto(job));

    public void Dispose()
    {
        _workerCts?.Cancel();
        _workerCts?.Dispose();
    }
}
