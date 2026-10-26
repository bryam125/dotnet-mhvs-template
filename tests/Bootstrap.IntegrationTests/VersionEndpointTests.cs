using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using MyApp.Bootstrap.Diagnostics;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// <c>GET /version</c>: expone la versión y el commit que el SDK embebe en el ensamblado de la app.
/// </summary>
[Collection(BootstrapCollection.Name)]
public class VersionEndpointTests(BootstrapFactory factory)
{
    [Fact]
    public async Task Version_Returns200_WithBuildInfoOfTheAppAssembly()
    {
        var response = await factory.CreateClient().GetAsync("/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        HttpAssertions.AssertHasCorrelationId(response);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        foreach (var key in new[] { "version", "commit", "commitFull", "buildConfiguration", "dotnetVersion" })
        {
            Assert.True(root.TryGetProperty(key, out _), $"Falta la clave \"{key}\" en el cuerpo de /version.");
        }

        // Este repo tiene historial de git, así que un build normal embebe el commit.
        var commitFull = root.GetProperty("commitFull").GetString();
        Assert.False(string.IsNullOrEmpty(commitFull), "commitFull es null: el build no embebió el commit de git.");
        Assert.Equal(commitFull[..12], root.GetProperty("commit").GetString());

        // Debe ser el commit del ensamblado de la APP, no el del host de pruebas (que es lo que
        // devolvería Assembly.GetEntryAssembly() bajo WebApplicationFactory).
        var appInformationalVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        Assert.EndsWith("+" + commitFull, appInformationalVersion);
    }

    [Fact]
    public void BuildInfo_WithoutInformationalVersion_ReturnsNullCommit_InsteadOfThrowing()
    {
        // Un ensamblado sin ningún atributo: el caso de un build sin información de git.
        var bareAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("SinInformacionDeGit") { Version = new Version(2, 3, 4) },
            AssemblyBuilderAccess.Run);

        var info = BuildInfo.From(bareAssembly);

        Assert.Equal("2.3.4", info.Version);
        Assert.Null(info.Commit);
        Assert.Null(info.CommitFull);
        Assert.Null(info.BuildConfiguration);
        Assert.False(string.IsNullOrEmpty(info.DotnetVersion));
    }
}
