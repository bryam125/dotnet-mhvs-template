using MyApp.Modules.Tasks.Domain;

namespace MyApp.Tests.Modules.Tasks.Support;

/// <summary>
/// Doble del puerto para tests de Application/Domain: nunca toca MySQL.
/// <see cref="FailOnAdd"/> simula un fallo del almacenamiento al guardar.
/// </summary>
internal sealed class FakeTaskRepository : TaskRepository
{
    private readonly Dictionary<Guid, TaskItem> tasks = [];

    public bool FailOnAdd { get; set; }

    public IReadOnlyCollection<TaskItem> Stored => tasks.Values;

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(tasks.TryGetValue(id, out var task) ? task : null);

    public Task AddAsync(TaskItem task, CancellationToken ct)
    {
        if (FailOnAdd)
            throw new InvalidOperationException("Simulated storage failure.");

        tasks[task.Id] = task;
        return Task.CompletedTask;
    }
}
