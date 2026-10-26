using System.Globalization;
using System.Text.Json.Serialization;
using MyApp.Modules.Tasks.Domain;

namespace MyApp.Modules.Tasks.Application.CreateTask;

/// <summary>
/// Resumen que se devuelve al crear la tarea (HU-TASKS-001, Flujo principal paso 5): identificador,
/// título, estado y fecha límite solo si se proporcionó.
/// </summary>
public sealed record CreateTaskResult(
    Guid Id,
    string Title,
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DueDate)
{
    public static CreateTaskResult From(TaskItem task) => new(
        task.Id,
        task.Title,
        task.Status.ToCode(),
        task.DueDate?.ToString(TaskItem.DueDateFormat, CultureInfo.InvariantCulture));
}
