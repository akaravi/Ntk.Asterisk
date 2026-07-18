using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Asterisk/CallFiles")]
public sealed class CallFilesController : ControllerBase
{
    private readonly ICallFileService _callFiles;

    public CallFilesController(ICallFileService callFiles) => _callFiles = callFiles;

    /// <summary>Create a Call File (stage → atomic move into outgoing spool).</summary>
    [HttpPost("Add")]
    public async Task<ActionResult<ApiResult<CallFileDto>>> Add(
        [FromBody] CallFileAddRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _callFiles.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(ApiResult<CallFileDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<CallFileDto>.Fail(ex.Message));
        }
    }
}
