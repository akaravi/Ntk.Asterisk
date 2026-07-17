using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Jobs;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
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
    /// <summary>
    /// Queue Originate System encode on the PBX (safe path — not MixMonitor Command).
    /// Idempotent per job until pull succeeds or process recycles.
    /// </summary>
    Task TriggerRemoteEncodeAsync(CallJob job, CancellationToken ct = default);
}

public sealed class CallRecordingService : ICallRecordingService
{
    /// <summary>
    /// AstDB on this Issabel build accepts ≤256-byte values. Chunks must stay ≤240.
    /// Never run this encoder from MixMonitor Command= (deadlocks Asterisk when calling -rx).
    /// </summary>
    private const int AstDbChunkWidth = 240;

    /// <summary>Max seconds to wait for remote AstDB encode + pull (Originate System path).</summary>
    private const int AstDbPullMaxSeconds = 300;

    private const string HttpPublishDirectory = "/var/www/html/ntk-recordings";

    /// <summary>Reject empty MixMonitor stubs (WAV header-only = 44 bytes).</summary>
    private const long MinRecordingBytes = 2048;

    private readonly IAsteriskSettingsService _settings;
    private readonly IAmiSession _ami;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CallRecordingService> _logger;
    private readonly HashSet<string> _encodeTriggered = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _encodeGate = new();

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

    public static string AstDbFamilyForJob(string jobId)
    {
        var token = jobId.Replace("-", "", StringComparison.Ordinal);
        return "NTKR" + token;
    }

    /// <summary>
    /// Shell for Originate System encode — encodes wav into AstDB family NTKR{jobId}.
    /// MUST NOT be used as MixMonitor Command= (asterisk -rx from that context deadlocks).
    /// </summary>
    public static string BuildAstDbEncodeShellCommand(string remoteFilePath, string jobId)
    {
        var fam = AstDbFamilyForJob(jobId);
        // Invoke via `sh script` — never rely on shebang (CRLF → exit 126 on Issabel).
        return string.Concat(
            "/bin/sh /var/lib/asterisk/agi-bin/ntk-enc.sh ", fam, " ", remoteFilePath);
    }

    /// <summary>Unix-LF encoder script body installed once on the PBX.</summary>
    public static string BuildEncoderScriptUnix()
    {
        return string.Join('\n',
            "#!/bin/sh",
            "Fam=$1; F=$2; T=/tmp/ntkenc.$Fam.chunks",
            "asterisk -rx \"database deltree $Fam\" >/dev/null 2>&1",
            "asterisk -rx \"database put $Fam enc 1\"",
            "if [ ! -f \"$F\" ]; then asterisk -rx \"database put $Fam meta MISSING\"; asterisk -rx \"database put $Fam enc 0\"; exit 0; fi",
            "SZ=$(stat -c%s \"$F\")",
            "asterisk -rx \"database put $Fam meta $SZ\"",
            "base64 -w0 \"$F\" | fold -w " + AstDbChunkWidth + " > \"$T\"",
            "i=0",
            "while read -r c; do",
            "asterisk -rx \"database put $Fam $i $c\"",
            "i=$((i+1))",
            "done < \"$T\"",
            "asterisk -rx \"database put $Fam n $i\"",
            "asterisk -rx \"database put $Fam enc 0\"",
            "rm -f \"$T\"",
            "");
    }

    public static string BuildEncoderInstallShellCommand()
    {
        var b64 = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(BuildEncoderScriptUnix()));
        return string.Concat(
            "sh -c 'echo ", b64,
            " | base64 -d > /var/lib/asterisk/agi-bin/ntk-enc.sh && chmod 755 /var/lib/asterisk/agi-bin/ntk-enc.sh'");
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
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length >= MinRecordingBytes)
        {
            job.RecordingFileName = Path.GetFileName(cachePath);
            job.RecordingAvailable = true;
            return true;
        }

        foreach (var candidate in EnumerateLocalCandidates(opt, fileName, job.Id, format))
        {
            if (!File.Exists(candidate) || new FileInfo(candidate).Length < MinRecordingBytes)
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
        foreach (var url in EnumerateHttpRecordingUrls(opt, fileName, httpBase))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("recording-fetch");
                using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    continue;

                await using var fs = File.Create(cachePath);
                await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
                if (fs.Length >= MinRecordingBytes)
                {
                    job.RecordingFileName = Path.GetFileName(cachePath);
                    job.RecordingAvailable = true;
                    _logger.LogInformation("Recording pulled via HTTP {Url} ({Bytes} bytes)", url, fs.Length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "HTTP recording fetch failed for {Url}", url);
            }
        }

        // Remote PBX: AstDB chunks written by Originate System encode (never MixMonitor Command).
        if (await TryPullViaAmiAstDbAsync(job, opt, cachePath, ct).ConfigureAwait(false))
            return true;

        job.RecordingAvailable = false;
        return false;
    }

    public Task TriggerRemoteEncodeAsync(CallJob job, CancellationToken ct = default)
    {
        var opt = _settings.GetEffective();
        return TriggerRemoteEncodeCoreAsync(job, opt, force: false, ct);
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

    private async Task<bool> TryPullViaAmiAstDbAsync(
        CallJob job,
        AsteriskOptions opt,
        string cachePath,
        CancellationToken ct)
    {
        var family = AstDbFamilyForJob(job.Id);
        try
        {
            // Encode must run via Originate System — MixMonitor Command + asterisk -rx deadlocks.
            // Also publishes wav under /var/www/html/ntk-recordings for HTTPS pull.
            await TriggerRemoteEncodeCoreAsync(job, opt, force: false, ct).ConfigureAwait(false);

            Dictionary<string, string>? map = null;
            for (var wait = 0; wait < AstDbPullMaxSeconds; wait++)
            {
                ct.ThrowIfCancellationRequested();

                // Prefer HTTP (usually ready a few seconds after publish).
                if (wait % 2 == 0)
                {
                    var format = string.IsNullOrWhiteSpace(opt.RecordingFormat)
                        ? "wav"
                        : opt.RecordingFormat.Trim().Trim('.');
                    var fileName = job.RecordingFileName ?? $"ntk-{job.Id}.{format}";
                    if (!fileName.Contains('.', StringComparison.Ordinal))
                        fileName = $"{fileName}.{format}";

                    foreach (var url in EnumerateHttpRecordingUrls(opt, fileName, opt.RecordingHttpBaseUrl))
                    {
                        try
                        {
                            var client = _httpClientFactory.CreateClient("recording-fetch");
                            using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
                            if (!resp.IsSuccessStatusCode)
                                continue;
                            await using var fs = File.Create(cachePath);
                            await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
                            if (fs.Length >= MinRecordingBytes)
                            {
                                job.RecordingFileName = Path.GetFileName(cachePath);
                                job.RecordingAvailable = true;
                                lock (_encodeGate)
                                    _encodeTriggered.Remove(job.Id);
                                _logger.LogInformation(
                                    "Recording pulled via HTTP during AstDB wait {Url} ({Bytes} bytes)",
                                    url, fs.Length);
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "HTTP retry failed for {Url}", url);
                        }
                    }
                }

                try
                {
                    map = await LoadAstDbFamilyAsync(family, ct).ConfigureAwait(false);
                    if (map.Count < 2)
                    {
                        // Bulk show truncated/empty — fall back to meta/n/enc via DBGet.
                        map = await LoadAstDbMetaViaDbGetAsync(family, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "AstDB family load attempt {Wait} for {JobId}", wait, job.Id);
                    map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                if (map.Count == 0)
                {
                    // Re-queue only if still empty after a long wait (do not deltree mid-encode).
                    if (wait is 60 or 150)
                        await TriggerRemoteEncodeCoreAsync(job, opt, force: true, ct).ConfigureAwait(false);

                    await Task.Delay(1_000, ct).ConfigureAwait(false);
                    continue;
                }

                if (!map.TryGetValue("meta", out var meta)
                    || string.IsNullOrWhiteSpace(meta)
                    || meta.Equals("MISSING", StringComparison.OrdinalIgnoreCase)
                    || !long.TryParse(meta.Trim(), out var expectedSize)
                    || expectedSize <= 0)
                {
                    await Task.Delay(1_000, ct).ConfigureAwait(false);
                    continue;
                }

                if (map.TryGetValue("enc", out var enc) && enc.Trim() == "1")
                {
                    await Task.Delay(1_000, ct).ConfigureAwait(false);
                    continue;
                }

                var expectedChunks = Math.Max(
                    1,
                    (int)Math.Ceiling(Math.Ceiling(expectedSize / 3.0) * 4.0 / AstDbChunkWidth));

                var chunkCount = 0;
                if (map.TryGetValue("n", out var nRaw)
                    && int.TryParse(nRaw.Trim(), out var nVal)
                    && nVal > 0
                    && nVal <= expectedChunks * 1.2 + 4)
                {
                    chunkCount = nVal;
                }
                else
                {
                    chunkCount = map.Keys.Count(k => int.TryParse(k, out _));
                }

                var hasN = map.ContainsKey("n");
                // Do NOT trust n alone — failed oversized puts still increment n in the shell loop.
                string? probe0 = null;
                if (map.TryGetValue("0", out var m0) && !string.IsNullOrWhiteSpace(m0))
                    probe0 = m0;
                else
                    probe0 = await DbGetValueAsync(family, "0", ct).ConfigureAwait(false);

                var ready = hasN
                    && chunkCount >= (int)(expectedChunks * 0.95)
                    && !string.IsNullOrWhiteSpace(probe0);

                if (!ready)
                {
                    // n without chunk-0 ⇒ encode wrote empty/oversized puts — re-encode once.
                    if (hasN
                        && string.IsNullOrWhiteSpace(probe0)
                        && wait is 15 or 90)
                    {
                        _logger.LogWarning(
                            "Recording AstDB n={N} but chunk0 missing for {JobId} — re-encode",
                            chunkCount, job.Id);
                        await TriggerRemoteEncodeCoreAsync(job, opt, force: true, ct).ConfigureAwait(false);
                    }

                    await Task.Delay(1_000, ct).ConfigureAwait(false);
                    continue;
                }

                if (!map.ContainsKey("0") && !string.IsNullOrWhiteSpace(probe0))
                    map["0"] = probe0!;

                // Prefer per-chunk DBGet — bulk `database show` truncates large families.
                var sb = new System.Text.StringBuilder(chunkCount * AstDbChunkWidth);
                var missing = false;
                for (var i = 0; i < chunkCount; i++)
                {
                    string? chunk = null;
                    if (map.TryGetValue(i.ToString(), out var fromMap) && !string.IsNullOrWhiteSpace(fromMap))
                        chunk = fromMap;
                    else
                        chunk = await DbGetValueAsync(family, i.ToString(), ct).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(chunk))
                    {
                        _logger.LogWarning(
                            "Recording AstDB chunk {Index}/{Total} missing for job {JobId} — re-wait",
                            i, chunkCount, job.Id);
                        missing = true;
                        break;
                    }

                    sb.Append(chunk.Trim());
                }

                if (missing)
                {
                    await Task.Delay(1_000, ct).ConfigureAwait(false);
                    continue;
                }

                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(sb.ToString());
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning(ex, "Recording AstDB base64 decode failed for job {JobId}", job.Id);
                    return false;
                }

                if (bytes.Length < 44)
                    return false;

                if (bytes.Length < expectedSize * 0.9)
                {
                    _logger.LogWarning(
                        "Recording AstDB incomplete for job {JobId}: got {Got} expected {Expected} chunks={Chunks}/{ExpectedChunks}",
                        job.Id, bytes.Length, expectedSize, chunkCount, expectedChunks);
                    await Task.Delay(1_000, ct).ConfigureAwait(false);
                    continue;
                }

                await File.WriteAllBytesAsync(cachePath, bytes, ct).ConfigureAwait(false);
                job.RecordingFileName = Path.GetFileName(cachePath);
                job.RecordingAvailable = true;
                lock (_encodeGate)
                    _encodeTriggered.Remove(job.Id);
                _logger.LogInformation(
                    "Recording pulled via AstDB ({Bytes} bytes, expected≈{Expected}, chunks={Chunks}) for job {JobId}",
                    bytes.Length, expectedSize, chunkCount, job.Id);

                try
                {
                    await _ami.SendActionAsync(
                            new DBDelTreeAction { Family = family },
                            ct,
                            8_000)
                        .ConfigureAwait(false);
                }
                catch
                {
                    try
                    {
                        await _ami.SendActionAsync(new CommandAction($"database deltree {family}"), ct, 8_000)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                return true;
            }

            _logger.LogWarning(
                "Recording AstDB pull timed out for job {JobId} (keys={Keys})",
                job.Id, map?.Count ?? 0);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AstDB recording pull failed for job {JobId}", job.Id);
            return false;
        }
    }

    private async Task TriggerRemoteEncodeCoreAsync(
        CallJob job,
        AsteriskOptions opt,
        bool force,
        CancellationToken ct)
    {
        lock (_encodeGate)
        {
            if (!force && !_encodeTriggered.Add(job.Id))
                return;
            if (force)
                _encodeTriggered.Add(job.Id);
        }

        var remote = BuildAsteriskFilePath(job.Id, opt);
        var family = AstDbFamilyForJob(job.Id);
        try
        {
            // Clear stuck MixMonitor Command deadlocks (enc=1 + meta only).
            try
            {
                await _ami.SendActionAsync(new CommandAction($"database deltree {family}"), ct, 8_000)
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            // Fast path: copy wav into Apache docroot so WebApi can HTTPS-pull (self-signed OK).
            var publish = BuildHttpPublishShellCommand(remote);
            var pubResp = await _ami.SendActionAsync(
                new OriginateAction
                {
                    Channel = "Local/s@default/n",
                    Application = "System",
                    Data = publish,
                    Async = true,
                    Timeout = 30_000,
                    CallerId = "NtkRecPub",
                    ActionId = $"recpub-{job.Id}"
                },
                ct,
                timeoutMs: 12_000).ConfigureAwait(false);
            _logger.LogInformation(
                "Recording HTTP publish for {JobId} ok={Ok} msg={Msg}",
                job.Id, pubResp.IsSuccess(), pubResp.Message);

            await _ami.SendActionAsync(
                new OriginateAction
                {
                    Channel = "Local/s@default/n",
                    Application = "System",
                    Data = BuildEncoderInstallShellCommand(),
                    Async = true,
                    Timeout = 30_000,
                    CallerId = "NtkEncInst",
                    ActionId = $"recenc-inst-{job.Id}"
                },
                ct,
                timeoutMs: 12_000).ConfigureAwait(false);

            await Task.Delay(1_200, ct).ConfigureAwait(false);

            var shell = BuildAstDbEncodeShellCommand(remote, job.Id);
            var queued = await _ami.SendActionAsync(
                new OriginateAction
                {
                    Channel = "Local/s@default/n",
                    Application = "System",
                    Data = shell,
                    Async = true,
                    Timeout = 600_000,
                    CallerId = "NtkRecEnc",
                    ActionId = $"recenc-{job.Id}"
                },
                ct,
                timeoutMs: 15_000).ConfigureAwait(false);
            _logger.LogInformation(
                "Recording encode Originate System for {JobId} force={Force} ok={Ok} msg={Msg} cmd={Cmd}",
                job.Id, force, queued.IsSuccess(), queued.Message, shell);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recording encode Originate failed for {JobId}", job.Id);
            lock (_encodeGate)
                _encodeTriggered.Remove(job.Id);
        }
    }

    public static string BuildHttpPublishShellCommand(string remoteFilePath)
    {
        // Copy MixMonitor wav into Issabel Apache docroot for HTTPS download by WebApi.
        return string.Concat(
            "sh -c 'mkdir -p ", HttpPublishDirectory,
            " && cp -f ", remoteFilePath, " ", HttpPublishDirectory,
            "/ && chmod 644 ", HttpPublishDirectory, "/*'");
    }

    private static IEnumerable<string> EnumerateHttpRecordingUrls(
        AsteriskOptions opt,
        string fileName,
        string? configuredBase)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        void Push(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;
            if (seen.Add(url))
                list.Add(url);
        }

        if (!string.IsNullOrWhiteSpace(configuredBase))
            Push($"{configuredBase.TrimEnd('/')}/{fileName}");

        var host = opt.Host?.Trim();
        if (!string.IsNullOrWhiteSpace(host))
        {
            Push($"https://{host}/ntk-recordings/{fileName}");
            Push($"http://{host}/ntk-recordings/{fileName}");
        }

        return list;
    }

    private async Task<Dictionary<string, string>> LoadAstDbMetaViaDbGetAsync(
        string family,
        CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "meta", "n", "enc" })
        {
            var val = await DbGetValueAsync(family, key, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(val))
                map[key] = val;
        }

        return map;
    }

    private async Task<string?> DbGetValueAsync(string family, string key, CancellationToken ct)
    {
        try
        {
            // Prefer CLI `database get` — DBGet ActionID/event races were unreliable on this PBX.
            var resp = await _ami.SendActionAsync(
                new CommandAction($"database get {family} {key}"),
                ct,
                timeoutMs: 12_000).ConfigureAwait(false);

            foreach (var raw in CollectCommandOutputLines(resp))
            {
                var t = raw.Trim();
                if (t.StartsWith("Output:", StringComparison.OrdinalIgnoreCase))
                    t = t["Output:".Length..].Trim();

                if (t.StartsWith("Value:", StringComparison.OrdinalIgnoreCase))
                    return t["Value:".Length..].Trim();

                // Some builds: "/family/key : value"
                var m = System.Text.RegularExpressions.Regex.Match(
                    t,
                    @"/" + System.Text.RegularExpressions.Regex.Escape(family)
                    + @"/" + System.Text.RegularExpressions.Regex.Escape(key)
                    + @"\s*:\s*(.*)$");
                if (m.Success)
                    return m.Groups[1].Value.Trim();
            }

            // Fallback: event-generating DBGet
            var ev = await _ami.SendEventGeneratingActionAsync(
                new DBGetAction(family, key) { ActionId = $"dbget-{family}-{key}-{Guid.NewGuid():N}" },
                timeoutMs: 8_000,
                cancellationToken: ct).ConfigureAwait(false);
            if (ev.Events != null)
            {
                foreach (var e in ev.Events)
                {
                    if (e is DBGetResponseEvent db && !string.IsNullOrWhiteSpace(db.Val))
                        return db.Val;
                }
            }

            var respVal = ev.Response?.GetAttribute("Val") ?? ev.Response?.GetAttribute("Value");
            return string.IsNullOrWhiteSpace(respVal) ? null : respVal;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DBGet {Family}/{Key} failed", family, key);
            return null;
        }
    }

    private async Task<Dictionary<string, string>> LoadAstDbFamilyAsync(string family, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resp = await _ami.SendActionAsync(
            new CommandAction($"database show {family}"),
            ct,
            timeoutMs: 60_000).ConfigureAwait(false);

        foreach (var raw in CollectCommandOutputLines(resp))
        {
            var t = raw.Trim();
            if (t.StartsWith("Output:", StringComparison.OrdinalIgnoreCase))
                t = t["Output:".Length..].Trim();

            // /NTKRxxx/key : value
            var m = System.Text.RegularExpressions.Regex.Match(
                t,
                @"/" + System.Text.RegularExpressions.Regex.Escape(family) + @"/([^\s:]+)\s*:\s*(.*)$");
            if (!m.Success)
                continue;

            var key = m.Groups[1].Value.Trim();
            var val = m.Groups[2].Value.Trim();
            if (key.Length == 0)
                continue;
            map[key] = val;
        }

        return map;
    }

    private static IEnumerable<string> CollectCommandOutputLines(ManagerResponse resp)
    {
        if (resp is CommandResponse cr && cr.Result is { Count: > 0 })
        {
            foreach (var line in cr.Result)
                yield return line;
            yield break;
        }

        var output = resp.GetAttribute("Output");
        if (string.IsNullOrWhiteSpace(output))
            yield break;

        foreach (var line in output.Split(
                     new[] { "\r\n", "\n", "\r" },
                     StringSplitOptions.RemoveEmptyEntries))
            yield return line;
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
