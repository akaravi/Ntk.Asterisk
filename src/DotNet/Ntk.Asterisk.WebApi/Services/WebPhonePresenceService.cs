using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Hubs;

namespace Ntk.Asterisk.WebApi.Services;

public interface IWebPhonePresenceService
{
    Task<WebPhonePresenceDto?> QueryExtensionAsync(string extension, string? context, CancellationToken cancellationToken);
    Task<WebPhoneMwiDto?> QueryMailboxAsync(string mailbox, CancellationToken cancellationToken);
}

/// <summary>
/// BLF/MWI bridge: AMI ExtensionStatus / DeviceStateChange / MessageWaiting → SignalR webphone group.
/// Skills: asterisk-voip-stack + asterisk-ami (ExtensionState / MailboxCount actions).
/// </summary>
public sealed class WebPhonePresenceService : IWebPhonePresenceService, IHostedService, IDisposable
{
    public const string HubGroup = "webphone";

    private readonly IAmiSession _ami;
    private readonly IHubContext<AsteriskHub> _hub;
    private readonly IOptionsMonitor<WebPhoneOptions> _options;
    private readonly ILogger<WebPhonePresenceService> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, WebPhonePresenceDto> _presence = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WebPhoneMwiDto> _mwi = new(StringComparer.OrdinalIgnoreCase);

    public WebPhonePresenceService(
        IAmiSession ami,
        IHubContext<AsteriskHub> hub,
        IOptionsMonitor<WebPhoneOptions> options,
        ILogger<WebPhonePresenceService> logger)
    {
        _ami = ami;
        _hub = hub;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent += OnAmiEvent;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent -= OnAmiEvent;
        return Task.CompletedTask;
    }

    public void Dispose() => _ami.AmiEvent -= OnAmiEvent;

    public async Task<WebPhonePresenceDto?> QueryExtensionAsync(
        string extension,
        string? context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        var ctx = string.IsNullOrWhiteSpace(context)
            ? _options.CurrentValue.DefaultHintContext
            : context.Trim();

        var response = await _ami.SendActionAsync(
            new ExtensionStateAction { Exten = extension.Trim(), Context = ctx },
            cancellationToken).ConfigureAwait(false);

        var status = response.Attributes?.GetValueOrDefault("Status")
            ?? response.Attributes?.GetValueOrDefault("status");
        var statusText = response.Attributes?.GetValueOrDefault("StatusText")
            ?? response.Attributes?.GetValueOrDefault("statustext")
            ?? MapStatusCode(status);

        int? code = null;
        if (int.TryParse(status, out var parsed))
            code = parsed;

        var dto = new WebPhonePresenceDto
        {
            Extension = extension.Trim(),
            Context = ctx,
            State = statusText,
            StatusCode = code,
            AtUtc = DateTimeOffset.UtcNow
        };

        lock (_gate)
            _presence[dto.Extension] = dto;

        if (_options.CurrentValue.EnablePresence)
            await _hub.Clients.Group(HubGroup).SendAsync("webphonePresence", dto, cancellationToken)
                .ConfigureAwait(false);

        return dto;
    }

    public async Task<WebPhoneMwiDto?> QueryMailboxAsync(string mailbox, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mailbox))
            return null;

        var mb = mailbox.Contains('@', StringComparison.Ordinal)
            ? mailbox.Trim()
            : $"{mailbox.Trim()}@{_options.CurrentValue.DefaultMailboxContext}";

        var response = await _ami.SendActionAsync(
            new MailboxCountAction(mb),
            cancellationToken).ConfigureAwait(false);

        var waiting = response.Attributes?.GetValueOrDefault("Waiting")
            ?? response.Attributes?.GetValueOrDefault("waiting");
        var newMsg = ParseInt(response.Attributes?.GetValueOrDefault("NewMessages")
            ?? response.Attributes?.GetValueOrDefault("newmessages"));
        var oldMsg = ParseInt(response.Attributes?.GetValueOrDefault("OldMessages")
            ?? response.Attributes?.GetValueOrDefault("oldmessages"));

        var dto = new WebPhoneMwiDto
        {
            Mailbox = mb,
            NewMessages = newMsg,
            OldMessages = oldMsg,
            Waiting = string.Equals(waiting, "1", StringComparison.Ordinal)
                || string.Equals(waiting, "yes", StringComparison.OrdinalIgnoreCase)
                || newMsg > 0,
            AtUtc = DateTimeOffset.UtcNow
        };

        lock (_gate)
            _mwi[dto.Mailbox] = dto;

        if (_options.CurrentValue.EnableMwi)
            await _hub.Clients.Group(HubGroup).SendAsync("webphoneMwi", dto, cancellationToken)
                .ConfigureAwait(false);

        return dto;
    }

    private void OnAmiEvent(object? sender, ManagerEvent e)
    {
        try
        {
            switch (e)
            {
                case ExtensionStatusEvent ext:
                    _ = PublishPresenceAsync(new WebPhonePresenceDto
                    {
                        Extension = ext.Exten ?? ext.Attributes?.GetValueOrDefault("Exten") ?? string.Empty,
                        Context = ext.Context ?? ext.Attributes?.GetValueOrDefault("Context"),
                        State = MapStatusCode(ext.Status.ToString()),
                        StatusCode = ext.Status,
                        AtUtc = DateTimeOffset.UtcNow
                    });
                    break;

                case DeviceStateChangeEvent device:
                    var deviceName = device.Attributes?.GetValueOrDefault("Device")
                        ?? device.Attributes?.GetValueOrDefault("device")
                        ?? string.Empty;
                    var state = device.Status
                        ?? device.Attributes?.GetValueOrDefault("State")
                        ?? device.Attributes?.GetValueOrDefault("DeviceState")
                        ?? "Unknown";
                    _ = PublishPresenceAsync(new WebPhonePresenceDto
                    {
                        Extension = ExtractExtension(deviceName),
                        Device = deviceName,
                        State = state,
                        AtUtc = DateTimeOffset.UtcNow
                    });
                    break;

                case MessageWaitingEvent mwi:
                    _ = PublishMwiAsync(new WebPhoneMwiDto
                    {
                        Mailbox = mwi.Mailbox ?? mwi.Attributes?.GetValueOrDefault("Mailbox") ?? string.Empty,
                        NewMessages = mwi.New > 0 ? mwi.New : mwi.Waiting,
                        OldMessages = mwi.Old,
                        Waiting = mwi.Waiting > 0 || mwi.New > 0,
                        AtUtc = DateTimeOffset.UtcNow
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WebPhone presence AMI event handling failed");
        }
    }

    private async Task PublishPresenceAsync(WebPhonePresenceDto dto)
    {
        if (!_options.CurrentValue.EnablePresence || string.IsNullOrWhiteSpace(dto.Extension))
            return;
        lock (_gate)
            _presence[dto.Extension] = dto;
        await _hub.Clients.Group(HubGroup).SendAsync("webphonePresence", dto).ConfigureAwait(false);
    }

    private async Task PublishMwiAsync(WebPhoneMwiDto dto)
    {
        if (!_options.CurrentValue.EnableMwi || string.IsNullOrWhiteSpace(dto.Mailbox))
            return;
        lock (_gate)
            _mwi[dto.Mailbox] = dto;
        await _hub.Clients.Group(HubGroup).SendAsync("webphoneMwi", dto).ConfigureAwait(false);
    }

    private static string ExtractExtension(string device)
    {
        if (string.IsNullOrWhiteSpace(device))
            return string.Empty;
        var slash = device.LastIndexOf('/');
        return slash >= 0 && slash < device.Length - 1 ? device[(slash + 1)..] : device;
    }

    private static string MapStatusCode(string? status) => status switch
    {
        "0" => "Idle",
        "1" => "InUse",
        "2" => "Busy",
        "4" => "Unavailable",
        "8" => "Ringing",
        "9" => "InUse&Ringing",
        "16" => "OnHold",
        _ => string.IsNullOrWhiteSpace(status) ? "Unknown" : status
    };

    private static int ParseInt(string? s) =>
        int.TryParse(s, out var n) ? n : 0;
}
