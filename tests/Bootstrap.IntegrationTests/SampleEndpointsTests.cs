using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Contrato HTTP de extremo a extremo, contra la app real, usando los endpoints del módulo de ejemplo
/// <c>Sample</c> porque son los únicos que reciben cuerpo JSON y pasan por el filtro de validación y por
/// <c>GlobalExceptionHandler</c>.
/// </summary>
/// <remarks>
/// ESTE ARCHIVO DEPENDE DE <c>Sample</c>: al borrar el módulo de ejemplo (ver README, "Cómo empezar tu
/// proyecto real"), bórralo o adáptalo a los endpoints de tu primer módulo real. Conserva lo que
/// verifica: qué formato de error sale de cada camino (ver ARCHITECTURE.md, "Contrato de errores").
/// <c>BootstrapTests</c> no depende de <c>Sample</c> y se queda.
/// </remarks>
[Collection(BootstrapCollection.Name)]
public class SampleEndpointsTests(BootstrapFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task PostSample_Valid_Returns200_AndCorrelationId()
    {
        var response = await client.PostAsync("/sample", HttpAssertions.Json("""{"name":"integration"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);

        using var body = await HttpAssertions.ReadJsonAsync(response);
        Assert.True(Guid.TryParse(body.RootElement.GetString(), out _), "Se esperaba el id (Guid) del item creado.");
    }

    [Fact]
    public async Task PostSample_EmptyName_Returns400_WithValidationFilterFormat_AndCorrelationId()
    {
        var response = await client.PostAsync("/sample", HttpAssertions.Json("""{"name":""}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);

        // Formato del filtro de validación (Results.ValidationProblem): RFC 9110, con "errors", SIN traceId.
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await HttpAssertions.ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Contains("rfc9110", root.GetProperty("type").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(root.TryGetProperty("errors", out var errors), "Falta el campo 'errors'.");
        Assert.True(errors.TryGetProperty("Name", out _), "Falta el error del campo 'Name'.");
        Assert.False(root.TryGetProperty("traceId", out _), "El formato del filtro de validación no lleva traceId.");
    }

    [Fact]
    public async Task PutRename_UnknownId_Returns404_AndCorrelationId()
    {
        var response = await client.PutAsync($"/sample/{Guid.NewGuid()}/name", HttpAssertions.Json("""{"name":"nuevo"}"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);

        // Formato de GlobalExceptionHandler: SÍ lleva traceId.
        using var body = await HttpAssertions.ReadJsonAsync(response);
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _), "El formato de GlobalExceptionHandler lleva traceId.");
    }

    [Fact]
    public async Task PutRename_MalformedId_Returns404_NotValidationError()
    {
        // Un identificador que no es un GUID equivale a "no encontrado" (404), no a un error de validación.
        var response = await client.PutAsync("/sample/no-es-un-guid/name", HttpAssertions.Json("""{"name":"nuevo"}"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);
    }

    [Fact]
    public async Task PutRename_ExistingItem_Returns200_WithNewName()
    {
        using var created = await HttpAssertions.ReadJsonAsync(
            await client.PostAsync("/sample", HttpAssertions.Json("""{"name":"antes"}""")));
        var id = created.RootElement.GetString();

        var response = await client.PutAsync($"/sample/{id}/name", HttpAssertions.Json("""{"name":"despues"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);
        using var body = await HttpAssertions.ReadJsonAsync(response);
        Assert.Equal("despues", body.RootElement.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("""{"name":""")]
    [InlineData("no es json")]
    public async Task PostSample_MalformedBody_Returns400_WithoutLeakingFrameworkMessage_AndCorrelationId(string malformed)
    {
        var response = await client.PostAsync("/sample", HttpAssertions.Json(malformed));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);

        var raw = await response.Content.ReadAsStringAsync();
        // No se expone el mensaje interno del framework ni del deserializador.
        Assert.DoesNotContain("Failed to read parameter", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Text.Json", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JsonException", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LineNumber", raw, StringComparison.OrdinalIgnoreCase);

        using var body = JsonDocument.Parse(raw);
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("La solicitud está mal formada o no incluye el cuerpo esperado.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task PostSample_NoBody_Returns400_AndCorrelationId()
    {
        var response = await client.PostAsync("/sample", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);
    }

    [Fact]
    public async Task UnsupportedContentType_Returns415_WithCorrelationId()
    {
        var response = await client.PostAsync("/sample", new StringContent("x", System.Text.Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);
    }

    [Fact]
    public async Task ClientErrors_DoNotProduceErrorLevelLogs()
    {
        // Un 400/404 causado por el cliente NO es un error del servidor: GlobalExceptionHandler lo registra
        // a propósito como Warning. Si además aparece un Error (p. ej. el "An unhandled exception has
        // occurred" que ASP.NET emite desde ExceptionHandlerMiddleware aunque la excepción sí se maneje),
        // cualquier alerta por nivel ERROR se dispara con un simple 404.
        factory.Logs.Clear();

        await client.PostAsync("/sample", HttpAssertions.Json("""{"name":""}"""));                                        // 400 validación
        await client.PostAsync("/sample", HttpAssertions.Json("""{"name":"""));                                            // 400 cuerpo mal formado
        await client.PutAsync($"/sample/{Guid.NewGuid()}/name", HttpAssertions.Json("""{"name":"x"}"""));                  // 404
        await client.PutAsync("/sample/no-es-un-guid/name", HttpAssertions.Json("""{"name":"x"}"""));                      // 404 id mal formado

        var errors = factory.Logs.Snapshot().Where(log => log.Level >= LogLevel.Error).ToList();
        Assert.True(
            errors.Count == 0,
            "Respuestas 4xx generaron logs de nivel Error:" + Environment.NewLine +
            string.Join(Environment.NewLine, errors.Select(log => $"  [{log.Category}] {log.Message}")));

        // Y el Warning intencional de GlobalExceptionHandler sigue presente (el silencio no debe ser total).
        Assert.Contains(
            factory.Logs.Snapshot(),
            log => log.Level == LogLevel.Warning && log.Category.EndsWith("GlobalExceptionHandler", StringComparison.Ordinal));
    }
}
