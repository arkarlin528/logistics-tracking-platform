using System.Security.Cryptography;
using System.Text;

namespace Logistics.Api.Auth;

public sealed class RateImportOptions
{
    public const string Section = "RateImport";

    /// <summary>Shared secret the freight-data-pipeline sends in the X-Api-Key header.</summary>
    public string ApiKey { get; set; } = "";
}

/// <summary>Machine-to-machine auth for the rate import endpoint. Compared in constant time.</summary>
internal sealed class ApiKeyFilter(Microsoft.Extensions.Options.IOptions<RateImportOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = options.Value.ApiKey;
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(expected) || !FixedTimeEquals(expected, provided))
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Missing or invalid API key.");

        return await next(context);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
