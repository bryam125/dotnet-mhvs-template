using System.Net;
using Microsoft.Extensions.Logging;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Plomería de <c>Bootstrap</c> que NO depende de ningún módulo de negocio: arranque, health check y
/// X-Correlation-ID. Contra la app real levantada en memoria (sin mocks), no contra los handlers.
/// Estos tests sobreviven a borrar el módulo <c>Sample</c>.
/// </summary>
[Collection(BootstrapCollection.Name)]
public class BootstrapTests(BootstrapFactory factory)
{
    [Fact]
    public void Startup_EmitsNoErrorLevelLogs()
    {
        Assert.True(
            factory.StartupErrorLogs.Count == 0,
            "La app registró logs de nivel Error al arrancar:" + Environment.NewLine +
            string.Join(Environment.NewLine, factory.StartupErrorLogs.Select(log => $"  [{log.Category}] {log.Message}")));
    }

    [Fact]
    public void Startup_EmitsNoLog4NetInternalErrors()
    {
        // p. ej. "log4net:ERROR [RollingFileAppender] ... Unable to acquire lock on file ...":
        // ocurre si log4net se configura dos veces sobre el mismo archivo y no pasa por ILogger.
        Assert.True(
            factory.StartupLog4NetErrors.Count == 0,
            "log4net reportó errores internos al arrancar:" + Environment.NewLine +
            string.Join(Environment.NewLine, factory.StartupLog4NetErrors.Select(message => $"  {message}")));
    }

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        HttpAssertions.AssertHasCorrelationId(response);
    }

    [Fact]
    public async Task ClientSuppliedCorrelationId_IsEchoedBack()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(HttpAssertions.CorrelationHeader, "test-correlation-123");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal("test-correlation-123", HttpAssertions.AssertHasCorrelationId(response));
    }

    // "TODAS las respuestas, éxito o error": también las que no pasan por el manejador de excepciones
    // (ruta inexistente, tipo de contenido no soportado). Las que sí pasan por él se cubren en
    // SampleEndpointsTests, porque necesitan un endpoint que reciba cuerpo JSON.
    [Fact]
    public async Task UnknownRoute_Returns404_WithCorrelationId()
    {
        var response = await factory.CreateClient().GetAsync("/no-such-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);
    }

    [Fact]
    public async Task EveryResponse_GetsAGeneratedCorrelationId_WhenTheClientSendsNone()
    {
        var client = factory.CreateClient();

        var first = HttpAssertions.AssertHasCorrelationId(await client.GetAsync("/health"));
        var second = HttpAssertions.AssertHasCorrelationId(await client.GetAsync("/health"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Harness_CapturesTheStartupInformationLog()
    {
        // Sanidad del propio arnés: el capturador sí ve los logs de la app (incluido el de arranque de
        // Program.cs), así que "0 errores" en los demás tests significa que no hubo errores, y no que
        // el capturador esté sordo.
        Assert.Contains(
            factory.StartupLogs,
            log => log.Level == LogLevel.Information && log.Message.StartsWith("Aplicación iniciada", StringComparison.Ordinal));
    }
}
