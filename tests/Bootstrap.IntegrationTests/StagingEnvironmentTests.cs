using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Entorno <c>Staging</c> (hallazgo 7.8 del ROADMAP, ver ARCHITECTURE.md → "Entornos"): se comporta como
/// Production (sin Swagger, con HTTPS/HSTS) y además carga <c>appsettings.Staging.json</c>.
/// </summary>
/// <remarks>
/// Igual que en <see cref="HttpsHstsTests"/>, las requests llevan un <c>Host</c> que no es
/// <c>localhost</c> (HSTS lo excluye por defecto) y <c>X-Forwarded-Proto: https</c>, para simular el
/// proxy que termina TLS delante del contenedor.
/// </remarks>
public class StagingEnvironmentTests(StagingBootstrapFactory staging, ProductionBootstrapFactory production)
    : IClassFixture<StagingBootstrapFactory>, IClassFixture<ProductionBootstrapFactory>
{
    private const string NonLocalHost = "api.mhvs-blank-template.example";
    private const string StagingSettingsFile = "appsettings.Staging.json";

    [Fact]
    public async Task Staging_Swagger_Returns404()
    {
        var response = await staging.CreateClient().SendAsync(ForwardedHttpsRequest("/swagger/index.html"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Staging_ForwardedHttpsRequest_GetsHstsHeader_LikeProduction()
    {
        var client = staging.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(ForwardedHttpsRequest("/health"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("Strict-Transport-Security", out _),
            "En Staging, igual que en Production, una request HTTPS (vía X-Forwarded-Proto) debe llevar Strict-Transport-Security.");
    }

    [Fact]
    public void Staging_LoadsAppSettingsStagingJson()
    {
        var provider = StagingSettingsProvider(staging);

        Assert.NotNull(provider);
        // CreateBuilder agrega appsettings.{Env}.json como opcional: el proveedor existiría aunque el
        // archivo no. Se comprueba también que el archivo existe en el content root.
        Assert.True(
            provider.Source.FileProvider!.GetFileInfo(provider.Source.Path!).Exists,
            $"El proveedor de {StagingSettingsFile} está registrado, pero el archivo no existe en el content root.");
    }

    [Fact]
    public void Production_DoesNotLoadAppSettingsStagingJson()
    {
        Assert.Null(StagingSettingsProvider(production));
    }

    private static JsonConfigurationProvider? StagingSettingsProvider(WebApplicationFactory<Program> factory)
    {
        var configuration = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();
        return configuration.Providers
            .OfType<JsonConfigurationProvider>()
            .SingleOrDefault(provider => provider.Source.Path == StagingSettingsFile);
    }

    private static HttpRequestMessage ForwardedHttpsRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = NonLocalHost;
        request.Headers.Add("X-Forwarded-Proto", "https");
        return request;
    }
}
