using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Modules.Sample.Application.PingSample;
using MyApp.Modules.Sample.Application.RenameSampleItem;
using MyApp.Modules.Sample.Domain;
using MyApp.Modules.Sample.Infrastructure;
using MyApp.Shared.Web.Validation;

namespace MyApp.Modules.Sample.Application;

public static class SampleModule
{
    public static IServiceCollection AddSampleModule(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<PingSampleCommand>();
        services.AddScoped<SampleItemRepository, InMemorySampleItemRepository>();
        services.AddScoped<PingSampleHandler>();
        services.AddScoped<RenameSampleItemHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapSampleModule(this IEndpointRouteBuilder app)
    {
        // .AllowAnonymous() explícito: la convención (ver ARCHITECTURE.md → "Autenticación y
        // autorización") es que todo endpoint declare .RequireAuthorization() o .AllowAnonymous()
        // sin excepción. Sample no necesita auth, pero lo declara igual, como ejemplo del patrón.
        app.MapPost("/sample", async (PingSampleCommand cmd, PingSampleHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(cmd, ct)))
           .WithCommandValidation<PingSampleCommand>()
           .WithTags("Sample")
           .AllowAnonymous();

        app.MapPut("/sample/{sampleItemId}/name", async (string sampleItemId, RenameSampleItemRequest request, RenameSampleItemHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new RenameSampleItemCommand(sampleItemId, request.Name), ct)))
           .WithCommandValidation<RenameSampleItemRequest>()
           .WithTags("Sample")
           .AllowAnonymous();

        return app;
    }
}
