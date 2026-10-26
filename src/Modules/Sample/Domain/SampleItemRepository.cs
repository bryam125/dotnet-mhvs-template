namespace MyApp.Modules.Sample.Domain;

// Puerto: el dominio define QUÉ necesita, no CÓMO se persiste.
public interface SampleItemRepository
{
    Task<SampleItem?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(SampleItem item, CancellationToken ct);
    Task UpdateAsync(SampleItem item, CancellationToken ct);
}
