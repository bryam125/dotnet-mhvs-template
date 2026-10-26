using System.Collections.Concurrent;
using MyApp.Modules.Sample.Domain;

namespace MyApp.Modules.Sample.Infrastructure;

// Adaptador de ejemplo: en memoria, para que el skeleton corra sin BD
// configurada. En un módulo real esto sería EF Core, Dapper, Mongo, etc.
public class InMemorySampleItemRepository : SampleItemRepository
{
    private static readonly ConcurrentDictionary<Guid, SampleItem> Store = new();

    public Task<SampleItem?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Store.TryGetValue(id, out var item) ? item : null);

    public Task AddAsync(SampleItem item, CancellationToken ct)
    {
        Store[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SampleItem item, CancellationToken ct)
    {
        Store[item.Id] = item;
        return Task.CompletedTask;
    }
}
