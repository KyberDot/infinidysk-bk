using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Services;

namespace NzbWebDAV.Api.Controllers.ResetHealthCheckQueue;

[ApiController]
[Route("api/reset-health-check-queue")]
public class ResetHealthCheckQueueController(DavDatabaseClient dbClient) : BaseApiController
{
    protected override async Task<IActionResult> HandleRequest()
    {
        if (!HttpMethods.IsPost(HttpContext.Request.Method))
        {
            return StatusCode(
                StatusCodes.Status405MethodNotAllowed,
                new BaseApiResponse { Status = false, Error = "POST required" });
        }

        // Mark every non-urgent usenet file with the forced-recheck sentinel. Unlike a plain
        // null reset, the sentinel overrides the history-linked exclusion in the health-check
        // queue query, so files still present in SAB history are re-checked too — without
        // deleting any history rows. Urgent repairs (UnixEpoch) are left untouched.
        var resetCount = await dbClient.Ctx.Items
            .Where(x => x.Type == DavItem.ItemType.UsenetFile)
            .Where(x => x.NextHealthCheck != DateTimeOffset.UnixEpoch)
            .ExecuteUpdateAsync(
                x => x.SetProperty(item => item.NextHealthCheck, HealthCheckService.ForcedRecheckSentinel),
                HttpContext.RequestAborted)
            .ConfigureAwait(false);

        return Ok(new ResetHealthCheckQueueResponse
        {
            Status = true,
            ResetCount = resetCount,
        });
    }
}
