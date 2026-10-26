using Microsoft.Extensions.Logging;
using MyApp.Modules.Sample.Domain;
using MyApp.Shared.Kernel;

namespace MyApp.Modules.Sample.Application.RenameSampleItem;

public sealed class RenameSampleItemHandler(
    SampleItemRepository repository,
    ILogger<RenameSampleItemHandler> logger)
{
    public async Task<RenameSampleItemResult> HandleAsync(RenameSampleItemCommand command, CancellationToken ct)
    {
        // El item debe existir. Un identificador mal formado equivale a "no encontrado".
        var item = Guid.TryParse(command.SampleItemId, out var itemId)
            ? await repository.GetByIdAsync(itemId, ct)
            : null;

        if (item is null)
            throw new NotFoundException("No se encontró un item con ese identificador.");

        // La validación de entrada corre en el borde HTTP (RenameSampleItemValidator);
        // el dominio la aplica de nuevo y devuelve una instancia nueva, sin mutar la original.
        var renamed = item.Rename(command.Name!);

        logger.LogInformation("Renombrando el item {SampleItemId} a {Name}", renamed.Id, renamed.Name);

        try
        {
            await repository.UpdateAsync(renamed, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Este handler es quien registra la causa original: el manejador global no vuelve a hacerlo.
            logger.LogError(exception, "No se pudo guardar el renombrado del item {SampleItemId}", renamed.Id);
            throw new PersistenceException("El renombrado no se realizó.");
        }

        return new RenameSampleItemResult(renamed.Id, renamed.Name);
    }
}
