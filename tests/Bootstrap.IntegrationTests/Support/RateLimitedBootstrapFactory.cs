using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyApp.Tests.Bootstrap.Support;

/// <summary>
/// La misma app real, con <c>RateLimiting:PermitLimit</c> bajado a un valor pequeño para poder
/// agotarlo en un test sin mandar cientos de requests. Instancia propia, exclusiva del test de rate
/// limiting: el limitador es estado en memoria del proceso de la app, así que compartir esta
/// instancia con otro test contaminaría el conteo de ambos.
/// </summary>
/// <remarks>
/// También quita los proveedores de logging, solo para no ensuciar la consola de test (ver
/// <see cref="ProductionBootstrapFactory"/> para por qué esta y otras instancias de <c>Program</c>
/// pueden coexistir en el mismo proceso sin pelear por <c>logs/app.log</c>: <c>MinimalLock</c> en
/// <c>log4net.config</c>, no esto).
/// </remarks>
public sealed class RateLimitedBootstrapFactory : WebApplicationFactory<Program>
{
    public const int PermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = PermitLimit.ToString(),
            ["RateLimiting:WindowSeconds"] = "60",
        }));
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}
