namespace MyApp.Modules.Tasks.Domain;

/// <summary>
/// Estado de una tarea. HU-TASKS-001 solo define el estado inicial (<c>PENDING</c>); los demás
/// estados llegarán con la HU que permita cambiarlo.
/// </summary>
/// <remarks>Se llama <c>TaskItemStatus</c> para no chocar con <see cref="System.Threading.Tasks.TaskStatus"/>.</remarks>
public enum TaskItemStatus
{
    Pending
}

/// <summary>Código de negocio de cada estado, tal como lo nombra la HU (<c>PENDING</c>).</summary>
public static class TaskItemStatusCodes
{
    public const string Pending = "PENDING";

    public static string ToCode(this TaskItemStatus status) => status switch
    {
        TaskItemStatus.Pending => Pending,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Estado de tarea sin código definido.")
    };

    public static TaskItemStatus FromCode(string code) => code switch
    {
        Pending => TaskItemStatus.Pending,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Código de estado de tarea desconocido.")
    };
}
