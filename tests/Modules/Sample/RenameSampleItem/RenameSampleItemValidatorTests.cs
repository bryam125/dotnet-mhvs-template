using MyApp.Modules.Sample.Application.RenameSampleItem;
using Xunit;

namespace MyApp.Tests.Modules.Sample.RenameSampleItem;

// Estas reglas de entrada no trazan a un Criterio de Aceptación específico, así que
// van en inglés y sin prefijo CA0x_ (ver CONTRIBUTING.md → Código y estilo).
public class RenameSampleItemValidatorTests
{
    private readonly RenameSampleItemValidator validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_IsInvalid(string? name)
    {
        var result = validator.Validate(new RenameSampleItemRequest(name));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RenameSampleItemRequest.Name));
    }

    [Fact]
    public void Validate_NameLongerThan100Characters_IsInvalid()
    {
        var result = validator.Validate(new RenameSampleItemRequest(new string('a', 101)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NameOfExactly100Characters_IsValid()
    {
        var result = validator.Validate(new RenameSampleItemRequest(new string('a', 100)));

        Assert.True(result.IsValid);
    }
}
