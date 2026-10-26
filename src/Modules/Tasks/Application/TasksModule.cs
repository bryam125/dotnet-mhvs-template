using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyApp.Modules.Tasks.Application.CreateTask;
using MyApp.Modules.Tasks.Domain;
using MyApp.Modules.Tasks.Infrastructure;
using MyApp.Shared.Ports;
using MyApp.Shared.Web.Validation;

namespace MyApp.Modules.Tasks.Application;

public static class TasksModule
{
    public static IServiceCollection AddTasksModule(this IServiceCollection services)
    {
        // TryAdd: IClock es un puerto de Shared; si otro módulo ya lo registró, no se duplica.
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddValidatorsFromAssemblyContaining<CreateTaskCommand>();
        // Lee ConnectionStrings:MysqlTodo de IConfiguration en cada operación, no al registrarse:
        // la app arranca aunque falte la cadena, y un cambio en el archivo local se toma en caliente.
        services.AddScoped<TaskRepository, MySqlTaskRepository>();
        services.AddScoped<CreateTaskHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapTasksModule(this IEndpointRouteBuilder app)
    {
        // .AllowAnonymous() explícito (convención de ARCHITECTURE.md → "Autenticación y autorización"):
        // la plantilla todavía no configura ningún esquema de autenticación.
        app.MapPost("/tasks", async (CreateTaskCommand command, CreateTaskHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(command, ct)))
           .WithCommandValidation<CreateTaskCommand>()
           .WithTags("Tasks")
           .AllowAnonymous();

        return app;
    }
}
