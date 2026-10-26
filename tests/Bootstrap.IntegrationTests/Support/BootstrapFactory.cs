using log4net.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MyApp.Tests.Bootstrap.Support;

/// <summary>
/// Levanta la app real (<c>Program</c>, sin mocks) en memoria y captura dos fuentes de errores:
/// lo que se registra por <see cref="ILogger"/> y los mensajes internos de log4net (por ejemplo el
/// <c>log4net:ERROR ... Unable to acquire lock</c> que aparece si el appender de archivo se configura
/// dos veces, que NO pasa por <see cref="ILogger"/>).
/// </summary>
/// <remarks>
/// La app se inicia en el constructor, con la suscripción a log4net ya hecha, para que
/// <see cref="StartupErrorLogs"/> y <see cref="StartupLog4NetErrors"/> reflejen solo el arranque.
/// Se comparte una única instancia entre todos los tests (ver <see cref="BootstrapCollection"/>) porque
/// el repositorio de log4net es global al proceso.
/// </remarks>
public sealed class BootstrapFactory : WebApplicationFactory<Program>
{
    private readonly object log4NetLock = new();
    private readonly List<string> log4NetMessages = [];

    public BootstrapFactory()
    {
        LogLog.LogReceived += OnLog4NetInternalMessage;

        // Forzar el arranque ahora (por defecto sería perezoso, al primer CreateClient).
        _ = Services;

        StartupLogs = Logs.Snapshot();
        StartupErrorLogs = StartupLogs.Where(log => log.Level >= LogLevel.Error).ToList();
        StartupLog4NetErrors = Log4NetInternalErrors();
    }

    public CapturingLoggerProvider Logs { get; } = new();

    /// <summary>Todo lo registrado por ILogger durante el arranque, de cualquier nivel.</summary>
    public IReadOnlyList<CapturedLog> StartupLogs { get; }

    /// <summary>Entradas de nivel Error o superior registradas por ILogger durante el arranque.</summary>
    public IReadOnlyList<CapturedLog> StartupErrorLogs { get; }

    /// <summary>Mensajes <c>log4net:ERROR</c> emitidos internamente por log4net durante el arranque.</summary>
    public IReadOnlyList<string> StartupLog4NetErrors { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(Logs));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            LogLog.LogReceived -= OnLog4NetInternalMessage;
        }

        base.Dispose(disposing);
    }

    private IReadOnlyList<string> Log4NetInternalErrors()
    {
        lock (log4NetLock)
        {
            return log4NetMessages.Where(message => message.StartsWith("log4net:ERROR", StringComparison.Ordinal)).ToList();
        }
    }

    private void OnLog4NetInternalMessage(object? sender, LogReceivedEventArgs e)
    {
        lock (log4NetLock)
        {
            log4NetMessages.Add($"{e.LogLog.Prefix}{e.LogLog.Message}");
        }
    }
}

/// <summary>Una sola app compartida por todos los tests de esta colección (se ejecutan en serie).</summary>
[CollectionDefinition(Name)]
public sealed class BootstrapCollection : ICollectionFixture<BootstrapFactory>
{
    public const string Name = "Bootstrap";
}
