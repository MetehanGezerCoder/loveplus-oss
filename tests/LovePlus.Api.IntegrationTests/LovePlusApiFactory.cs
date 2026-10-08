using LovePlus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LovePlus.Api.IntegrationTests;

public sealed class LovePlusApiFactory : WebApplicationFactory<Program>
{
    private static readonly string? TestPostgresConnectionString =
        Environment.GetEnvironmentVariable("LOVEPLUS_TEST_POSTGRES");

    public bool UsesPostgres => !string.IsNullOrWhiteSpace(TestPostgresConnectionString);

    public LovePlusApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Postgres",
            TestPostgresConnectionString ?? "Host=localhost;Database=unused;Username=unused");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379,abortConnect=false");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "loveplus-tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "loveplus-test-client");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", new string('t', 64));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var postgresConnection =
            TestPostgresConnectionString ?? "Host=localhost;Database=unused;Username=unused";

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = postgresConnection,
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false",
                ["Jwt:Issuer"] = "loveplus-tests",
                ["Jwt:Audience"] = "loveplus-test-client",
                ["Jwt:SigningKey"] = new string('t', 64),
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "30"
            }));
        builder.ConfigureServices(services =>
        {
            // CI supplies LOVEPLUS_TEST_POSTGRES so the application keeps its production Npgsql
            // registration, including NetTopologySuite and EnableRetryOnFailure. Local contributors
            // can still run the full default test suite without a database by falling back to the
            // in-memory provider.
            if (UsesPostgres)
            {
                return;
            }

            var descriptors = services
                .Where(x => x.ServiceType == typeof(DbContextOptions<LovePlusDbContext>)
                    || x.ServiceType == typeof(LovePlusDbContext)
                    || x.ServiceType == typeof(IDbContextOptionsConfiguration<LovePlusDbContext>))
                .ToArray();
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }
            services.AddDbContext<LovePlusDbContext>(options =>
                options.UseInMemoryDatabase("loveplus-integration")
                    .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }
}
