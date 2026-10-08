using System.Net.Http.Json;
using Logistics.Application.Shipments;

namespace Logistics.Api.IntegrationTests;

internal static class ApiTestHelpers
{
    public static async Task<T> ReadAs<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json);
        return body ?? throw new InvalidOperationException("Empty response body.");
    }

    public static Task<HttpResponseMessage> PostJson<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json);

    /// <summary>Each test books its own shipment so tests never depend on each other's data.</summary>
    public static async Task<ShipmentDetailDto> BookShipment(this HttpClient staffClient, int customerId = 1)
    {
        var response = await staffClient.PostJson("/api/shipments", new CreateShipmentRequest(
            customerId, CarrierId: 1, "THLCH", "SGSIN", "Integration test cargo", 1200m,
            DateTimeOffset.UtcNow.AddDays(10)));
        response.EnsureSuccessStatusCode();
        return await response.ReadAs<ShipmentDetailDto>();
    }
}
