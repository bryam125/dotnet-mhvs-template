using MyApp.Modules.Sample.Domain;

namespace MyApp.Tests.Modules.Sample.Support;

/// <summary>
/// Doble del puerto para tests de Application/Domain: nunca toca infraestructura real.
/// <see cref="FailOnAdd"/> y <see cref="FailOnUpdate"/> simulan un fallo del almacenamiento al crear
/// o al guardar un cambio.
/// </summary>
internal sealed class FakeSampleItemRepository : SampleItemRepository
{
    private readonly Dictionary<Guid, SampleItem> items = [];

    public bool FailOnAdd { get; set; }

    public bool FailOnUpdate { get; set; }

    public int Count => items.Count;

    public Task<SampleItem?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(items.TryGetValue(id, out var item) ? item : null);

    public Task AddAsync(SampleItem item, CancellationToken ct)
    {
        if (FailOnAdd)
            throw new InvalidOperationException("Simulated storage failure.");

        items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SampleItem item, CancellationToken ct)
    {
        if (FailOnUpdate)
            throw new InvalidOperationException("Simulated storage failure.");

        items[item.Id] = item;
        return Task.CompletedTask;
    }
}
