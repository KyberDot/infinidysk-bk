using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Config;
using NzbWebDAV.Services;
using NzbWebDAV.Tasks;
using NzbWebDAV.Websocket;

namespace NzbWebDAV.Api.Controllers.RemoveMissingPayloads;

[ApiController]
[Route("api/remove-missing-payloads/dry-run")]
public sealed class RemoveMissingPayloadsDryRunController(
    ConfigManager configManager,
    WebsocketManager websocketManager,
    ArrReplacementSearchBudget replacementSearchBudget
) : PostOnlyApiController
{
    protected override async Task<IActionResult> HandleRequest()
    {
        var task = new RemoveMissingPayloadsTask(
            configManager,
            websocketManager,
            replacementSearchBudget,
            isDryRun: true);
        var executed = await task.Execute().ConfigureAwait(false);
        if (!executed)
            return Conflict(new { error = "Another maintenance task is already running." });
        return task.Succeeded
            ? Ok(new
            {
                status = true,
                message = task.TerminalMessage,
                previewToken = task.IssuedPreviewToken,
            })
            : BadRequest(new { status = false, error = task.TerminalMessage });
    }
}
