using Microsoft.Extensions.Logging.Abstractions;
using MyApp.Modules.Tasks.Application.CreateTask;
using MyApp.Modules.Tasks.Domain;
using MyApp.Shared.Kernel;
using MyApp.Tests.Modules.Tasks.Support;
using Xunit;

namespace MyApp.Tests.Modules.Tasks.CreateTask;

// Criterios de aceptación de HU-TASKS-001 — Crear una tarea, contra el handler con un doble del puerto.
// Cada caso de fallo verifica además que no se guardó ninguna tarea (regla 9).
public class CreateTaskHandlerTests
{
    private readonly FakeTaskRepository repository = new();
    private readonly FixedClock clock = new();
    private readonly CreateTaskHandler handler;

    public CreateTaskHandlerTests()
    {
        handler = new CreateTaskHandler(repository, clock, NullLogger<CreateTaskHandler>.Instance);
    }

    [Fact]
    public async Task CA01_CreacionExitosa_GuardaLaTareaPendienteYDevuelveSuResumen()
    {
        var dueDate = clock.TodayUtc.AddDays(7).ToString("yyyy-MM-dd");

        var result = await handler.HandleAsync(
            new CreateTaskCommand("  Comprar pan  ", "  Integral  ", dueDate), CancellationToken.None);

        var stored = Assert.Single(repository.Stored);
        Assert.NotEqual(Guid.Empty, stored.Id);
        Assert.Equal("Comprar pan", stored.Title);
        Assert.Equal("Integral", stored.Description);
        Assert.Equal(clock.TodayUtc.AddDays(7), stored.DueDate);
        Assert.Equal(TaskItemStatus.Pending, stored.Status);
        Assert.Equal(clock.UtcNow, stored.CreatedAt);
        Assert.Equal(stored.CreatedAt, stored.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);

        Assert.Equal(new CreateTaskResult(stored.Id, "Comprar pan", "PENDING", dueDate), result);
    }

    [Fact]
    public async Task CA01_CreacionExitosa_SoloConTitulo_NoIncluyeFechaLimiteEnElResumen()
    {
        var result = await handler.HandleAsync(new CreateTaskCommand("Comprar pan", "   ", null), CancellationToken.None);

        var stored = Assert.Single(repository.Stored);
        Assert.Null(stored.Description);
        Assert.Null(stored.DueDate);
        Assert.Null(result.DueDate);
        Assert.Equal("PENDING", result.Status);
    }

    [Fact]
    public async Task CA01_CreacionExitosa_FechaLimiteIgualAHoyUtc_SeAcepta()
    {
        var today = clock.TodayUtc.ToString("yyyy-MM-dd");

        var result = await handler.HandleAsync(new CreateTaskCommand("Comprar pan", null, today), CancellationToken.None);

        Assert.Equal(today, result.DueDate);
        Assert.Single(repository.Stored);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CA02_TituloAusenteVacioOSoloEspacios_InformaQueEsObligatorioYNoGuarda(string? title)
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreateTaskCommand(title, null, null), CancellationToken.None));

        Assert.Equal(TaskItemErrors.TitleRequired, exception.Message);
        Assert.Empty(repository.Stored);
    }

    [Theory]
    [InlineData(2, 0)]    // "aa"
    [InlineData(2, 3)]    // "   aa   ": 8 caracteres, pero 2 después de eliminar espacios exteriores
    [InlineData(121, 0)]
    public async Task CA03_TituloFueraDeLongitud_IndicaElIntervaloYNoGuarda(int length, int padding)
    {
        var spaces = new string(' ', padding);
        var title = spaces + new string('a', length) + spaces;

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreateTaskCommand(title, null, null), CancellationToken.None));

        Assert.Equal(TaskItemErrors.TitleLength, exception.Message);
        Assert.Empty(repository.Stored);
    }

    [Fact]
    public async Task CA04_DescripcionFueraDeLongitud_IndicaElLimiteYNoGuarda()
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreateTaskCommand("Comprar pan", new string('d', 2001), null), CancellationToken.None));

        Assert.Equal(TaskItemErrors.DescriptionTooLong, exception.Message);
        Assert.Empty(repository.Stored);
    }

    [Theory]
    [InlineData("2026-10-01T10:00:00")]
    [InlineData("2026-10-01 10:00")]
    [InlineData("01/10/2026")]
    [InlineData("2026-1-5")]
    [InlineData("2026-13-45")]
    [InlineData("2026-02-30")]
    [InlineData(" 2026-10-01")]
    [InlineData("")]
    public async Task CA05_FechaLimiteConFormatoInvalido_InformaElFormatoYNoGuarda(string dueDate)
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreateTaskCommand("Comprar pan", null, dueDate), CancellationToken.None));

        Assert.Equal(TaskItemErrors.DueDateInvalidFormat, exception.Message);
        Assert.Empty(repository.Stored);
    }

    [Fact]
    public async Task CA06_FechaLimiteEnElPasado_InformaQueNoPuedeEstarEnElPasadoYNoGuarda()
    {
        var yesterday = clock.TodayUtc.AddDays(-1).ToString("yyyy-MM-dd");

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new CreateTaskCommand("Comprar pan", null, yesterday), CancellationToken.None));

        Assert.Equal(TaskItemErrors.DueDateInPast, exception.Message);
        Assert.Empty(repository.Stored);
    }

    [Fact]
    public async Task CA07_ErrorDePersistencia_LanzaPersistenceExceptionSinDetalleTecnicoYNoGuarda()
    {
        repository.FailOnAdd = true;

        var exception = await Assert.ThrowsAsync<PersistenceException>(() =>
            handler.HandleAsync(new CreateTaskCommand("Comprar pan", null, null), CancellationToken.None));

        // El mensaje es el de la HU, no el de la excepción original del almacenamiento.
        Assert.Equal("La tarea no fue creada.", exception.Message);
        Assert.DoesNotContain("Simulated", exception.Message);
        Assert.Empty(repository.Stored);
    }
}
