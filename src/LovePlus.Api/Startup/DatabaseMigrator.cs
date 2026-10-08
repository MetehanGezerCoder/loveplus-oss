using LovePlus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Api.Startup;

public static class DatabaseMigrator
{
    /// <summary>
    /// Brings a relational database to the current model before the first request. A two-person
    /// deployment has no separate migration pipeline, so this runs in-process and is reported
    /// through diagnostics rather than assumed. It is a no-op for the Development in-memory store.
    /// </summary>
    public static async Task<bool> MigrateAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Database:MigrateOnStartup", true))
        {
            logger.LogInformation("Database__MigrateOnStartup is false; skipping automatic migration");
            return false;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LovePlusDbContext>();
        if (!db.Database.IsRelational())
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            return false;
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pending.Length == 0)
        {
            logger.LogInformation("Database schema is already current");
            return true;
        }

        logger.LogInformation("Applying {Count} pending database migrations", pending.Length);
        await db.Database.MigrateAsync(cancellationToken);
        return true;
    }
}
