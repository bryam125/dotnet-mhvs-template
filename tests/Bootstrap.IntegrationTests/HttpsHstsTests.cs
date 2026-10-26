using System.Net;
using System.Net.Http.Headers;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Cubre la topología documentada en ARCHITECTURE.md → "HTTPS y el proxy delante del contenedor":
/// <c>UseForwardedHeaders</c> confía en <c>X-Forwarded-Proto</c> para que la app sepa que la request
/// original llegó por HTTPS al proxy, aunque internamente le llegue en HTTP plano.
/// </summary>
/// <remarks>
/// Usa <see cref="ProductionBootstrapFactory"/> (entorno <c>Production</c>): en <c>Development</c>,
/// <c>Program.cs</c> no registra <c>UseHttpsRedirection</c>/<c>UseHsts</c> a propósito.
/// El <c>Host</c> de cada request se fija a un valor que no sea <c>localhost</c>: <c>HstsOptions</c>
/// excluye <c>localhost</c>/<c>127.0.0.1</c>/<c>[::1]</c> por defecto, y probar contra <c>localhost</c>
/// nunca mostraría el header aunque todo estuviera bien configurado (verificado a mano antes de
/// escribir este test: el mismo request sí trae el header con otro <c>Host</c>).
/// </remarks>
public class HttpsHstsTests(ProductionBootstrapFactory factory)
    : IClassFixture<ProductionBootstrapFactory>
{
    private const string NonLocalHost = "api.mhvs-blank-template.example";

    [Fact]
    public async Task ForwardedHttpsRequest_GetsHstsHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = NonLocalHost;
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("Strict-Transport-Security", out _),
            "Con X-Forwarded-Proto: https, la app debe tratar la request como HTTPS y agregar Strict-Transport-Security.");
    }

    [Fact]
    public async Task PlainHttpRequest_WithoutForwardedProto_DoesNotGetHstsHeader()
    {
        // Sin X-Forwarded-Proto, no hay proxy simulado delante: la app ve una conexión HTTP real, y
        // UseHsts solo agrega el header cuando Request.IsHttps.
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = NonLocalHost;

        var response = await factory.CreateClient().SendAsync(request);

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task ForwardedHttpsRequest_IsNotRedirected()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = NonLocalHost;
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        // Nada de 307/308 hacia https: la app ya considera la conexión HTTPS, UseHttpsRedirection no
        // tiene nada que redirigir.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
