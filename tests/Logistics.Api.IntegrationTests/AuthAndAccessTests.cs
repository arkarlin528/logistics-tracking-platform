using System.Net;
using Logistics.Application.Auth;
using Logistics.Application.Common;
using Logistics.Application.Shipments;
using Logistics.Domain.Users;

namespace Logistics.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthAndAccessTests(ApiFactory api)
{
    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var response = await api.CreateClient().PostJson("/api/auth/login",
            new LoginRequest(ApiFactory.Users.Dispatcher, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Requests_without_a_token_are_rejected()
    {
        var response = await api.CreateClient().GetAsync("/api/shipments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var client = await api.ClientFor(ApiFactory.Users.Customer);

        var me = await (await client.GetAsync("/api/auth/me")).ReadAs<UserDto>();

        Assert.Equal(UserRole.Customer, me.Role);
        Assert.NotNull(me.CustomerId);
    }

    [Fact]
    public async Task Customer_only_sees_their_own_shipments()
    {
        var customer = await api.ClientFor(ApiFactory.Users.Customer);
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);

        var own = await (await customer.GetAsync("/api/shipments?pageSize=100")).ReadAs<PagedResult<ShipmentSummaryDto>>();
        var all = await (await staff.GetAsync("/api/shipments?pageSize=100")).ReadAs<PagedResult<ShipmentSummaryDto>>();

        Assert.NotEmpty(own.Items);
        Assert.Single(own.Items.Select(s => s.CustomerName).Distinct());
        Assert.True(all.TotalCount > own.TotalCount);
    }

    [Fact]
    public async Task Customer_gets_404_for_another_customers_shipment()
    {
        var staff = await api.ClientFor(ApiFactory.Users.Dispatcher);
        var customer = await api.ClientFor(ApiFactory.Users.Customer);
        var me = await (await customer.GetAsync("/api/auth/me")).ReadAs<UserDto>();
        var someoneElses = await staff.BookShipment(customerId: me.CustomerId!.Value + 1);

        var byId = await customer.GetAsync($"/api/shipments/{someoneElses.Id}");
        var byReference = await customer.GetAsync($"/api/shipments/by-reference/{someoneElses.Reference}");

        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byReference.StatusCode);
    }

    [Theory]
    [InlineData("/api/dashboard")]
    [InlineData("/api/reference/customers")]
    public async Task Staff_only_reads_are_forbidden_for_customers(string url)
    {
        var customer = await api.ClientFor(ApiFactory.Users.Customer);

        var response = await customer.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_book_shipments()
    {
        var customer = await api.ClientFor(ApiFactory.Users.Customer);

        var response = await customer.PostJson("/api/shipments", new CreateShipmentRequest(
            1, 1, "THLCH", "SGSIN", "x", 1m, DateTimeOffset.UtcNow.AddDays(3)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
