using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace MyApp.Tests.Bootstrap.Support;

/// <summary>
/// La misma app real, pero en el entorno <c>Production</c>: <c>UseHttpsRedirection</c>/<c>UseHsts</c>
/// solo corren fuera de <c>Development</c> (ver <c>Program.cs</c> y ARCHITECTURE.md → "HTTPS y el
/// proxy delante del contenedor"), así que las pruebas de ese comportamiento necesitan este entorno.
/// Instancia separada de <see cref="BootstrapFactory"/>: coexiste con ella (y con
/// <see cref="RateLimitedBootstrapFactory"/>) gracias a que <c>log4net.config</c> usa
/// <c>MinimalLock</c> en el <c>RollingFileAppender</c> — con el <c>ExclusiveLock</c> por defecto,
/// dos instancias de <c>Program</c> en el mismo proceso de test escribiendo el mismo
/// <c>logs/app.log</c> físico (la ruta relativa del appender resuelve siempre al mismo directorio de
/// salida del ensamblado, sin importar <c>ContentRoot</c> ni el directorio de trabajo del proceso)
/// chocan con <c>log4net:ERROR ... Unable to acquire lock</c> al arrancar la segunda — verificado
/// reproduciendo el conflicto con dos <c>WebApplicationFactory&lt;Program&gt;</c> sin ninguna otra
/// configuración, antes de agregar <c>MinimalLock</c>. Sin nada que probar de logging, además se
/// quitan los proveedores (<c>ClearProviders()</c>, después de que <c>Program.cs</c> ya agregó
/// log4net) solo para no ensuciar la salida de la consola de test.
/// </summary>
public sealed class ProductionBootstrapFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}
