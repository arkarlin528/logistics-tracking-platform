using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Logistics.Application.Auth;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Logistics.Api.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway SQL Server database (LocalDB on Windows, a SQL Server
/// container in CI via LOGISTICS_TEST_SQL). The database is seeded once and dropped afterwards.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Integration-Test-Pass-1";
    public const string RateApiKey = "integration-test-rate-key";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _connectionString;

    public ApiFactory()
    {
        var server = Environment.GetEnvironmentVariable("LOGISTICS_TEST_SQL")
                     ?? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
        _connectionString = $"{server};Database=LogisticsTests_{Guid.NewGuid():N}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _connectionString,
            ["Jwt:SigningKey"] = "integration-tests-signing-key-0123456789abcdef",
            ["RateImport:ApiKey"] = RateApiKey,
            ["Seed:DemoPassword"] = DemoPassword,
            ["Seed:ShipmentCount"] = "30",
            ["Database:MigrateOnStartup"] = "true",
            ["Simulation:Enabled"] = "false",
        }));
    }

    public async Task InitializeAsync()
    {
        // Touching Services builds the host, which migrates and seeds.
        _ = Services;
        await Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
    }

    public async Task<HttpClient> ClientFor(string email)
    {
        var client = CreateClient();
        var token = await TokenFor(email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>One login per user for the whole run: the login endpoint is rate limited (10/min per IP), on purpose.</summary>
    public Task<string> TokenFor(string email) => _tokens.GetOrAdd(email, async e =>
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(e, DemoPassword));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        return login!.AccessToken;
    });

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<string>> _tokens = new();

    /// <summary>A SignalR connection over the in-memory test server (long polling, since TestServer has no real sockets).</summary>
    public HubConnection HubFor(string token) => new HubConnectionBuilder()
        .WithUrl(new Uri(Server.BaseAddress, "hubs/tracking"), options =>
        {
            options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
        })
        .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
        .Build();

    public static class Users
    {
        public const string Admin = DataSeeder.AdminEmail;
        public const string Dispatcher = DataSeeder.DispatcherEmail;
        public const string Customer = DataSeeder.CustomerEmail;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
