using Microsoft.Extensions.Logging;
using MyApp.Modules.Sample.Domain;
using MyApp.Shared.Kernel;

namespace MyApp.Modules.Sample.Application.PingSample;

public class PingSampleHandler(SampleItemRepository repository, ILogger<PingSampleHandler> logger)
{
    public async Task<Guid> HandleAsync(PingSampleCommand command, CancellationToken ct)
    {
        logger.LogInformation("Creando SampleItem con nombre {Name}", command.Name);

        var item = SampleItem.Create(command.Name);

        try
        {
            await repository.AddAsync(item, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Este handler es quien registra la causa original: el manejador global no vuelve a hacerlo.
            logger.LogError(exception, "No se pudo guardar el item {SampleItemId}", item.Id);
            throw new PersistenceException("El item no fue creado.");
        }

        return item.Id;
    }
}
