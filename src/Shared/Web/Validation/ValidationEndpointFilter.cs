using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace MyApp.Shared.Web.Validation;

/// <summary>
/// Valida en el borde HTTP el argumento de tipo <typeparamref name="TCommand"/> con su
/// <c>IValidator&lt;TCommand&gt;</c>, antes de que el handler lo reciba.
/// Ante error responde SIEMPRE con <c>Results.ValidationProblem</c> (400, tipo RFC 9110, campo
/// <c>errors</c>, sin <c>traceId</c>), sin importar qué endpoint lo use, para que todos los slices
/// produzcan el mismo cuerpo. <typeparamref name="TCommand"/> debe ser el tipo que realmente llega
/// como argumento del endpoint (el cuerpo), no el comando interno que arma el handler.
/// </summary>
public sealed class ValidationEndpointFilter<TCommand> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService(typeof(IValidator<TCommand>)) as IValidator<TCommand>;
        if (validator is null)
        {
            return await next(context);
        }

        var command = context.Arguments.OfType<TCommand>().FirstOrDefault();
        if (command is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(command, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
    }
}
