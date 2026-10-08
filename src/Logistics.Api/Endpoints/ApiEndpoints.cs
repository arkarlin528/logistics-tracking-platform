using Logistics.Api.Auth;
using Logistics.Api.Infrastructure;
using Logistics.Application.Auth;
using Logistics.Application.Common;
using Logistics.Application.Dashboard;
using Logistics.Application.Rates;
using Logistics.Application.Reference;
using Logistics.Application.Shipments;

namespace Logistics.Api.Endpoints;

public static class ApiEndpoints
{
    public const string LoginRateLimit = "login";

    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");
        MapAuth(api.MapGroup("/auth").WithTags("Auth"));
        MapShipments(api.MapGroup("/shipments").WithTags("Shipments").RequireAuthorization());
        MapReference(api.MapGroup("/reference").WithTags("Reference data").RequireAuthorization());
        MapRates(api.MapGroup("/rates").WithTags("Freight rates"));
        api.MapGet("/dashboard", (DashboardQueries q, CancellationToken ct) => q.SummaryAsync(ct))
            .WithTags("Dashboard")
            .WithSummary("Counts by status, delays and on-time rate")
            .RequireAuthorization(Policies.Staff);
    }

    private static void MapAuth(RouteGroupBuilder group)
    {
        group.MapPost("/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
                await auth.LoginAsync(request, ct) is { } response
                    ? Results.Ok(response)
                    : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password."))
            .Validate<LoginRequest>()
            .RequireRateLimiting(LoginRateLimit)
            .WithSummary("Exchange email + password for a JWT")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", (AuthService auth, CancellationToken ct) => auth.MeAsync(ct))
            .RequireAuthorization()
            .WithSummary("The signed-in user");
    }

    private static void MapShipments(RouteGroupBuilder group)
    {
        group.MapGet("/", ([AsParameters] ShipmentFilter filter, ShipmentQueries q, CancellationToken ct) =>
                q.ListAsync(filter, ct))
            .Validate<ShipmentFilter>()
            .WithSummary("Search and page shipments. Customers only see their own.");

        group.MapGet("/{id:int}", (int id, ShipmentQueries q, CancellationToken ct) => q.GetByIdAsync(id, ct))
            .WithSummary("Shipment with its full event history")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/by-reference/{reference}", (string reference, ShipmentQueries q, CancellationToken ct) =>
                q.GetByReferenceAsync(reference, ct))
            .WithSummary("Look up a shipment by its reference, e.g. SH-100042")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateShipmentRequest request, ShipmentCommands c, CancellationToken ct) =>
            {
                var shipment = await c.CreateAsync(request, ct);
                return Results.Created($"/api/shipments/{shipment.Id}", shipment);
            })
            .Validate<CreateShipmentRequest>()
            .RequireAuthorization(Policies.Staff)
            .WithSummary("Book a new shipment")
            .Produces<ShipmentDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:int}/events", (int id, RecordEventRequest request, ShipmentCommands c, CancellationToken ct) =>
                c.RecordEventAsync(id, request, ct))
            .Validate<RecordEventRequest>()
            .RequireAuthorization(Policies.Staff)
            .WithSummary("Record a status change or position update. Pushed live to SignalR clients.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{id:int}/eta", (int id, ReviseEtaRequest request, ShipmentCommands c, CancellationToken ct) =>
                c.ReviseEtaAsync(id, request, ct))
            .Validate<ReviseEtaRequest>()
            .RequireAuthorization(Policies.Staff)
            .WithSummary("Change the estimated arrival, with a reason")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapReference(RouteGroupBuilder group)
    {
        group.MapGet("/ports", (ReferenceDataQueries q, CancellationToken ct) => q.PortsAsync(ct));
        group.MapGet("/carriers", (ReferenceDataQueries q, CancellationToken ct) => q.CarriersAsync(ct));
        group.MapGet("/customers", (ReferenceDataQueries q, CancellationToken ct) => q.CustomersAsync(ct))
            .RequireAuthorization(Policies.Staff);
    }

    private static void MapRates(RouteGroupBuilder group)
    {
        group.MapGet("/", ([AsParameters] RateSearch search, RateService rates, CancellationToken ct) =>
                rates.SearchAsync(search, ct))
            .RequireAuthorization()
            .WithSummary("Rates valid on a date (default today) for a lane, cheapest first");

        group.MapPost("/import", (RateImportRequest request, RateService rates, CancellationToken ct) =>
                rates.ImportAsync(request, ct))
            .AddEndpointFilter<ApiKeyFilter>()
            .Validate<RateImportRequest>()
            .WithSummary($"Bulk upsert from the freight-data-pipeline. Authenticated with the {ApiKeyFilter.HeaderName} header.")
            .Produces<RateImportResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
