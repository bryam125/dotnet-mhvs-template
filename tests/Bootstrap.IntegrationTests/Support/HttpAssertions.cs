using System.Text.Json;
using Xunit;

namespace MyApp.Tests.Bootstrap.Support;

internal static class HttpAssertions
{
    public const string CorrelationHeader = "X-Correlation-ID";

    /// <summary>Afirma que la respuesta trae X-Correlation-ID no vacío y lo devuelve.</summary>
    public static string AssertHasCorrelationId(HttpResponseMessage response)
    {
        Assert.True(
            response.Headers.TryGetValues(CorrelationHeader, out var values),
            $"La respuesta {(int)response.StatusCode} no trae el header {CorrelationHeader}.");

        var value = Assert.Single(values!);
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value;
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    public static StringContent Json(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");
}
