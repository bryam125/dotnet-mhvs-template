using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace MyApp.Tests.Bootstrap.Support;

/// <summary>
/// La misma app real, con un content root temporal FUERA del repo que trae un
/// <c>config/connectionstrings.local.json</c> falso (solo claves de prueba inventadas) y con variables
/// de entorno <c>ConnectionStrings__*</c> de prueba. Sirve para fijar cómo llegan los secretos de
/// producción a <c>IConfiguration</c> y quién gana cuando la misma clave está en los dos sitios.
/// </summary>
/// <remarks>
/// Las variables de entorno se leen al construir el host, así que se ponen en el constructor, antes
/// de que nada cree el host, y se restauran en <see cref="Dispose(bool)"/>, igual que la carpeta
/// temporal se borra ahí. El content root propio hace que <c>LocalConfigLocator</c> encuentre el
/// archivo falso y nunca el <c>config/connectionstrings.local.json</c> real del desarrollador.
/// El proyecto corre sin paralelismo (<c>AssemblyInfo.cs</c>), así que las variables no se filtran a
/// otro test mientras esta instancia vive.
/// </remarks>
public sealed class EnvironmentConfigBootstrapFactory : WebApplicationFactory<Program>
{
    /// <summary>Solo como variable de entorno: comprueba que "__" se traduce a ":".</summary>
    public const string EnvOnlyKey = "TmplEnvOnlyKey";

    /// <summary>Solo en el archivo local falso: comprueba que se cargó desde el content root temporal.</summary>
    public const string FileOnlyKey = "TmplFileOnlyKey";

    /// <summary>En los dos sitios, con valores distintos: comprueba quién gana.</summary>
    public const string BothKey = "TmplBothKey";

    public const string EnvValue = "valor-desde-variable-de-entorno";
    public const string FileValue = "valor-desde-archivo-local-falso";

    private readonly string contentRoot;
    private readonly Dictionary<string, string?> previousEnvironment = [];

    public EnvironmentConfigBootstrapFactory()
    {
        contentRoot = Path.Combine(Path.GetTempPath(), "mhvs-config-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(contentRoot, "config"));
        File.WriteAllText(
            Path.Combine(contentRoot, "config", "connectionstrings.local.json"),
            $$"""
            {
              "ConnectionStrings": {
                "{{FileOnlyKey}}": "{{FileValue}}",
                "{{BothKey}}": "{{FileValue}}"
              }
            }
            """);

        SetEnvironment($"ConnectionStrings__{EnvOnlyKey}", EnvValue);
        SetEnvironment($"ConnectionStrings__{BothKey}", EnvValue);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(contentRoot);
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            foreach (var (name, value) in previousEnvironment)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            try
            {
                Directory.Delete(contentRoot, recursive: true);
            }
            catch (IOException)
            {
                // Una carpeta en %TEMP% que no se pudo borrar no debe tumbar la suite.
            }
        }
    }

    private void SetEnvironment(string name, string value)
    {
        previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }
}
