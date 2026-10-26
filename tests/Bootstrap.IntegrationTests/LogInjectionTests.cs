using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using log4net;
using log4net.Appender;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Hallazgo 4.4 del ROADMAP: un valor de entrada no puede falsificar líneas de log, y el
/// <c>X-Correlation-ID</c> del cliente no se refleja si no tiene un formato razonable.
/// Se verifica contra el archivo de log real que escribe log4net, no contra lo que ve <c>ILogger</c>
/// (que recibe el mensaje sin sanear: el saneamiento vive en el layout de log4net).
/// </summary>
[Collection(BootstrapCollection.Name)]
public class LogInjectionTests(BootstrapFactory factory)
{
    [Fact]
    public async Task InputWithNewline_DoesNotForgeASeparateLogLine()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        var forgedLine = $"ERROR [admin] Security - login ok {marker}";

        // PingSampleHandler registra el nombre recibido ("Creando SampleItem con nombre {Name}").
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/sample", new { name = $"prueba-{marker}\n{forgedLine}" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var lines = ReadLogFileLines();

        // Ninguna línea del archivo EMPIEZA con el texto inyectado: no hay un evento falso.
        Assert.DoesNotContain(lines, line => line.StartsWith(forgedLine, StringComparison.Ordinal));

        // El texto inyectado sigue ahí (no se pierde información), pero en la misma línea que el
        // evento real, detrás de un "\n" literal y visible.
        var realLine = Assert.Single(lines, line => line.Contains($"prueba-{marker}", StringComparison.Ordinal));
        Assert.Contains($"prueba-{marker}\\n{forgedLine}", realLine, StringComparison.Ordinal);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3} \[", realLine);
    }

    [Fact]
    public async Task OversizedCorrelationId_IsReplacedByAServerGeneratedOne()
    {
        var oversized = new string('a', 5000);
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(HttpAssertions.CorrelationHeader, oversized);

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = HttpAssertions.AssertHasCorrelationId(response);
        Assert.NotEqual(oversized, returned);
        Assert.Matches(new Regex("^[0-9a-f]{32}$"), returned); // Guid.NewGuid().ToString("N")
    }

    private static string[] ReadLogFileLines()
    {
        var logFile = LogManager.GetAllRepositories()
            .SelectMany(repository => repository.GetAppenders())
            .OfType<RollingFileAppender>()
            .Select(appender => appender.File)
            .First(path => !string.IsNullOrEmpty(path));

        // MinimalLock: log4net abre y cierra el archivo en cada escritura, así que se puede leer ya.
        using var stream = new FileStream(logFile!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split(["\r\n", "\n"], StringSplitOptions.None);
    }
}
