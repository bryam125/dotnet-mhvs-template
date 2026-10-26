using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace MyApp.Tests.Bootstrap.Support;

/// <summary>
/// La misma app real, pero en el entorno <c>Staging</c> (ver ARCHITECTURE.md → "Entornos"): Staging se
/// comporta como Production salvo lo que configure <c>appsettings.Staging.json</c>. Mismo patrón que
/// <see cref="ProductionBootstrapFactory"/>, incluido el <c>ClearProviders()</c> para no ensuciar la
/// salida de la consola de test.
/// </summary>
public sealed class StagingBootstrapFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Staging");
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}
