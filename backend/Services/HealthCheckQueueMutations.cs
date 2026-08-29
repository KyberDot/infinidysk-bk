using Microsoft.EntityFrameworkCore;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;

namespace NzbWebDAV.Services;

public static class HealthCheckQueueMutations
{
    public static Task<int> MakeDueAsync(DavDatabaseContext context, CancellationToken cancellationToken) =>
        context.Items
            .Where(x => x.Type == DavItem.ItemType.UsenetFile)
            .Where(x => x.NextHealthCheck != null && x.NextHealthCheck != DateTimeOffset.UnixEpoch)
            .ExecuteUpdateAsync(
                x => x.SetProperty(item => item.NextHealthCheck, (DateTimeOffset?)null),
                cancellationToken);
}
