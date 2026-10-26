using System.Reflection;
using System.Runtime.InteropServices;

namespace MyApp.Bootstrap.Diagnostics;

/// <summary>
/// Versión y commit del build que está corriendo, tal como los embebe el SDK en el ensamblado.
/// Cuerpo de <c>GET /version</c>.
/// </summary>
public sealed record BuildInfo(
    string Version,
    string? Commit,
    string? CommitFull,
    string? BuildConfiguration,
    string DotnetVersion)
{
    private const int ShortCommitLength = 12;

    /// <summary>
    /// Lee <see cref="AssemblyInformationalVersionAttribute"/> ("1.0.0+{hash}") del ensamblado de la app.
    /// Se usa <c>typeof(Program).Assembly</c> y no <c>Assembly.GetEntryAssembly()</c>: bajo
    /// <c>WebApplicationFactory</c> (tests) el ensamblado de entrada es el host de pruebas, no la app,
    /// y se reportaría la versión de otro producto.
    /// Nunca lanza: sin atributo o sin "+hash" (build sin información de git, p. ej. una imagen
    /// Docker construida sin <c>.git</c>) devuelve <c>Commit</c>/<c>CommitFull</c> en <c>null</c>.
    /// </summary>
    public static BuildInfo From(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        var version = assembly.GetName().Version?.ToString(3) ?? "desconocida";
        string? commitFull = null;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var separator = informationalVersion.IndexOf('+');
            version = separator >= 0 ? informationalVersion[..separator] : informationalVersion;
            if (separator >= 0 && separator < informationalVersion.Length - 1)
            {
                commitFull = informationalVersion[(separator + 1)..];
            }
        }

        var commit = commitFull is null
            ? null
            : commitFull[..Math.Min(ShortCommitLength, commitFull.Length)];

        return new BuildInfo(
            version,
            commit,
            commitFull,
            assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            RuntimeInformation.FrameworkDescription);
    }
}
