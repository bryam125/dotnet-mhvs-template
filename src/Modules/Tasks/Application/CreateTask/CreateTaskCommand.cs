namespace MyApp.Modules.Tasks.Application.CreateTask;

/// <summary>
/// Implementa HU-TASKS-001 — Crear una tarea (Datos de entrada).
/// </summary>
/// <remarks>
/// <c>DueDate</c> llega como texto, no como <see cref="DateOnly"/>: la regla 4 exige rechazar un valor
/// con hora con el mensaje de formato de la HU, y un <see cref="DateOnly"/> haría fallar la
/// deserialización antes, con un 400 genérico. En JSON es <c>dueDate</c> (camelCase por defecto, como
/// el resto de la API); la columna <c>due_date</c> es un detalle del adaptador MySQL.
/// </remarks>
public sealed record CreateTaskCommand(
    string? Title,
    string? Description,
    string? DueDate);
