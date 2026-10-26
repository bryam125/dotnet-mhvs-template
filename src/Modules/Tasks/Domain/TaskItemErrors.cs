namespace MyApp.Modules.Tasks.Domain;

/// <summary>
/// Mensajes de HU-TASKS-001 → "Errores que deben comunicarse". Los comparten el validador del borde
/// HTTP y el dominio, para que la persona reciba el mismo mensaje sin importar qué capa lo detecte.
/// </summary>
public static class TaskItemErrors
{
    public const string TitleRequired = "El título es obligatorio y debe contener texto válido.";
    public const string TitleLength = "El título debe tener entre 3 y 120 caracteres.";
    public const string DescriptionTooLong = "La descripción no puede superar los 2000 caracteres.";
    public const string DueDateInvalidFormat = "La fecha límite debe tener el formato AAAA-MM-DD.";
    public const string DueDateInPast = "La fecha límite no puede estar en el pasado.";
}
