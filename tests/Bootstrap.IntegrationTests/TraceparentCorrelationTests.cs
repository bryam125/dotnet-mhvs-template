using System.Net;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Convivencia de <c>X-Correlation-ID</c> y <c>traceparent</c> (hallazgo 7.2 del ROADMAP, ver
/// ARCHITECTURE.md → "Observabilidad"): hoy son independientes y el correlation ID manda en todo lo
/// visible. Solo se asierta lo observable desde fuera (headers); no el <c>Activity</c>, que nada expone.
/// </summary>
/// <remarks>
/// <c>BootstrapTests</c> ya cubre el eco de un id propio y el 404 con header sin <c>traceparent</c>; aquí
/// solo se agregan los casos en que interviene <c>traceparent</c>.
/// </remarks>
[Collection(BootstrapCollection.Name)]
public class TraceparentCorrelationTests(BootstrapFactory factory)
{
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string ValidTraceparent = $"00-{TraceId}-b7ad6b7169203331-01";
    private const string ClientCorrelationId = "soporte-caso-42";

    [Fact]
    public async Task ValidTraceparent_DoesNotFail_AndGetsGeneratedCorrelationId()
    {
        var response = await SendAsync(HttpMethod.Get, "/health", traceparent: ValidTraceparent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);
    }

    [Fact]
    public async Task ValidTraceparent_GeneratedCorrelationId_IsNotTheTraceId()
    {
        // Independencia de los dos identificadores. Si algún día se decide derivar uno del otro (por
        // ejemplo, usar el trace-id como correlation ID), este test debe actualizarse como DECISIÓN DE
        // DISEÑO documentada en ARCHITECTURE.md → "Observabilidad", no por accidente.
        var response = await SendAsync(HttpMethod.Get, "/health", traceparent: ValidTraceparent);

        var correlationId = HttpAssertions.AssertHasCorrelationId(response);
        Assert.DoesNotContain(TraceId, correlationId, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientCorrelationId_IsEchoedBack_WithAndWithoutTraceparent(bool withTraceparent)
    {
        var response = await SendAsync(
            HttpMethod.Get, "/health",
            traceparent: withTraceparent ? ValidTraceparent : null,
            correlationId: ClientCorrelationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ClientCorrelationId, HttpAssertions.AssertHasCorrelationId(response));
    }

    [Fact]
    public async Task UnknownRoute_404_KeepsCorrelationId_WithTraceparent()
    {
        // Solo el header: un 404 de ruta inexistente no pasa por GlobalExceptionHandler y sale sin cuerpo.
        var response = await SendAsync(HttpMethod.Get, "/no-existe", traceparent: ValidTraceparent);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);
    }

    [Fact]
    public async Task UnknownRoute_404_EchoesClientCorrelationId_WithTraceparent()
    {
        var response = await SendAsync(
            HttpMethod.Get, "/no-existe", traceparent: ValidTraceparent, correlationId: ClientCorrelationId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ClientCorrelationId, HttpAssertions.AssertHasCorrelationId(response));
    }

    [Theory]
    [InlineData("esto-no-es-w3c")]
    [InlineData("00-00000000000000000000000000000000-0000000000000000-01")] // trace-id todo ceros: inválido en W3C
    [InlineData("ff-zz-yy-xx")]
    public async Task InvalidTraceparent_DoesNotFail_NorBreakCorrelationId(string traceparent)
    {
        var generated = await SendAsync(HttpMethod.Get, "/health", traceparent: traceparent);
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        HttpAssertions.AssertHasCorrelationId(generated);

        var echoed = await SendAsync(HttpMethod.Get, "/health", traceparent: traceparent, correlationId: ClientCorrelationId);
        Assert.Equal(HttpStatusCode.OK, echoed.StatusCode);
        Assert.Equal(ClientCorrelationId, HttpAssertions.AssertHasCorrelationId(echoed));
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string? traceparent = null, string? correlationId = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (traceparent is not null)
        {
            // Sin validación del cliente: los casos inválidos deben llegar tal cual al servidor.
            request.Headers.TryAddWithoutValidation("traceparent", traceparent);
        }

        if (correlationId is not null)
        {
            request.Headers.Add(HttpAssertions.CorrelationHeader, correlationId);
        }

        return factory.CreateClient().SendAsync(request);
    }
}
