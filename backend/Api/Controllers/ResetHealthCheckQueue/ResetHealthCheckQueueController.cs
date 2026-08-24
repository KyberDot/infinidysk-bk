using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Database;
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

        var resetCount = await HealthCheckQueueMutations
            .MakeDueAsync(dbClient.Ctx, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        return Ok(new ResetHealthCheckQueueResponse
        {
            Status = true,
            ResetCount = resetCount,
        });
    }
}
