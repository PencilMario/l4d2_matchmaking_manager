using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Scheduling;

public sealed class SharedLibraryMaintenanceService(MatchmakingDbContext dbContext)
{
    public async Task AcquireAsync(CancellationToken cancellationToken)
    {
        var lease = await dbContext.SharedLibraryMaintenanceLeases.SingleOrDefaultAsync(
            candidate => candidate.Name == "shared-library", cancellationToken);
        if (lease is null)
        {
            dbContext.SharedLibraryMaintenanceLeases.Add(new SharedLibraryMaintenanceLease
            {
                OperationId = Guid.NewGuid(),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });
        }
        else
        {
            lease.OperationId = Guid.NewGuid();
            lease.ExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsHeldAsync(CancellationToken cancellationToken) =>
        dbContext.SharedLibraryMaintenanceLeases.AnyAsync(
            lease => lease.Name == "shared-library" && lease.ExpiresAt > DateTimeOffset.UtcNow,
            cancellationToken);
}
