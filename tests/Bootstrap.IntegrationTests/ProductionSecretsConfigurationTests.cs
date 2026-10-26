using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Tests.Bootstrap.Support;
using Xunit;
using Factory = MyApp.Tests.Bootstrap.Support.EnvironmentConfigBootstrapFactory;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Secretos en producción (hallazgo 7.1 del ROADMAP, ver config/README.md): llegan como variables de
/// entorno con "__" en lugar de ":", y ganan sobre el archivo local. Claves de prueba inventadas;
/// nunca se toca ni se lee el config/connectionstrings.local.json real.
/// </summary>
public class ProductionSecretsConfigurationTests(Factory factory) : IClassFixture<Factory>
{
    private IConfiguration Configuration => factory.Services.GetRequiredService<IConfiguration>();

    [Fact]
    public void EnvironmentVariable_WithDoubleUnderscore_ReachesConfigurationAsConnectionString()
    {
        Assert.Equal(Factory.EnvValue, Configuration.GetConnectionString(Factory.EnvOnlyKey));
        Assert.Equal(Factory.EnvValue, Configuration[$"ConnectionStrings:{Factory.EnvOnlyKey}"]);
    }

    [Fact]
    public void EnvironmentVariable_WinsOverLocalConfigFile()
    {
        // Precondición: el archivo local falso se cargó (LocalConfigLocator respeta el content root
        // de la factory). Sin esto, el test siguiente no probaría nada.
        Assert.Equal(Factory.FileValue, Configuration.GetConnectionString(Factory.FileOnlyKey));

        // La misma clave en el archivo local y en una variable de entorno: gana la variable, porque
        // Program.cs agrega AddEnvironmentVariables() después del archivo local.
        Assert.Equal(Factory.EnvValue, Configuration.GetConnectionString(Factory.BothKey));
    }
}
