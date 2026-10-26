using log4net;

namespace MyApp.Bootstrap.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.TraceIdentifier = correlationId;

        // Se escribe al iniciar la respuesta, no antes: ExceptionHandlerMiddleware limpia los headers de
        // la respuesta antes de invocar a GlobalExceptionHandler, así que un header puesto por adelantado
        // se perdía justo en las respuestas de error (400/404/500), cuando más falta hace.
        context.Response.OnStarting(static state =>
        {
            var (httpContext, id) = ((HttpContext, string))state;
            httpContext.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        }, (context, correlationId));

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        LogicalThreadContext.Properties["CorrelationId"] = correlationId;

        try
        {
            await next(context);
        }
        finally
        {
            LogicalThreadContext.Properties.Remove("CorrelationId");
        }
    }

    /// <summary>Longitud máxima aceptada del header entrante (un GUID con guiones mide 36).</summary>
    public const int MaxLength = 128;

    // Se usa el del cliente solo si tiene el formato de un GUID o de un id de traza: letras ASCII,
    // dígitos y guiones, hasta MaxLength. Cualquier otra cosa (demasiado largo, espacios, caracteres de
    // control, varios valores) se reemplaza por uno generado. Nunca se rechaza la request: es un
    // header de diagnóstico, no una validación de negocio. Evita que el cliente inyecte en los logs
    // (el valor va a %property{CorrelationId}) o haga devolver un header de miles de caracteres.
    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values)
            && values.Count == 1
            && IsAcceptable(values[0]))
        {
            return values[0]!;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsAcceptable(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaxLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
}
