using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyApp.Shared.Kernel;

namespace MyApp.Bootstrap.ExceptionHandling;

/// <summary>
/// Traduce las excepciones no controladas a respuestas <see cref="ProblemDetails"/> consistentes.
/// El orden del switch importa: <see cref="NotFoundException"/> y <see cref="PersistenceException"/>
/// se evalúan antes que <see cref="DomainException"/> para no caer en el 400 genérico.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            NotFoundException notFoundException => new ProblemDetails
            {
                Title = "Resource not found",
                Detail = notFoundException.Message,
                Status = StatusCodes.Status404NotFound,
                Type = "https://httpstatuses.com/404"
            },
            ValidationException validationException => new ValidationProblemDetails(
                validationException.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            },
            // Cuerpo ausente o mal formado: no se expone el mensaje interno del framework.
            BadHttpRequestException badRequestException => new ProblemDetails
            {
                Title = "Bad request",
                Detail = "La solicitud está mal formada o no incluye el cuerpo esperado.",
                Status = badRequestException.StatusCode,
                Type = "https://httpstatuses.com/400"
            },
            PersistenceException persistenceException => new ProblemDetails
            {
                Title = "Persistence failure",
                Detail = persistenceException.Message,
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://httpstatuses.com/500"
            },
            DomainException domainException => new ProblemDetails
            {
                Title = "Business rule violation",
                Detail = domainException.Message,
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            },
            _ => new ProblemDetails
            {
                Title = "Unexpected error",
                Detail = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://httpstatuses.com/500"
            }
        };

        // PersistenceException ya fue registrada por el handler que la lanzó, con el id de la entidad
        // y la excepción original; registrarla de nuevo aquí solo duplicaría el error sin más contexto.
        if (exception is not PersistenceException)
        {
            if (problem.Status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Unhandled exception while processing request {Path}", httpContext.Request.Path);
            }
            else
            {
                logger.LogWarning(exception, "Handled exception while processing request {Path}", httpContext.Request.Path);
            }
        }

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        // Se serializa por el tipo runtime para no perder el campo "errors" de ValidationProblemDetails.
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), cancellationToken);
        return true;
    }
}
