using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Jobs;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Response;

namespace Ntk.Asterisk.WebApi.Services;

public interface ICallRecordingService
{
    string BuildAsteriskFilePath(string jobId, AsteriskOptions opt);
    string CacheFilePath(string jobId, string extension);
    Task<bool> TryRefreshAvailabilityAsync(CallJob job, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string DownloadName)?> OpenForDownloadAsync(
        CallJob job,
        CancellationToken ct = default);
}

public sealed class CallRecordingService : ICallRecordingService
{
    private readonly IAsteriskSettingsService _settings;
    private readonly IAmiSession _ami;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CallRecordingService> _logger;

    public CallRecordingService(
        IAsteriskSettingsService settings,
        IAmiSession ami,
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        ILogger<CallRecordingService> logger)
    {
        _settings = settings;
        _ami = ami;
        _env = env;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string BuildAsteriskFilePath(string jobId, AsteriskOptions opt)
    {
        var format = string.IsNullOrWhiteSpace(opt.RecordingFormat) ? "wav" : opt.RecordingFormat.Trim().Trim('.');
        var withExt = $"ntk-{jobId}.{format}";
        var dir = string.IsNullOrWhiteSpace(opt.RecordingAsteriskDirectory)
            ? "/var/spool/asterisk/monitor"
            : opt.RecordingAsteriskDirectory.Trim();
        return Path.Combine(dir.TrimEnd('/', '\\'), withExt).Replace('\\', '/');
    }

    public string CacheFilePath(string jobId, string extension)
    {
        var dir = Path.Combine(_env.ContentRootPath, "App_Data", "recordings");
        Directory.CreateDirectory(dir);
        var ext = string.IsNullOrWhiteSpace(extension) ? "wav" : extension.Trim().Trim('.');
        return Path.Combine(dir, $"ntk-{jobId}.{ext}");
    }

    public async Task<bool> TryRefreshAvailabilityAsync(CallJob job, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(job.RecordingFileName) && !job.RecordingStarted)
            return false;

        var opt = _settings.GetEffective();
        var format = string.IsNullOrWhiteSpace(opt.RecordingFormat) ? "wav" : opt.RecordingFormat.Trim().Trim('.');
        var fileName = job.RecordingFileName ?? $"ntk-{job.Id}.{format}";
        if (!fileName.Contains('.', StringComparison.Ordinal))
            fileName = $"{fileName}.{format}";

        var cachePath = CacheFilePath(job.Id, format);
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
        {
            job.RecordingFileName = Path.GetFileName(cachePath);
            job.RecordingAvailable = true;
            return true;
        }

        foreach (var candidate in EnumerateLocalCandidates(opt, fileName, job.Id, format))
        {
            if (!File.Exists(candidate) || new FileInfo(candidate).Length <= 0)
                continue;
            try
            {
                File.Copy(candidate, cachePath, overwrite: true);
                job.RecordingFileName = Path.GetFileName(cachePath);
                job.RecordingAvailable = true;
                _logger.LogInformation("Recording cached from {Src} → {Dst}", candidate, cachePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed copying recording from {Src}", candidate);
            }
        }

        var httpBase = opt.RecordingHttpBaseUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(httpBase))
        {
            try
            {
                var url = $"{httpBase.TrimEnd('/')}/{Uri.EscapeDataString(fileName)}";
                var client = _httpClientFactory.CreateClient("recording-fetch");
                using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    await using var fs = File.Create(cachePath);
                    await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
                    if (fs.Length > 0)
                    {
                        job.RecordingFileName = Path.GetFileName(cachePath);
                        job.RecordingAvailable = true;
                        _logger.LogInformation("Recording pulled via HTTP {Url}", url);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "HTTP recording fetch failed for job {JobId}", job.Id);
            }
        }

        // Remote PBX without mount: pull via AMI CLI base64 (needs manager write=command / system).
        if (await TryPullViaAmiBase64Async(job, opt, fileName, cachePath, ct).ConfigureAwait(false))
            return true;

        job.RecordingAvailable = false;
        return false;
    }

    public async Task<(Stream Stream, string ContentType, string DownloadName)?> OpenForDownloadAsync(
        CallJob job,
        CancellationToken ct = default)
    {
        await TryRefreshAvailabilityAsync(job, ct).ConfigureAwait(false);
        if (!job.RecordingAvailable)
            return null;

        var opt = _settings.GetEffective();
        var format = string.IsNullOrWhiteSpace(opt.RecordingFormat) ? "wav" : opt.RecordingFormat.Trim().Trim('.');
        var cachePath = CacheFilePath(job.Id, format);
        if (!File.Exists(cachePath))
            return null;

        var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var contentType = format.Equals("mp3", StringComparison.OrdinalIgnoreCase)
            ? "audio/mpeg"
            : "audio/wav";
        var name = job.RecordingFileName ?? Path.GetFileName(cachePath);
        return (stream, contentType, name);
    }

    private async Task<bool> TryPullViaAmiBase64Async(
        CallJob job,
        AsteriskOptions opt,
        string fileName,
        string cachePath,
        CancellationToken ct)
    {
        try
        {
            var remote = BuildAsteriskFilePath(job.Id, opt);
            var candidates = new[]
            {
                remote,
                $"/var/spool/asterisk/monitor/{fileName}",
                fileName
            }.Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var path in candidates)
            {
                // Prefer coreutils base64; fall back to openssl.
                foreach (var cli in new[]
                         {
                             $"base64 -w0 {path}",
                             $"base64 {path}",
                             $"openssl base64 -A -in {path}"
                         })
                {
                    var resp = await _ami.SendActionAsync(new CommandAction(cli), ct, timeoutMs: 90_000)
                        .ConfigureAwait(false);
                    if (!TryExtractBase64(resp, out var b64) || string.IsNullOrWhiteSpace(b64))
                        continue;

                    try
                    {
                        var bytes = Convert.FromBase64String(b64.Trim());
                        if (bytes.Length < 44)
                            continue;

                        await File.WriteAllBytesAsync(cachePath, bytes, ct).ConfigureAwait(false);
                        job.RecordingFileName = Path.GetFileName(cachePath);
                        job.RecordingAvailable = true;
                        _logger.LogInformation(
                            "Recording pulled via AMI CLI ({Bytes} bytes) from {Path}",
                            bytes.Length, path);
                        return true;
                    }
                    catch (FormatException)
                    {
                        // try next command/path
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AMI base64 recording pull failed for job {JobId}", job.Id);
        }

        return false;
    }

    private static bool TryExtractBase64(ManagerResponse resp, out string? b64)
    {
        b64 = null;
        if (resp is CommandResponse cr && cr.Result is { Count: > 0 })
        {
            var lines = cr.Result
                .Where(l => !string.IsNullOrWhiteSpace(l)
                            && !l.Contains("END COMMAND", StringComparison.OrdinalIgnoreCase)
                            && !l.StartsWith("No such", StringComparison.OrdinalIgnoreCase)
                            && !l.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase)
                            && !l.Contains("No such file", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Trim());
            b64 = string.Concat(lines);
            return !string.IsNullOrWhiteSpace(b64);
        }

        // Some AMI stacks put output in Message / Output attributes.
        var msg = resp.GetAttribute("Output")
                  ?? resp.GetAttribute("Message")
                  ?? resp.Message;
        if (!string.IsNullOrWhiteSpace(msg)
            && !msg.Contains("Error", StringComparison.OrdinalIgnoreCase)
            && msg.Length > 80)
        {
            b64 = msg.Trim();
            return true;
        }

        return false;
    }

    private static IEnumerable<string> EnumerateLocalCandidates(
        AsteriskOptions opt,
        string fileName,
        string jobId,
        string format)
    {
        var localDir = opt.RecordingLocalDirectory?.Trim();
        if (string.IsNullOrWhiteSpace(localDir))
            yield break;

        yield return Path.Combine(localDir, fileName);
        yield return Path.Combine(localDir, $"ntk-{jobId}.{format}");
        yield return Path.Combine(localDir, $"ntk-{jobId}");
        var day = DateTime.UtcNow;
        yield return Path.Combine(localDir, day.ToString("yyyy"), day.ToString("MM"), day.ToString("dd"), fileName);
        yield return Path.Combine(localDir, day.ToString("yyyy"), day.ToString("MM"), day.ToString("dd"), $"ntk-{jobId}.{format}");
    }
}
