using System.Net;
using Logistics.Application.Auth;
using Logistics.Application.Rates;
using Logistics.Application.Shipments;
using Logistics.Domain.Shipments;
using Microsoft.AspNetCore.SignalR.Client;

namespace Logistics.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RealtimeTests(ApiFactory api)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Staff_and_the_owning_customer_receive_live_updates()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);
        var customerClient = await api.ClientFor(ApiFactory.Users.Customer);
        var me = await (await customerClient.GetAsync("/api/auth/me")).ReadAs<UserDto>();
        var shipment = await staff.BookShipment(customerId: me.CustomerId!.Value);

        await using var staffHub = api.HubFor(await api.TokenFor(ApiFactory.Users.Dispatcher));
        await using var customerHub = api.HubFor(await api.TokenFor(ApiFactory.Users.Customer));
        var staffGot = Listen(staffHub, shipment.Id);
        var customerGot = Listen(customerHub, shipment.Id);
        await staffHub.StartAsync();
        await customerHub.StartAsync();

        await staff.PostJson($"/api/shipments/{shipment.Id}/events",
            new RecordEventRequest(ShipmentStatus.PickedUp, null, "Dock 1", 13.1, 100.9, null));

        var forStaff = await staffGot.Task.WaitAsync(Timeout);
        var forCustomer = await customerGot.Task.WaitAsync(Timeout);
        Assert.Equal(ShipmentStatus.PickedUp, forStaff.Status);
        Assert.Equal(13.1, forCustomer.CurrentLatitude);
        Assert.Equal(shipment.Reference, forCustomer.Reference);
    }

    [Fact]
    public async Task Other_customers_do_not_receive_the_update()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);
        var customerClient = await api.ClientFor(ApiFactory.Users.Customer);
        var me = await (await customerClient.GetAsync("/api/auth/me")).ReadAs<UserDto>();
        var someoneElses = await staff.BookShipment(customerId: me.CustomerId!.Value + 1);

        await using var customerHub = api.HubFor(await api.TokenFor(ApiFactory.Users.Customer));
        var customerGot = Listen(customerHub, someoneElses.Id);
        await customerHub.StartAsync();

        await staff.PostJson($"/api/shipments/{someoneElses.Id}/events",
            new RecordEventRequest(ShipmentStatus.PickedUp, null, null, null, null, null));

        var winner = await Task.WhenAny(customerGot.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.NotSame(customerGot.Task, winner);
    }

    [Fact]
    public async Task Hub_rejects_anonymous_connections()
    {
        await using var hub = api.HubFor(token: "");

        await Assert.ThrowsAnyAsync<Exception>(() => hub.StartAsync());
    }

    private static TaskCompletionSource<ShipmentUpdatedMessage> Listen(HubConnection hub, int shipmentId)
    {
        var received = new TaskCompletionSource<ShipmentUpdatedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<ShipmentUpdatedMessage>("ShipmentUpdated", message =>
        {
            if (message.ShipmentId == shipmentId)
                received.TrySetResult(message);
        });
        return received;
    }
}

[Collection(ApiCollection.Name)]
public class RateImportTests(ApiFactory api)
{
    private static RateImportRequest Batch(decimal amount) => new("freight-data-pipeline",
    [
        new RateImportRow("THLCH", "JPTYO", "MCL", "40hc", "usd", amount, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31)),
        new RateImportRow("NOPE1", "JPTYO", "MCL", "20GP", "USD", 100m, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31)),
        new RateImportRow("THLCH", "JPTYO", "MCL", "20GP", "USD", -1m, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31)),
    ]);

    [Fact]
    public async Task Import_requires_the_api_key()
    {
        var client = api.CreateClient();

        var response = await client.PostJson("/api/rates/import", Batch(900m));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Import_is_idempotent_and_reports_bad_rows()
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiFactory.RateApiKey);

        var first = await (await client.PostJson("/api/rates/import", Batch(900m))).ReadAs<RateImportResult>();
        var second = await (await client.PostJson("/api/rates/import", Batch(950m))).ReadAs<RateImportResult>();

        Assert.Equal((1, 0), (first.Inserted, first.Updated));
        Assert.Equal((0, 1), (second.Inserted, second.Updated));
        Assert.Equal([1, 2], second.Rejected.Select(r => r.Row));

        var reader = await api.ClientFor(ApiFactory.Users.Customer);
        var rates = await (await reader.GetAsync("/api/rates?origin=thlch&destination=JPTYO&on=2030-01-15"))
            .ReadAs<List<FreightRateDto>>();
        var rate = Assert.Single(rates);
        Assert.Equal(950m, rate.Amount);
        Assert.Equal("40HC", rate.ContainerType);
        Assert.Equal("USD", rate.Currency);
    }
}
