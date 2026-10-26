using MyApp.Modules.Tasks.Application.CreateTask;
using MyApp.Modules.Tasks.Domain;
using MyApp.Tests.Modules.Tasks.Support;
using Xunit;

namespace MyApp.Tests.Modules.Tasks.CreateTask;

// El validador es quien responde primero en el borde HTTP (400 con "errors"): estos tests verifican que
// cada CA de fallo de HU-TASKS-001 se rechaza ahí con el mensaje de la HU. Los límites exactos (3, 120,
// 2000) no trazan a un CA concreto, por eso van en inglés y sin prefijo.
public class CreateTaskValidatorTests
{
    private readonly FixedClock clock = new();
    private readonly CreateTaskValidator validator;

    public CreateTaskValidatorTests()
    {
        validator = new CreateTaskValidator(clock);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CA02_TituloAusenteVacioOSoloEspacios_SeRechazaConMensajeDeObligatorio(string? title)
    {
        AssertSingleError(new CreateTaskCommand(title, null, null), nameof(CreateTaskCommand.Title), TaskItemErrors.TitleRequired);
    }

    [Theory]
    [InlineData("aa")]
    [InlineData("   aa   ")]
    public void CA03_TituloMuyCorto_SeRechazaConElIntervalo(string title)
    {
        AssertSingleError(new CreateTaskCommand(title, null, null), nameof(CreateTaskCommand.Title), TaskItemErrors.TitleLength);
    }

    [Fact]
    public void CA03_TituloMuyLargo_SeRechazaConElIntervalo()
    {
        AssertSingleError(new CreateTaskCommand(new string('a', 121), null, null), nameof(CreateTaskCommand.Title), TaskItemErrors.TitleLength);
    }

    [Fact]
    public void CA04_DescripcionFueraDeLongitud_SeRechazaConElLimite()
    {
        AssertSingleError(new CreateTaskCommand("Comprar pan", new string('d', 2001), null), nameof(CreateTaskCommand.Description), TaskItemErrors.DescriptionTooLong);
    }

    [Theory]
    [InlineData("2026-10-01T10:00:00")]
    [InlineData("01/10/2026")]
    [InlineData("2026-02-30")]
    [InlineData("")]
    public void CA05_FechaLimiteConFormatoInvalido_SeRechazaConElFormato(string dueDate)
    {
        AssertSingleError(new CreateTaskCommand("Comprar pan", null, dueDate), nameof(CreateTaskCommand.DueDate), TaskItemErrors.DueDateInvalidFormat);
    }

    [Fact]
    public void CA06_FechaLimiteEnElPasado_SeRechaza()
    {
        var yesterday = clock.TodayUtc.AddDays(-1).ToString("yyyy-MM-dd");

        AssertSingleError(new CreateTaskCommand("Comprar pan", null, yesterday), nameof(CreateTaskCommand.DueDate), TaskItemErrors.DueDateInPast);
    }

    [Fact]
    public void Validate_BoundaryValues_AreValid()
    {
        var command = new CreateTaskCommand(
            "  " + new string('a', 120) + "  ",
            "  " + new string('d', 2000) + "  ",
            clock.TodayUtc.ToString("yyyy-MM-dd"));

        Assert.True(validator.Validate(command).IsValid);
    }

    [Fact]
    public void Validate_MinimumTitleAndNoOptionalFields_IsValid()
    {
        Assert.True(validator.Validate(new CreateTaskCommand("abc", null, null)).IsValid);
    }

    [Fact]
    public void Validate_WhitespaceOnlyDescription_IsTreatedAsAbsent()
    {
        Assert.True(validator.Validate(new CreateTaskCommand("abc", new string(' ', 3000), null)).IsValid);
    }

    private void AssertSingleError(CreateTaskCommand command, string propertyName, string message)
    {
        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(propertyName, error.PropertyName);
        Assert.Equal(message, error.ErrorMessage);
    }
}
