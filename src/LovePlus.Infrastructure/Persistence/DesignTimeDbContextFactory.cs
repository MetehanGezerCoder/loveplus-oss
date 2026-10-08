using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LovePlus.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LovePlusDbContext>
{
    public LovePlusDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Postgres before creating or inspecting migrations.");
        var options = new DbContextOptionsBuilder<LovePlusDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.UseNetTopologySuite())
            .Options;
        return new LovePlusDbContext(options);
    }
}
