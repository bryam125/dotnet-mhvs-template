using MyApp.Modules.Sample.Domain;
using MyApp.Shared.Kernel;
using Xunit;

namespace MyApp.Tests.Modules.Sample.Domain;

// Invariantes de dominio: no trazan a un Criterio de Aceptación, por eso los nombres van en
// inglés y sin prefijo CA0x_. La excepción de idioma de CONTRIBUTING.md aplica solo a tests que
// trazan a un CA, no a los que trazan a una regla de negocio o invariante.
public class SampleItemTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ThrowsDomainException(string? name)
    {
        Assert.Throws<DomainException>(() => SampleItem.Create(name!));
    }

    [Fact]
    public void Create_TrimsSurroundingWhitespace()
    {
        var item = SampleItem.Create("  created  ");

        Assert.Equal("created", item.Name);
    }

    [Fact]
    public void Create_NameOf100Characters_IsValid()
    {
        var name = new string('a', 100);

        var item = SampleItem.Create(name);

        Assert.Equal(name, item.Name);
    }

    [Fact]
    public void Create_NameLongerThan100Characters_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => SampleItem.Create(new string('a', 101)));
    }

    [Fact]
    public void Rename_ReturnsNewInstance_AndLeavesOriginalUntouched()
    {
        var original = SampleItem.Create("original");

        var renamed = original.Rename("renamed");

        Assert.NotSame(original, renamed);
        Assert.Equal("original", original.Name);
        Assert.Equal("renamed", renamed.Name);
        Assert.Equal(original.Id, renamed.Id);
    }

    [Fact]
    public void Rename_TrimsSurroundingWhitespace()
    {
        var renamed = SampleItem.Create("original").Rename("  renamed  ");

        Assert.Equal("renamed", renamed.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_BlankName_ThrowsDomainException(string? newName)
    {
        var item = SampleItem.Create("original");

        Assert.Throws<DomainException>(() => item.Rename(newName!));
    }

    [Fact]
    public void Rename_NameLongerThan100Characters_ThrowsDomainException()
    {
        var item = SampleItem.Create("original");

        Assert.Throws<DomainException>(() => item.Rename(new string('a', 101)));
    }
}
