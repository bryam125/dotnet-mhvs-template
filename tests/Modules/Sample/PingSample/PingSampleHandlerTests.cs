using Microsoft.Extensions.Logging.Abstractions;
using MyApp.Modules.Sample.Application.PingSample;
using MyApp.Shared.Kernel;
using MyApp.Tests.Modules.Sample.Support;
using Xunit;

namespace MyApp.Tests.Modules.Sample.PingSample;

public class PingSampleHandlerTests
{
    [Fact]
    public async Task HandleAsync_AddsSampleItemAndReturnsItsId()
    {
        var repository = new FakeSampleItemRepository();
        var handler = new PingSampleHandler(repository, NullLogger<PingSampleHandler>.Instance);

        var id = await handler.HandleAsync(new PingSampleCommand("test"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(await repository.GetByIdAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_StorageFailure_ThrowsPersistenceException_AndStoresNothing()
    {
        var repository = new FakeSampleItemRepository { FailOnAdd = true };
        var handler = new PingSampleHandler(repository, NullLogger<PingSampleHandler>.Instance);

        var exception = await Assert.ThrowsAsync<PersistenceException>(() =>
            handler.HandleAsync(new PingSampleCommand("test"), CancellationToken.None));

        // 500, no 400: un fallo de guardado no es una regla de negocio violada por el cliente.
        Assert.IsNotAssignableFrom<DomainException>(exception);
        Assert.Equal(0, repository.Count);
    }
}
