using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace MyApp.Shared.Web.Validation;

public static class ValidationEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder WithCommandValidation<TCommand>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationEndpointFilter<TCommand>>();
}
