using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using LovePlus.Api;
using LovePlus.Api.Contracts;
using LovePlus.Api.ErrorHandling;
using LovePlus.Api.Health;
using LovePlus.Api.Hubs;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Behaviors;
using LovePlus.Application.Identity.Commands;
using LovePlus.Application.Heartbeat;
using LovePlus.Application.Heartbeat.Commands;
using LovePlus.Application.Pairing.Commands;
using LovePlus.Application.Pairing.Queries;
using LovePlus.Application.Realtime;
using LovePlus.Application.Realtime.Commands;
using LovePlus.Application.Realtime.Queries;
using LovePlus.Infrastructure.Common;
using LovePlus.Infrastructure.Identity;
using LovePlus.Infrastructure.Notifications;
using LovePlus.Infrastructure.Persistence;
using LovePlus.Infrastructure.Realtime;
using LovePlus.Api.Startup;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var useInMemoryDemoInfrastructure = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue("Demo:UseInMemoryInfrastructure", false);
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is required.");
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 64)
{
    throw new InvalidOperationException("Jwt__SigningKey must contain at least 64 UTF-8 bytes.");
}

var pushOptions = builder.Configuration.GetSection(PushNotificationOptions.SectionName)
    .Get<PushNotificationOptions>() ?? new PushNotificationOptions();
builder.Services.Configure<PushNotificationOptions>(
    builder.Configuration.GetSection(PushNotificationOptions.SectionName));

// Presence must survive one lost telemetry beat, so the online window is derived from the
// client cadence instead of being an unrelated constant.
var clientStatusHeartbeatSeconds =
    builder.Configuration.GetValue("Realtime:ClientStatusHeartbeatSeconds", 60);
var presenceOnlineSeconds =
    builder.Configuration.GetValue("Realtime:OnlineSeconds", clientStatusHeartbeatSeconds * 3);
var presenceRecentlyOnlineSeconds =
    builder.Configuration.GetValue("Realtime:RecentlyOnlineSeconds", 600);
var requireHttps = builder.Configuration.GetValue("Api:RequireHttps", builder.Environment.IsProduction());

try
{
    ProductionConfigurationGuard.Validate(
        builder.Configuration,
        builder.Environment,
        jwt,
        pushOptions,
        presenceOnlineSeconds,
        clientStatusHeartbeatSeconds);
}
catch (InvalidOperationException configurationError)
{
    // A deployment mistake deserves a readable instruction, not a stack trace that buries it.
    Console.Error.WriteLine(configurationError.Message);
    return 78; // EX_CONFIG
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
if (useInMemoryDemoInfrastructure)
{
    builder.Services.AddDbContext<LovePlusDbContext>(options =>
        options.UseInMemoryDatabase("loveplus-usb-demo"));
    builder.Services.AddSingleton<ILiveStatusStore, InMemoryLiveStatusStore>();
}
else
{
    var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings__Postgres must be configured.");
    var redisConnection = builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("ConnectionStrings__Redis must be configured.");

    builder.Services.AddDbContext<LovePlusDbContext>(options =>
        options.UseNpgsql(postgresConnection, npgsql =>
        {
            npgsql.UseNetTopologySuite();
            npgsql.EnableRetryOnFailure(3);
        }));
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
    builder.Services.AddSingleton<ILiveStatusStore, RedisLiveStatusStore>();
}
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<LovePlusDbContext>());
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IGeoDistanceCalculator, GeoDistanceCalculator>();
var realtimePolicy = new RealtimeStatusPolicy(
    LiveStatusTtl: TimeSpan.FromMinutes(builder.Configuration.GetValue("Realtime:LiveStatusTtlMinutes", 12)),
    OnlineWindow: TimeSpan.FromSeconds(presenceOnlineSeconds),
    RecentlyOnlineWindow: TimeSpan.FromSeconds(presenceRecentlyOnlineSeconds),
    LocationFreshnessWindow: TimeSpan.FromMinutes(builder.Configuration.GetValue("Realtime:LocationFreshnessMinutes", 10)),
    MaxOfflinePacketAge: TimeSpan.FromHours(builder.Configuration.GetValue("Realtime:MaxOfflinePacketAgeHours", 24)),
    ApproximateAccuracyMeters: builder.Configuration.GetValue("Realtime:ApproximateAccuracyMeters", 100d),
    SimulatorEnabled: builder.Environment.IsDevelopment()
        && builder.Configuration.GetValue("Realtime:EnableSimulator", false),
    SamePlaceMaxMeters: builder.Configuration.GetValue("Realtime:Proximity:SamePlaceMaxMeters", 100d),
    VeryCloseMaxMeters: builder.Configuration.GetValue("Realtime:Proximity:VeryCloseMaxMeters", 500d),
    NearbyMaxMeters: builder.Configuration.GetValue("Realtime:Proximity:NearbyMaxMeters", 2_000d),
    SameAreaMaxMeters: builder.Configuration.GetValue("Realtime:Proximity:SameAreaMaxMeters", 10_000d));
builder.Services.AddSingleton(realtimePolicy);
builder.Services.AddScoped<IPartnerStatusPublisher, PartnerStatusPublisher>();
var heartbeatPolicy = new HeartbeatPolicy(
    EventTtl: TimeSpan.FromSeconds(builder.Configuration.GetValue("Heartbeat:EventTtlSeconds", 20)),
    RateWindow: TimeSpan.FromSeconds(builder.Configuration.GetValue("Heartbeat:RateWindowSeconds", 30)),
    MaxEventsPerWindow: builder.Configuration.GetValue("Heartbeat:MaxEventsPerWindow", 3),
    MaxPatternEntries: builder.Configuration.GetValue("Heartbeat:MaxPatternEntries", 32),
    MaxTotalDurationMilliseconds: builder.Configuration.GetValue("Heartbeat:MaxTotalDurationMilliseconds", 8_000),
    MaxEntryDurationMilliseconds: builder.Configuration.GetValue("Heartbeat:MaxEntryDurationMilliseconds", 1_500));
builder.Services.AddSingleton(heartbeatPolicy);
builder.Services.AddSingleton<IRealtimePresenceTracker, InMemoryRealtimePresenceTracker>();
builder.Services.AddSingleton<IHeartbeatPublisher, HeartbeatPublisher>();
if (useInMemoryDemoInfrastructure || builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<IEphemeralHeartbeatStore, InMemoryHeartbeatStore>();
}
else
{
    builder.Services.AddSingleton<IEphemeralHeartbeatStore, RedisHeartbeatStore>();
}
var pushSelection = builder.Services.AddPushNotifications(pushOptions, builder.Environment);
builder.Services.AddSingleton(new RuntimeDiagnosticsState
{
    ApiVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    UsesRelationalDatabase = !useInMemoryDemoInfrastructure,
    UsesRedis = !useInMemoryDemoInfrastructure,
    PushProvider = pushSelection.Provider,
    PushConfigured = pushSelection.Configured,
    RequiresHttps = requireHttps,
    PresenceOnlineSeconds = presenceOnlineSeconds,
    PresenceRecentlyOnlineSeconds = presenceRecentlyOnlineSeconds,
    ClientStatusHeartbeatSeconds = clientStatusHeartbeatSeconds
});
// Forwarded headers are only honoured for proxies the operator names, as a single address
// or as CIDR (a container gets a fresh address on every restart). An unconfigured deployment
// keeps the socket address, because a spoofable X-Forwarded-For would let a caller choose
// its own auth rate-limit partition.
var trustedProxyEntries = (builder.Configuration["Api:TrustedProxies"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var trustedProxyAddresses = new List<IPAddress>();
var trustedProxyNetworks = new List<System.Net.IPNetwork>();
foreach (var entry in trustedProxyEntries)
{
    if (entry.Contains('/') && System.Net.IPNetwork.TryParse(entry, out var network))
    {
        trustedProxyNetworks.Add(network);
    }
    else if (IPAddress.TryParse(entry, out var address))
    {
        trustedProxyAddresses.Add(address);
    }
    else
    {
        throw new InvalidOperationException(
            $"Api__TrustedProxies contains '{entry}', which is neither an IP address nor a CIDR range.");
    }
}
var trustsAnyProxy = trustedProxyAddresses.Count > 0 || trustedProxyNetworks.Count > 0;
if (trustsAnyProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var proxy in trustedProxyAddresses)
        {
            options.KnownProxies.Add(proxy);
        }
        foreach (var network in trustedProxyNetworks)
        {
            options.KnownIPNetworks.Add(network);
        }
    });
}

builder.Services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();
builder.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(typeof(UpdateUserStatusCommand).Assembly);
    configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/status")
                    && context.Request.Query.TryGetValue("access_token", out var accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue("sub");
                var sessionId = context.Principal?.FindFirstValue("session_id");
                if (!Guid.TryParse(userId, out var parsedUserId)
                    || !Guid.TryParse(sessionId, out var parsedSessionId))
                {
                    context.Fail("Required identity claims are missing.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<LovePlusDbContext>();
                var active = await db.DeviceSessions.AsNoTracking().AnyAsync(
                    x => x.Id == parsedSessionId
                        && x.UserId == parsedUserId
                        && x.RevokedAtUtc == null
                        && x.User.DeletedAtUtc == null,
                    context.HttpContext.RequestAborted);
                if (!active)
                {
                    context.Fail("Device session is no longer active.");
                }
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 16 * 1024;
    options.MaximumParallelInvocationsPerClient = 1;
}).AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("status", context => RateLimitPartition.GetTokenBucketLimiter(
        context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 30,
            TokensPerPeriod = 15,
            ReplenishmentPeriod = TimeSpan.FromSeconds(30),
            AutoReplenishment = true,
            QueueLimit = 0
        }));
    options.AddPolicy("heartbeat", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 6,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
var healthChecks = builder.Services.AddHealthChecks();
if (!useInMemoryDemoInfrastructure)
{
    healthChecks
        .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"])
        .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
}

var app = builder.Build();
var diagnosticsState = app.Services.GetRequiredService<RuntimeDiagnosticsState>();
diagnosticsState.MigrationsApplied = await DatabaseMigrator.MigrateAsync(
    app.Services,
    app.Configuration,
    app.Logger,
    CancellationToken.None);
await DemoDataSeeder.SeedAsync(app.Services, app.Configuration, app.Environment, CancellationToken.None);

if (trustsAnyProxy)
{
    app.UseForwardedHeaders();
}
if (requireHttps)
{
    // TLS terminates at the reverse proxy, so the API must not redirect (it cannot know the
    // public port and would loop). It rejects a request that the proxy tells us arrived in
    // cleartext, and leaves HSTS to the edge that actually serves HTTPS.
    app.Use(async (context, next) =>
    {
        // With Api__TrustedProxies configured, UseForwardedHeaders has already consumed and
        // removed the header, so Request.IsHttps is the authoritative signal. The raw header
        // is only a fallback for a proxied deployment that has not declared its proxy yet.
        var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString();
        var arrivedSecurely = context.Request.IsHttps
            || string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);
        if (!arrivedSecurely && !context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "HTTPS required",
                detail = "Love+ rejects cleartext API traffic. Terminate TLS in front of the API."
            });
            return;
        }
        await next(context);
    });
}
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

var auth = app.MapGroup("/api/auth").RequireRateLimiting("auth");
auth.MapPost("/register", (RegisterRequest request, ISender sender, CancellationToken ct) =>
    sender.Send(new RegisterCommand(
        request.Email, request.Password, request.DisplayName, request.DeviceId, request.DeviceName), ct));
auth.MapPost("/login", (LoginRequest request, ISender sender, CancellationToken ct) =>
    sender.Send(new LoginCommand(
        request.Email, request.Password, request.DeviceId, request.DeviceName), ct));
auth.MapPost("/refresh", (RefreshRequest request, ISender sender, CancellationToken ct) =>
    sender.Send(new RefreshTokenCommand(request.RefreshToken, request.DeviceId), ct));
auth.MapPost("/logout", async (ClaimsPrincipal principal, ISender sender, CancellationToken ct) =>
    {
        await sender.Send(new LogoutCommand(principal.RequiredUserId(), principal.RequiredSessionId()), ct);
        return Results.NoContent();
    })
    .RequireAuthorization();

var pairing = app.MapGroup("/api/pairing").RequireAuthorization();
pairing.MapPost("/code", (ClaimsPrincipal principal, ISender sender, CancellationToken ct) =>
    sender.Send(new CreatePairingCodeCommand(principal.RequiredUserId()), ct));
pairing.MapPost("/redeem", (RedeemPairingCodeRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken ct) =>
    sender.Send(new RedeemPairingCodeCommand(principal.RequiredUserId(), request.Code), ct));
pairing.MapGet("/current", (ClaimsPrincipal principal, ISender sender, CancellationToken ct) =>
    sender.Send(new GetCurrentPairQuery(principal.RequiredUserId()), ct));

var status = app.MapGroup("/api/status").RequireAuthorization().RequireRateLimiting("status");
status.MapPost("/", (StatusUpdateRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken ct) =>
    sender.Send(request.ToCommand(principal.RequiredUserId(), principal.RequiredDeviceId()), ct));
status.AddEndpointFilter(async (context, next) =>
{
    if (context.HttpContext.Request.Method != HttpMethods.Post
        || context.HttpContext.User.FindFirstValue("token_use") is "telemetry" or "app")
    {
        return await next(context);
    }
    return Results.Forbid();
});
status.MapGet("/partner", (ClaimsPrincipal principal, ISender sender, CancellationToken ct) =>
    sender.Send(new GetPartnerStatusQuery(principal.RequiredUserId()), ct));
status.MapPost("/mood", (
    UpdateMoodRequest request,
    ClaimsPrincipal principal,
    ISender sender,
    CancellationToken ct) => sender.Send(new UpdateMoodCommand(
        principal.RequiredUserId(), request.Mood, request.IsMoodSharingEnabled), ct))
    .AddEndpointFilter(async (context, next) =>
        context.HttpContext.User.FindFirstValue("token_use") == "app"
            ? await next(context)
            : Results.Forbid());

var heartbeat = app.MapGroup("/api/heartbeat")
    .RequireAuthorization()
    .RequireRateLimiting("heartbeat")
    .AddEndpointFilter(async (context, next) =>
    {
        return context.HttpContext.User.FindFirstValue("token_use") == "app"
            ? await next(context)
            : Results.Forbid();
    });
heartbeat.MapPost("/", (SendHeartbeatRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken ct) =>
    sender.Send(new SendHeartbeatCommand(
        principal.RequiredUserId(),
        request.EventId,
        request.Pattern), ct));
heartbeat.MapPost("/{eventId:guid}/ack", (
    Guid eventId,
    AcknowledgeHeartbeatRequest request,
    ClaimsPrincipal principal,
    ISender sender,
    CancellationToken ct) => sender.Send(new AcknowledgeHeartbeatCommand(
        principal.RequiredUserId(),
        eventId,
        request.State), ct));

var devices = app.MapGroup("/api/devices")
    .RequireAuthorization()
    .RequireRateLimiting("status")
    .AddEndpointFilter(async (context, next) =>
        context.HttpContext.User.FindFirstValue("token_use") == "app"
            ? await next(context)
            : Results.Forbid());
devices.MapPost("/push-token", (
    RegisterPushTokenRequest request,
    ClaimsPrincipal principal,
    ISender sender,
    CancellationToken ct) => sender.Send(new RegisterPushTokenCommand(
        principal.RequiredUserId(),
        principal.RequiredSessionId(),
        request.Platform,
        request.Token), ct));

// Answers "why is this phone not working" without exposing credentials, coordinates or
// partner data. Authenticated so it cannot be used to fingerprint the deployment anonymously.
app.MapGet("/api/diagnostics", (RuntimeDiagnosticsState state, IHostEnvironment environment, IClock clock) =>
        new RuntimeDiagnostics(
            environment.EnvironmentName,
            state.ApiVersion,
            state.UsesRelationalDatabase,
            state.UsesRedis,
            state.MigrationsApplied,
            state.PushProvider,
            state.PushConfigured,
            state.RequiresHttps,
            state.PresenceOnlineSeconds,
            state.PresenceRecentlyOnlineSeconds,
            state.ClientStatusHeartbeatSeconds,
            clock.UtcNow))
    .RequireAuthorization();

app.MapHub<StatusHub>("/hubs/status", options => options.CloseOnAuthenticationExpiration = true)
    .RequireAuthorization();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.Run();
return 0;

public partial class Program;
