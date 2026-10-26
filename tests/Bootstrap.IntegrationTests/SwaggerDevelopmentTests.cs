using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Swagger solo está activo en <c>Development</c> (ver ARCHITECTURE.md → "Entornos"). Usa
/// <see cref="BootstrapFactory"/>, que no llama a <c>UseEnvironment</c>: corre en Development porque
/// <c>WebApplicationFactory&lt;TEntryPoint&gt;</c> fija ese entorno por defecto. El primer test lo
/// comprueba explícitamente, para que este archivo no dependa de un supuesto.
/// </summary>
[Collection(BootstrapCollection.Name)]
public class SwaggerDevelopmentTests(BootstrapFactory factory)
{
    [Fact]
    public void BootstrapFactory_RunsInDevelopment()
    {
        var environment = factory.Services.GetRequiredService<IWebHostEnvironment>();

        Assert.Equal("Development", environment.EnvironmentName);
    }

    [Fact]
    public async Task Development_Swagger_Returns200()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
