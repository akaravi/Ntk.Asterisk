using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

/// <summary>Auth routes (naming-exempt): queue ACL login session.</summary>
[ApiController]
[Route("api/v1/Auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IQueueAclStore _store;
    private readonly IQueueAclSessionService _sessions;

    public AuthController(IQueueAclStore store, IQueueAclSessionService sessions)
    {
        _store = store;
        _sessions = sessions;
    }

    [HttpGet("status")]
    public ActionResult<ApiResult<QueueAclStatusDto>> Status()
    {
        var principal = _sessions.ResolveFromHttp(Request);
        var dto = new QueueAclStatusDto
        {
            RequireLogin = _store.RequireLogin,
            GateActive = _store.IsGateActive,
            IsAuthenticated = principal is not null,
            Username = principal?.Username,
            Role = principal?.Role,
            AllowedQueues = principal?.AllowedQueues.ToArray() ?? Array.Empty<string>(),
            IsAdmin = principal?.IsAdmin ?? false,
            UserCount = _store.CountEnabled
        };
        return Ok(ApiResult<QueueAclStatusDto>.Ok(dto));
    }

    [HttpPost("login")]
    public ActionResult<ApiResult<QueueAclSessionDto>> Login([FromBody] QueueAclLoginRequest request)
    {
        try
        {
            var session = _store.Login(request.Username, request.Password);
            if (session is null)
                return Ok(ApiResult<QueueAclSessionDto>.Fail("Invalid username or password."));
            return Ok(ApiResult<QueueAclSessionDto>.Ok(session));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<QueueAclSessionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("logout")]
    public ActionResult<ApiResult<object>> Logout()
    {
        var token = Request.Headers[QueueAclOptions.TokenHeaderName].FirstOrDefault();
        _sessions.Revoke(token);
        return Ok(ApiResult.OkEmpty());
    }

    [HttpGet("me")]
    public ActionResult<ApiResult<QueueAclStatusDto>> Me() => Status();
}

[ApiController]
[Route("api/v1/Asterisk/QueueAclUsers")]
public sealed class QueueAclUsersController : ControllerBase
{
    private readonly IQueueAclStore _store;
    private readonly IQueueAclSessionService _sessions;

    public QueueAclUsersController(IQueueAclStore store, IQueueAclSessionService sessions)
    {
        _store = store;
        _sessions = sessions;
    }

    [HttpGet("GetList")]
    public ActionResult<ApiResult<QueueAclUserDto>> GetList([FromQuery] string? quickSearch = null)
    {
        if (!TryAuthorizeAdmin(out var fail))
            return Ok(ApiResult<QueueAclUserDto>.Fail(fail!));
        return Ok(ApiResult<QueueAclUserDto>.Ok(_store.GetList(quickSearch)));
    }

    [HttpGet("GetOne/{id}")]
    public ActionResult<ApiResult<QueueAclUserDto>> GetOne(string id)
    {
        if (!TryAuthorizeAdmin(out var fail))
            return Ok(ApiResult<QueueAclUserDto>.Fail(fail!));
        var user = _store.GetOne(id);
        if (user is null)
            return Ok(ApiResult<QueueAclUserDto>.Fail($"User '{id}' not found."));
        return Ok(ApiResult<QueueAclUserDto>.Ok(user));
    }

    [HttpPost("Add")]
    public ActionResult<ApiResult<QueueAclUserDto>> Add([FromBody] QueueAclUserUpsertRequest request)
    {
        if (!TryAuthorizeAdmin(out var fail))
            return Ok(ApiResult<QueueAclUserDto>.Fail(fail!));
        try
        {
            return Ok(ApiResult<QueueAclUserDto>.Ok(_store.Add(request)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<QueueAclUserDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Update")]
    public ActionResult<ApiResult<QueueAclUserDto>> Update([FromBody] QueueAclUserUpsertRequest request)
    {
        if (!TryAuthorizeAdmin(out var fail))
            return Ok(ApiResult<QueueAclUserDto>.Fail(fail!));
        try
        {
            return Ok(ApiResult<QueueAclUserDto>.Ok(_store.Update(request)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<QueueAclUserDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ActionDelete")]
    public ActionResult<ApiResult<object>> ActionDelete([FromBody] QueueAclUserUpsertRequest request)
    {
        if (!TryAuthorizeAdmin(out var fail))
            return Ok(ApiResult<object>.Fail(fail!));
        if (string.IsNullOrWhiteSpace(request.Id))
            return Ok(ApiResult.Fail("Id is required."));
        if (!_store.Delete(request.Id))
            return Ok(ApiResult.Fail($"User '{request.Id}' not found."));
        return Ok(ApiResult.OkEmpty());
    }

    [HttpPost("ActionSetRequireLogin")]
    public ActionResult<ApiResult<QueueAclStatusDto>> ActionSetRequireLogin(
        [FromBody] QueueAclOptionsUpdateRequest request)
    {
        if (!TryAuthorizeAdmin(out var fail))
            return Ok(ApiResult<QueueAclStatusDto>.Fail(fail!));
        if (request.RequireLogin is null)
            return Ok(ApiResult<QueueAclStatusDto>.Fail("RequireLogin is required."));
        _store.SetRequireLogin(request.RequireLogin.Value);
        var principal = _sessions.ResolveFromHttp(Request);
        return Ok(ApiResult<QueueAclStatusDto>.Ok(new QueueAclStatusDto
        {
            RequireLogin = _store.RequireLogin,
            GateActive = _store.IsGateActive,
            IsAuthenticated = principal is not null,
            Username = principal?.Username,
            Role = principal?.Role,
            AllowedQueues = principal?.AllowedQueues.ToArray() ?? Array.Empty<string>(),
            IsAdmin = principal?.IsAdmin ?? false,
            UserCount = _store.CountEnabled
        }));
    }

    /// <summary>
    /// When gate is inactive (open mode), LAN admin CRUD is allowed (parity with Settings).
    /// When gate is active, only authenticated admin role may manage users.
    /// </summary>
    private bool TryAuthorizeAdmin(out string? error)
    {
        error = null;
        if (!_store.IsGateActive)
            return true;

        var principal = _sessions.ResolveFromHttp(Request);
        if (principal is null)
        {
            error = "Authentication required.";
            return false;
        }

        if (!principal.IsAdmin)
        {
            error = "Admin role required.";
            return false;
        }

        return true;
    }
}
