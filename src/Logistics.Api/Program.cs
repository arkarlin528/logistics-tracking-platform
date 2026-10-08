using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Logistics.Api.Auth;
using Logistics.Api.Endpoints;
using Logistics.Api.Infrastructure;
using Logistics.Api.Realtime;
using Logistics.Api.Simulation;
using Logistics.Application;
using Logistics.Application.Common;
using Logistics.Infrastructure;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IShipmentNotifier, SignalRShipmentNotifier>();

// Auth. Options are bound lazily (not read here) so tests and environment variables can override them.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .Validate(o => o.SigningKey.Length >= 32,
        "Jwt:SigningKey must be at least 32 characters. Set it via configuration or the Jwt__SigningKey environment variable.")
    .ValidateOnStart();
builder.Services.Configure<RateImportOptions>(builder.Configuration.GetSection(RateImportOptions.Section));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SecurityKey(),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        // Browsers can't set headers on WebSocket requests, so SignalR sends the token in the query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(TrackingHub.Path))
                    context.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Staff, policy => policy.RequireRole(Policies.StaffRoles));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ApiEndpoints.LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// HTTP plumbing
// Behind Caddy -> nginx the real client IP is in X-Forwarded-For (two hops). Without this, every
// request looks like it comes from nginx and the per-IP login rate limit becomes one global bucket.
// The API port is only reachable inside the Docker network, so trusting the proxies there is safe.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection(SimulationOptions.Section));
builder.Services.AddHostedService<ShipmentSimulator>();

// API docs: OpenAPI JSON from Swashbuckle, rendered by Scalar at /docs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Logistics Tracking API",
        Version = "v1",
        Description = "Shipments, live tracking events, freight rates and dashboards. Log in via POST /api/auth/login and use the token as a Bearer header.",
    });
    var bearer = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseSwagger();
app.MapScalarApiReference("/docs", options =>
{
    options.Title = "Logistics Tracking API";
    options.OpenApiRoutePattern = "/swagger/{documentName}/swagger.json";
});
app.MapGet("/", () => Results.Redirect("/docs")).ExcludeFromDescription();

app.MapApiEndpoints();
app.MapHub<TrackingHub>(TrackingHub.Path);
app.MapHealthChecks("/health");

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().MigrateAndSeedAsync();
}

app.Run();

/// <summary>Exposed for WebApplicationFactory in the integration tests.</summary>
public partial class Program;
