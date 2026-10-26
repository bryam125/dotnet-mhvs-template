using Microsoft.Extensions.Logging;
using MyApp.Modules.Tasks.Domain;
using MyApp.Shared.Kernel;
using MyApp.Shared.Ports;

namespace MyApp.Modules.Tasks.Application.CreateTask;

/// <summary>
/// Implementa HU-TASKS-001 — Crear una tarea, en el orden del Flujo principal.
/// </summary>
public sealed class CreateTaskHandler(
    TaskRepository repository,
    IClock clock,
    ILogger<CreateTaskHandler> logger)
{
    public async Task<CreateTaskResult> HandleAsync(CreateTaskCommand command, CancellationToken ct)
    {
        // Pasos 2 y 3: el dominio normaliza, vuelve a aplicar las reglas y genera id, estado y fechas.
        // Una regla violada lanza DomainException antes de tocar el repositorio (regla 9).
        var task = TaskItem.Create(command.Title, command.Description, command.DueDate, clock.UtcNow);

        // Paso 4.
        try
        {
            await repository.AddAsync(task, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Este handler es quien registra la causa original: el manejador global no vuelve a hacerlo.
            logger.LogError(exception, "No se pudo guardar la tarea {TaskId}", task.Id);
            throw new PersistenceException("La tarea no fue creada.");
        }

        logger.LogInformation("Tarea {TaskId} creada", task.Id);

        // Paso 5.
        return CreateTaskResult.From(task);
    }
}
