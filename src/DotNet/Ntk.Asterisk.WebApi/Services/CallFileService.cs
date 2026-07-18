using System.Text;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface ICallFileService
{
    Task<CallFileDto> CreateAsync(CallFileAddRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes Asterisk Call Files: stage then atomic Move into outgoing (never write/copy directly into outgoing).
/// </summary>
public sealed class CallFileService : ICallFileService
{
    private readonly IAsteriskSettingsService _settings;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CallFileService> _logger;

    public CallFileService(
        IAsteriskSettingsService settings,
        IWebHostEnvironment env,
        ILogger<CallFileService> logger)
    {
        _settings = settings;
        _env = env;
        _logger = logger;
    }

    public async Task<CallFileDto> CreateAsync(CallFileAddRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var opt = _settings.GetEffective();
        var channel = (request.Channel ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Channel is required (e.g. PJSIP/1001 or Local/1001@from-internal).");

        var hasApp = !string.IsNullOrWhiteSpace(request.Application);
        var hasCtx = !string.IsNullOrWhiteSpace(request.Context)
            && !string.IsNullOrWhiteSpace(request.Extension);
        if (hasApp == hasCtx)
            throw new ArgumentException("Specify either Application(+Data) or Context+Extension(+Priority), not both or neither.");

        var outgoing = NullIfWhiteSpace(opt.CallFileOutgoingDirectory);
        if (outgoing is null)
            throw new InvalidOperationException(
                "CallFileOutgoingDirectory is not configured. Set a path (UNC mount of /var/spool/asterisk/outgoing) in Settings.");

        var staging = NullIfWhiteSpace(opt.CallFileStagingDirectory)
            ?? Path.Combine(_env.ContentRootPath, "App_Data", "callfile-staging");

        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(outgoing);

        var body = BuildCallFileBody(request, channel, opt);
        // Asterisk Call Files must not be UTF-8/Unicode — ASCII only.
        var bytes = Encoding.ASCII.GetBytes(body);

        var fileName = $"ntk-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.call";
        var stagingPath = Path.Combine(staging, fileName);
        var outgoingPath = Path.Combine(outgoing, fileName);

        await File.WriteAllBytesAsync(stagingPath, bytes, cancellationToken).ConfigureAwait(false);

        try
        {
            // Atomic move on same volume — never File.Copy into outgoing (pbx_spool race).
            File.Move(stagingPath, outgoingPath);
        }
        catch (Exception ex)
        {
            try { File.Delete(stagingPath); } catch { /* ignore */ }
            _logger.LogError(ex, "CallFile Move failed staging={Staging} outgoing={Outgoing}", stagingPath, outgoingPath);
            throw new InvalidOperationException(
                "Failed to move Call File into outgoing. Staging and outgoing must be on the same volume/share. " + ex.Message,
                ex);
        }

        _logger.LogInformation("CallFile created {FileName} Channel={Channel}", fileName, channel);

        return new CallFileDto
        {
            FileName = fileName,
            Channel = channel,
            OutgoingPath = outgoingPath,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Mode = hasApp ? "Application" : "Context"
        };
    }

    private static string BuildCallFileBody(CallFileAddRequest request, string channel, AsteriskOptions opt)
    {
        var sb = new StringBuilder();
        sb.Append("Channel: ").Append(SanitizeAsciiLine(channel)).Append('\n');

        var callerId = NullIfWhiteSpace(request.CallerId) ?? NullIfWhiteSpace(opt.DefaultCallerId);
        if (callerId is not null)
            sb.Append("Callerid: ").Append(SanitizeAsciiLine(callerId)).Append('\n');

        var wait = request.WaitTimeSec is > 0
            ? request.WaitTimeSec.Value
            : Math.Max(1, opt.DefaultTimeoutMs / 1000);
        sb.Append("WaitTime: ").Append(wait).Append('\n');

        if (request.MaxRetries is >= 0)
            sb.Append("MaxRetries: ").Append(request.MaxRetries.Value).Append('\n');
        if (request.RetryTimeSec is > 0)
            sb.Append("RetryTime: ").Append(request.RetryTimeSec.Value).Append('\n');

        var archive = string.Equals(request.Archive, "yes", StringComparison.OrdinalIgnoreCase) ? "yes" : "no";
        sb.Append("Archive: ").Append(archive).Append('\n');

        if (!string.IsNullOrWhiteSpace(request.Application))
        {
            sb.Append("Application: ").Append(SanitizeAsciiLine(request.Application!)).Append('\n');
            if (!string.IsNullOrWhiteSpace(request.Data))
                sb.Append("Data: ").Append(SanitizeAsciiLine(request.Data!)).Append('\n');
        }
        else
        {
            sb.Append("Context: ").Append(SanitizeAsciiLine(request.Context!)).Append('\n');
            sb.Append("Extension: ").Append(SanitizeAsciiLine(request.Extension!)).Append('\n');
            var prio = string.IsNullOrWhiteSpace(request.Priority) ? "1" : request.Priority.Trim();
            sb.Append("Priority: ").Append(SanitizeAsciiLine(prio)).Append('\n');
        }

        if (request.SetVars is { Count: > 0 })
        {
            foreach (var kv in request.SetVars)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                sb.Append("Setvar: ")
                    .Append(SanitizeAsciiLine(kv.Key))
                    .Append('=')
                    .Append(SanitizeAsciiLine(kv.Value ?? string.Empty))
                    .Append('\n');
            }
        }

        return sb.ToString();
    }

    private static string SanitizeAsciiLine(string value)
    {
        var trimmed = value.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        var chars = trimmed.Select(c => c <= 127 ? c : '?').ToArray();
        return new string(chars);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
