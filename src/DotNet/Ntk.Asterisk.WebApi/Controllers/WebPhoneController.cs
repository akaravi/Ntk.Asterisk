using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

/// <summary>
/// Softphone provision + buddies/CDR/recording/QoS. Config-style routes for provision;
/// entity-style for stores. Optional API-key gate via WebPhone:RequireApiKey + WebPhone:ApiKey.
/// </summary>
[ApiController]
[Route("api/v1/WebPhone")]
public sealed class WebPhoneController : ControllerBase
{
    private readonly IWebPhoneExtensionStore _extensions;
    private readonly IWebPhoneProvisionTokenStore _provisionTokens;
    private readonly IWebPhoneBuddyStore _buddies;
    private readonly IWebPhoneCdrStore _cdr;
    private readonly IWebPhoneRecordingStore _recordings;
    private readonly IWebPhoneQosStore _qos;
    private readonly IWebPhonePresenceService _presence;
    private readonly IAsteriskSettingsService _settings;
    private readonly IOptionsMonitor<WebPhoneOptions> _webPhoneOptions;
    private readonly IQueueAclSessionService _aclSessions;
    private readonly ILogger<WebPhoneController> _logger;

    public WebPhoneController(
        IWebPhoneExtensionStore extensions,
        IWebPhoneProvisionTokenStore provisionTokens,
        IWebPhoneBuddyStore buddies,
        IWebPhoneCdrStore cdr,
        IWebPhoneRecordingStore recordings,
        IWebPhoneQosStore qos,
        IWebPhonePresenceService presence,
        IAsteriskSettingsService settings,
        IOptionsMonitor<WebPhoneOptions> webPhoneOptions,
        IQueueAclSessionService aclSessions,
        ILogger<WebPhoneController> logger)
    {
        _extensions = extensions;
        _provisionTokens = provisionTokens;
        _buddies = buddies;
        _cdr = cdr;
        _recordings = recordings;
        _qos = qos;
        _presence = presence;
        _settings = settings;
        _webPhoneOptions = webPhoneOptions;
        _aclSessions = aclSessions;
        _logger = logger;
    }

    [HttpGet("GetSipConfig")]
    [HttpPost("GetSipConfig")]
    public ActionResult<ApiResult<SipConfigDto>> GetSipConfig(
        [FromQuery] string? sipUsername,
        [FromQuery] string? serverId,
        [FromBody] SipConfigRequest? body)
    {
        try
        {
            if (!TryAuthorizeApiKey(out var authFail))
                return Ok(ApiResult<SipConfigDto>.Fail(authFail!));

            var username = FirstNonEmpty(body?.SipUsername, sipUsername);
            var sid = FirstNonEmpty(body?.ServerId, serverId);

            WebPhoneExtensionRecord? ext = null;
            if (!string.IsNullOrWhiteSpace(username))
                ext = _extensions.FindByUsername(username!);
            else
            {
                var first = _extensions.GetList().FirstOrDefault(e => e.IsEnabled);
                if (first != null)
                    ext = _extensions.FindById(first.Id);
            }

            if (ext == null || string.IsNullOrWhiteSpace(ext.SipPassword))
                return Ok(ApiResult<SipConfigDto>.Fail(
                    "No SIP extension provisioned. Add App_Data/webphone-extensions.json entry first."));

            var server = ResolveServer(sid ?? ext.ServerId);
            var dto = BuildSipConfig(ext, server);
            _logger.LogInformation(
                "GetSipConfig served username={Username} serverId={ServerId}",
                ext.SipUsername,
                server?.Id);
            return Ok(ApiResult<SipConfigDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetSipConfig failed");
            return Ok(ApiResult<SipConfigDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Redeem admin-issued provision token — returns SIP config, buddies, and feature flags.
    /// No WebPhone API key required; the token is the credential.
    /// </summary>
    [HttpPost("GetProvisionByToken")]
    public ActionResult<ApiResult<WebPhoneProvisionBundleDto>> GetProvisionByToken(
        [FromBody] WebPhoneProvisionByTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Token))
                return Ok(ApiResult<WebPhoneProvisionBundleDto>.Fail("Provision token is required."));

            var tokenRecord = _provisionTokens.FindByToken(request.Token);
            if (tokenRecord is null)
                return Ok(ApiResult<WebPhoneProvisionBundleDto>.Fail("Invalid or expired provision token."));

            var ext = _extensions.FindById(tokenRecord.ExtensionId);
            if (ext is null || !ext.IsEnabled || string.IsNullOrWhiteSpace(ext.SipPassword))
                return Ok(ApiResult<WebPhoneProvisionBundleDto>.Fail(
                    "Extension for this token is missing, disabled, or has no SIP secret."));

            var server = ResolveServer(ext.ServerId);
            var sip = BuildSipConfig(ext, server);
            var buddies = _buddies.GetList();
            var opts = _webPhoneOptions.CurrentValue;
            _provisionTokens.TouchLastUsed(tokenRecord.Id);

            var bundle = new WebPhoneProvisionBundleDto
            {
                SipConfig = sip,
                Buddies = buddies,
                Options = new WebPhoneOptionsDto
                {
                    EnableTransfer = opts.EnableTransfer,
                    EnableConference = opts.EnableConference,
                    EnableRecordAll = opts.EnableRecordAll,
                    EnableVideo = opts.EnableVideo,
                    EnablePresence = opts.EnablePresence,
                    EnableMwi = opts.EnableMwi,
                    RequireApiKey = opts.IsApiKeyGateActive
                }
            };

            _logger.LogInformation(
                "GetProvisionByToken served extension={Username} tokenId={TokenId}",
                ext.SipUsername,
                tokenRecord.Id);
            return Ok(ApiResult<WebPhoneProvisionBundleDto>.Ok(bundle));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetProvisionByToken failed");
            return Ok(ApiResult<WebPhoneProvisionBundleDto>.Fail(ex.Message));
        }
    }

    [HttpGet("ProvisionTokens/GetList")]
    public ActionResult<ApiResult<WebPhoneProvisionTokenDto>> ProvisionTokensGetList()
    {
        if (!TryAuthorizeProvisioner(out var authFail))
            return Ok(ApiResult<WebPhoneProvisionTokenDto>.Fail(authFail!));
        return Ok(ApiResult<WebPhoneProvisionTokenDto>.Ok(_provisionTokens.GetList()));
    }

    [HttpPost("ProvisionTokens/Add")]
    public ActionResult<ApiResult<WebPhoneProvisionTokenCreatedDto>> ProvisionTokensAdd(
        [FromBody] WebPhoneProvisionTokenCreateRequest request)
    {
        try
        {
            if (!TryAuthorizeProvisioner(out var authFail))
                return Ok(ApiResult<WebPhoneProvisionTokenCreatedDto>.Fail(authFail!));

            var ext = _extensions.FindById(request.ExtensionId);
            if (ext is null)
                return Ok(ApiResult<WebPhoneProvisionTokenCreatedDto>.Fail("Extension not found."));

            return Ok(ApiResult<WebPhoneProvisionTokenCreatedDto>.Ok(_provisionTokens.Create(request)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhoneProvisionTokenCreatedDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ProvisionTokens/ActionDelete")]
    public ActionResult<ApiResult<object>> ProvisionTokensDelete([FromBody] WebPhoneProvisionTokenIdRequest request)
    {
        try
        {
            if (!TryAuthorizeProvisioner(out var authFail))
                return Ok(ApiResult.Fail(authFail!));
            if (!_provisionTokens.Delete(request.Id))
                return Ok(ApiResult.Fail("Provision token not found."));
            return Ok(ApiResult.OkEmpty());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    [HttpGet("Extensions/GetList")]
    public ActionResult<ApiResult<WebPhoneExtensionDto>> ExtensionsGetList() =>
        Ok(ApiResult<WebPhoneExtensionDto>.Ok(_extensions.GetList()));

    [HttpPost("Extensions/Add")]
    [HttpPost("Extensions/Update")]
    public ActionResult<ApiResult<WebPhoneExtensionDto>> ExtensionsUpsert(
        [FromBody] WebPhoneExtensionUpsertRequest request)
    {
        try
        {
            if (!TryAuthorizeApiKey(out var authFail))
                return Ok(ApiResult<WebPhoneExtensionDto>.Fail(authFail!));
            return Ok(ApiResult<WebPhoneExtensionDto>.Ok(_extensions.Upsert(request)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhoneExtensionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Extensions/ActionDelete")]
    public ActionResult<ApiResult<object>> ExtensionsDelete([FromBody] WebPhoneBuddyIdRequest request)
    {
        try
        {
            if (!TryAuthorizeApiKey(out var authFail))
                return Ok(ApiResult.Fail(authFail!));
            if (!_extensions.Delete(request.Id))
                return Ok(ApiResult.Fail($"Extension '{request.Id}' not found."));
            return Ok(ApiResult.OkEmpty());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    [HttpGet("Buddies/GetList")]
    public ActionResult<object> BuddiesGetList(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? quickSearch = null)
    {
        try
        {
            var all = _buddies.GetList(quickSearch);
            var size = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);
            var index = pageIndex < 0 ? 0 : pageIndex;
            var page = all.Skip(index * size).Take(size).ToList();
            return Ok(new
            {
                isSuccess = true,
                data = page,
                errorMessage = (string?)null,
                totalCount = all.Count,
                pageIndex = index,
                pageSize = size
            });
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhoneBuddyDto>.Fail(ex.Message));
        }
    }

    [HttpGet("Buddies/GetOne/{id}")]
    public ActionResult<ApiResult<WebPhoneBuddyDto>> BuddiesGetOne(string id)
    {
        var dto = _buddies.GetOne(id);
        if (dto == null)
            return Ok(ApiResult<WebPhoneBuddyDto>.Fail($"Buddy '{id}' not found."));
        return Ok(ApiResult<WebPhoneBuddyDto>.Ok(dto));
    }

    [HttpPost("Buddies/Add")]
    public ActionResult<ApiResult<WebPhoneBuddyDto>> BuddiesAdd([FromBody] WebPhoneBuddyUpsertRequest request)
    {
        try
        {
            return Ok(ApiResult<WebPhoneBuddyDto>.Ok(_buddies.Add(request)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhoneBuddyDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Buddies/Update")]
    public ActionResult<ApiResult<WebPhoneBuddyDto>> BuddiesUpdate([FromBody] WebPhoneBuddyUpsertRequest request)
    {
        try
        {
            return Ok(ApiResult<WebPhoneBuddyDto>.Ok(_buddies.Update(request)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhoneBuddyDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Buddies/ActionDelete")]
    public ActionResult<ApiResult<object>> BuddiesDelete([FromBody] WebPhoneBuddyIdRequest request)
    {
        try
        {
            if (!_buddies.Delete(request.Id))
                return Ok(ApiResult.Fail($"Buddy '{request.Id}' not found."));
            return Ok(ApiResult.OkEmpty());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    [HttpGet("Cdr/GetList")]
    public ActionResult<object> CdrGetList(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] string? quickSearch = null)
    {
        try
        {
            var (items, total) = _cdr.GetList(pageIndex, pageSize, sortBy, sortDir, quickSearch);
            var size = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);
            var index = pageIndex < 0 ? 0 : pageIndex;
            return Ok(new
            {
                isSuccess = true,
                data = items,
                errorMessage = (string?)null,
                totalCount = total,
                pageIndex = index,
                pageSize = size
            });
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhoneCdrDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Cdr/Add")]
    public ActionResult<ApiResult<WebPhoneCdrDto>> CdrAdd([FromBody] WebPhoneCdrAddRequest request)
    {
        try
        {
            return Ok(ApiResult<WebPhoneCdrDto>.Ok(_cdr.Add(request)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhoneCdrDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Presence/ActionQuery")]
    public async Task<ActionResult<ApiResult<WebPhonePresenceDto>>> PresenceQuery(
        [FromQuery] string? extension,
        [FromQuery] string? context,
        [FromBody] WebPhonePresenceQueryRequest? body,
        CancellationToken cancellationToken)
    {
        try
        {
            var ext = FirstNonEmpty(body?.Extension, extension);
            var ctx = FirstNonEmpty(body?.Context, context);
            var dto = await _presence.QueryExtensionAsync(ext ?? string.Empty, ctx, cancellationToken)
                .ConfigureAwait(false);
            if (dto == null)
                return Ok(ApiResult<WebPhonePresenceDto>.Fail("Extension is required."));
            return Ok(ApiResult<WebPhonePresenceDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhonePresenceDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Mwi/ActionQuery")]
    public async Task<ActionResult<ApiResult<WebPhoneMwiDto>>> MwiQuery(
        [FromQuery] string? mailbox,
        [FromBody] WebPhoneMwiQueryRequest? body,
        CancellationToken cancellationToken)
    {
        try
        {
            var mb = FirstNonEmpty(body?.Mailbox, mailbox);
            var dto = await _presence.QueryMailboxAsync(mb ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (dto == null)
                return Ok(ApiResult<WebPhoneMwiDto>.Fail("Mailbox is required."));
            return Ok(ApiResult<WebPhoneMwiDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhoneMwiDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Recordings/Add")]
    [RequestSizeLimit(104_857_600)]
    public async Task<ActionResult<ApiResult<WebPhoneRecordingMetaDto>>> RecordingsAdd(
        IFormFile? file,
        [FromForm] string? buddyId,
        [FromForm] string? cdrId,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Ok(ApiResult<WebPhoneRecordingMetaDto>.Fail("file is required."));

            await using var stream = file.OpenReadStream();
            var dto = await _recordings.SaveAsync(
                stream,
                file.FileName,
                file.ContentType,
                buddyId,
                cdrId,
                notes,
                cancellationToken).ConfigureAwait(false);
            return Ok(ApiResult<WebPhoneRecordingMetaDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebPhone recording upload failed");
            return Ok(ApiResult<WebPhoneRecordingMetaDto>.Fail(ex.Message));
        }
    }

    [HttpGet("Recordings/GetList")]
    public ActionResult<ApiResult<WebPhoneRecordingMetaDto>> RecordingsGetList() =>
        Ok(ApiResult<WebPhoneRecordingMetaDto>.Ok(_recordings.GetList()));

    [HttpGet("Recordings/GetOne/{id}")]
    public ActionResult RecordingsGetOne(string id)
    {
        var path = _recordings.ResolveFilePath(id);
        if (path == null)
            return NotFound();
        var contentType = "application/octet-stream";
        return PhysicalFile(path, contentType, Path.GetFileName(path));
    }

    [HttpPost("Qos/Add")]
    public ActionResult<ApiResult<WebPhoneQosDto>> QosAdd([FromBody] WebPhoneQosAddRequest request)
    {
        try
        {
            return Ok(ApiResult<WebPhoneQosDto>.Ok(_qos.Add(request)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<WebPhoneQosDto>.Fail(ex.Message));
        }
    }

    [HttpGet("Qos/GetList")]
    public ActionResult<ApiResult<WebPhoneQosDto>> QosGetList([FromQuery] int take = 100) =>
        Ok(ApiResult<WebPhoneQosDto>.Ok(_qos.GetList(take)));

    [HttpGet("Config/GetWebPhoneOptions")]
    public ActionResult<ApiResult<WebPhoneOptionsDto>> GetWebPhoneOptions()
    {
        var o = _webPhoneOptions.CurrentValue;
        return Ok(ApiResult<WebPhoneOptionsDto>.Ok(new WebPhoneOptionsDto
        {
            EnableTransfer = o.EnableTransfer,
            EnableConference = o.EnableConference,
            EnableRecordAll = o.EnableRecordAll,
            EnableVideo = o.EnableVideo,
            EnablePresence = o.EnablePresence,
            EnableMwi = o.EnableMwi,
            RequireApiKey = o.IsApiKeyGateActive
        }));
    }

    /// <summary>
    /// Additive shared-secret gate. Inactive when RequireApiKey is false or ApiKey is empty (dev/local open).
    /// Accepts X-WebPhone-Api-Key or X-Api-Key. Never logs the secret or presented key.
    /// </summary>
    private bool TryAuthorizeApiKey(out string? errorMessage)
    {
        errorMessage = null;
        var opts = _webPhoneOptions.CurrentValue;
        if (!opts.IsApiKeyGateActive)
            return true;

        var presented = Request.Headers[WebPhoneOptions.ApiKeyHeaderName].FirstOrDefault()
            ?? Request.Headers[WebPhoneOptions.ApiKeyHeaderAlias].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(presented))
        {
            errorMessage = "WebPhone API key required. Send header X-WebPhone-Api-Key.";
            _logger.LogWarning("WebPhone API-key gate denied: missing header");
            return false;
        }

        if (!FixedTimeEquals(presented.Trim(), opts.ApiKey!.Trim()))
        {
            errorMessage = "WebPhone API key invalid.";
            _logger.LogWarning("WebPhone API-key gate denied: mismatch");
            return false;
        }

        return true;
    }

    /// <summary>Admin Queue ACL session or WebPhone API key for provision-token management.</summary>
    private bool TryAuthorizeProvisioner(out string? errorMessage)
    {
        errorMessage = null;
        var principal = _aclSessions.ResolveFromHttp(Request);
        if (principal?.IsAdmin == true)
            return true;
        return TryAuthorizeApiKey(out errorMessage);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length)
        {
            // Still compare to avoid short-circuit timing on length alone for equal-length paths.
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, ba);
            return false;
        }

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private AsteriskServerConfig? ResolveServer(string? serverId)
    {
        if (!string.IsNullOrWhiteSpace(serverId))
        {
            var all = _settings.GetEnabledServerConfigs();
            return all.FirstOrDefault(s =>
                string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase));
        }

        return _settings.GetActiveServer();
    }

    private SipConfigDto BuildSipConfig(WebPhoneExtensionRecord ext, AsteriskServerConfig? server)
    {
        var features = _webPhoneOptions.CurrentValue;
        var host = FirstNonEmpty(
            server?.SipWebsocketHost,
            ExtractHost(server?.SipWebsocketUrl),
            server?.Host) ?? "127.0.0.1";
        var port = server?.WebSocketPort is > 0
            ? server.WebSocketPort.Value.ToString()
            : "8089";
        var path = string.IsNullOrWhiteSpace(server?.WebSocketPath) ? "/ws" : server!.WebSocketPath!;
        var domain = FirstNonEmpty(server?.SipDomain, host) ?? host;
        var useTls = server?.SipUseTls ?? false;
        var wssUrl = FirstNonEmpty(
            server?.SipWebsocketUrl,
            $"{(useTls ? "wss" : "ws")}://{host}:{port}{path}");

        return new SipConfigDto
        {
            SipUsername = ext.SipUsername,
            SipPassword = ext.SipPassword,
            ProfileName = string.IsNullOrWhiteSpace(ext.ProfileName) ? ext.SipUsername : ext.ProfileName,
            ServerIp = host,
            WssUrl = wssUrl,
            WebSocketPort = port,
            ServerPath = path,
            SipDomain = domain,
            SipUseTls = useTls,
            StunServersJson = server?.StunServersJson,
            ServerId = server?.Id,
            Features = new WebPhoneOptionsDto
            {
                EnableTransfer = features.EnableTransfer,
                EnableConference = features.EnableConference,
                EnableRecordAll = features.EnableRecordAll,
                EnableVideo = features.EnableVideo,
                EnablePresence = features.EnablePresence,
                EnableMwi = features.EnableMwi,
                RequireApiKey = features.IsApiKeyGateActive
            }
        };
    }

    private static string? ExtractHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Host;
        return null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
