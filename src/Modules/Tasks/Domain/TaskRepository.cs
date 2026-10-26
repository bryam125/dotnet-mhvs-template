namespace MyApp.Modules.Tasks.Domain;

// Puerto: el dominio define QUÉ necesita, no CÓMO se persiste. Sin tipos de ninguna tecnología
// (ni MySQL, ni ADO.NET): el adaptador concreto vive en Infrastructure/.
public interface TaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Guarda la tarea completa o nada (regla 9: sin escrituras parciales).</summary>
    Task AddAsync(TaskItem task, CancellationToken ct);
}
