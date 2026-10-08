using System.Net;
using System.Net.Http.Json;
using Logistics.Application.Shipments;
using Logistics.Domain.Shipments;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ShipmentLifecycleTests(ApiFactory api)
{
    [Fact]
    public async Task Booking_assigns_a_sequential_reference_and_starts_booked()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);

        var first = await staff.BookShipment();
        var second = await staff.BookShipment();

        Assert.Matches(@"^SH-\d{6}$", first.Reference);
        Assert.Equal(int.Parse(first.Reference[3..]) + 1, int.Parse(second.Reference[3..]));
        Assert.Equal(ShipmentStatus.Booked, first.Status);
        Assert.Equal([ShipmentStatus.PickedUp, ShipmentStatus.Cancelled], first.NextStatuses);
    }

    [Fact]
    public async Task Shipment_can_travel_from_booked_to_delivered()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);
        var shipment = await staff.BookShipment();

        ShipmentDetailDto latest = shipment;
        foreach (var status in new[]
                 {
                     ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.ArrivedAtPort,
                     ShipmentStatus.CustomsClearance, ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered,
                 })
        {
            var response = await staff.PostJson($"/api/shipments/{shipment.Id}/events",
                new RecordEventRequest(status, null, "Test", null, null, null));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            latest = await response.ReadAs<ShipmentDetailDto>();
        }

        Assert.Equal(ShipmentStatus.Delivered, latest.Status);
        Assert.NotNull(latest.DeliveredAt);
        Assert.Empty(latest.NextStatuses);
        Assert.Equal(7, latest.Events.Count);
        Assert.Equal(ShipmentStatus.Delivered, latest.Events[0].Status); // newest first
    }

    [Fact]
    public async Task Skipping_a_step_returns_422_with_the_reason()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);
        var shipment = await staff.BookShipment();

        var response = await staff.PostJson($"/api/shipments/{shipment.Id}/events",
            new RecordEventRequest(ShipmentStatus.Delivered, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("from Booked to Delivered", problem!.Title);
    }

    [Fact]
    public async Task Invalid_booking_returns_field_errors()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);

        var response = await staff.PostJson("/api/shipments", new CreateShipmentRequest(
            0, 1, "THLCH", "THLCH", "", -5m, DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("CustomerId", problem!.Errors.Keys);
        Assert.Contains("DestinationPortCode", problem.Errors.Keys);
        Assert.Contains("WeightKg", problem.Errors.Keys);
    }

    [Fact]
    public async Task Unknown_shipment_returns_404()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);

        var response = await staff.GetAsync("/api/shipments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Two_simultaneous_updates_never_both_win()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);
        var shipment = await staff.BookShipment();
        var request = new RecordEventRequest(ShipmentStatus.PickedUp, null, "Dock 4", null, null, null);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            staff.PostJson($"/api/shipments/{shipment.Id}/events", request)));

        // Exactly one request wins. The others either hit the rowversion check (409) or, if they
        // loaded the shipment after the winner committed, the lifecycle rule (422).
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r =>
            Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity }));

        var final = await (await staff.GetAsync($"/api/shipments/{shipment.Id}")).ReadAs<ShipmentDetailDto>();
        Assert.Single(final.Events, e => e.Status == ShipmentStatus.PickedUp);
    }

    [Fact]
    public async Task Revising_eta_is_recorded_in_the_history()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);
        var shipment = await staff.BookShipment();
        var newEta = DateTimeOffset.UtcNow.AddDays(20);

        var response = await staff.PutAsJsonAsync($"/api/shipments/{shipment.Id}/eta",
            new ReviseEtaRequest(newEta, "Port congestion"), ApiFactory.Json);

        var updated = await response.ReadAs<ShipmentDetailDto>();
        Assert.Equal(newEta.ToUnixTimeSeconds(), updated.EstimatedArrival.ToUnixTimeSeconds());
        Assert.Contains(updated.Events, e => e.Note!.Contains("Port congestion"));
    }
}
