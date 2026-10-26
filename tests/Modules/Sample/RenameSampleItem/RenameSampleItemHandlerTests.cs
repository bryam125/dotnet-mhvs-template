using Microsoft.Extensions.Logging.Abstractions;
using MyApp.Modules.Sample.Application.RenameSampleItem;
using MyApp.Modules.Sample.Domain;
using MyApp.Shared.Kernel;
using MyApp.Tests.Modules.Sample.Support;
using Xunit;

namespace MyApp.Tests.Modules.Sample.RenameSampleItem;

// Sample no tiene una HU real: estos tres criterios de aceptación son ilustrativos del slice
// RenameSampleItem y existen para mostrar la convención de nombres CA0x_ (ver CONTRIBUTING.md):
//   CA-01  Renombrar un item existente guarda el nuevo nombre y lo devuelve.
//   CA-02  Renombrar un item inexistente (o con id mal formado) responde "no encontrado".
//   CA-03  Si el guardado falla, se comunica una falla de persistencia y el item original no cambia.
// Los nombres CA0x_ pueden ir en español porque trazan a un criterio de aceptación.
public class RenameSampleItemHandlerTests
{
    private readonly FakeSampleItemRepository repository = new();
    private readonly RenameSampleItemHandler handler;

    public RenameSampleItemHandlerTests()
    {
        handler = new RenameSampleItemHandler(repository, NullLogger<RenameSampleItemHandler>.Instance);
    }

    [Fact]
    public async Task CA01_RenombrarExitoso_GuardaElNuevoNombreYLoRetorna()
    {
        var item = SampleItem.Create("original");
        await repository.AddAsync(item, CancellationToken.None);

        var result = await handler.HandleAsync(new RenameSampleItemCommand(item.Id.ToString(), "nuevo"), CancellationToken.None);

        Assert.Equal(item.Id, result.Id);
        Assert.Equal("nuevo", result.Name);
        Assert.Equal("nuevo", (await repository.GetByIdAsync(item.Id, CancellationToken.None))!.Name);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("no-es-un-guid")]
    public async Task CA02_ItemInexistenteOIdMalFormado_LanzaNotFoundException(string sampleItemId)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new RenameSampleItemCommand(sampleItemId, "nuevo"), CancellationToken.None));
    }

    [Fact]
    public async Task CA03_FalloAlGuardar_LanzaPersistenceExceptionYNoModificaElItemAlmacenado()
    {
        var item = SampleItem.Create("original");
        await repository.AddAsync(item, CancellationToken.None);
        repository.FailOnUpdate = true;

        await Assert.ThrowsAsync<PersistenceException>(() =>
            handler.HandleAsync(new RenameSampleItemCommand(item.Id.ToString(), "nuevo"), CancellationToken.None));

        Assert.Equal("original", (await repository.GetByIdAsync(item.Id, CancellationToken.None))!.Name);
    }
}
