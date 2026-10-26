using MyApp.Shared.Kernel;

namespace MyApp.Modules.Sample.Domain;

// ELIMINA este módulo completo cuando empieces el proyecto real.
// Está aquí solo como ejemplo vivo de la convención MHVS.
public class SampleItem
{
    private const int MaxNameLength = 100;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;

    private SampleItem() { }

    public static SampleItem Create(string name)
    {
        return new SampleItem { Id = Guid.NewGuid(), Name = NormalizeName(name) };
    }

    /// <summary>
    /// Devuelve una copia del item con el nombre reemplazado. La instancia original no se modifica,
    /// de modo que un fallo posterior al persistir deja intacto el objeto en memoria y el almacenado.
    /// </summary>
    public SampleItem Rename(string newName)
    {
        var normalizedName = NormalizeName(newName);

        // ADVERTENCIA: MemberwiseClone hace una copia SUPERFICIAL (shallow copy).
        // Es seguro solo mientras la entidad tenga campos de valor o inmutables
        // (string, Guid, DateTime, enum...). Si la entidad gana un campo de
        // colección (List<T>, Dictionary<,>...) u otro objeto mutable, la copia y
        // el original lo compartirían por referencia: reemplaza esto por una copia
        // profunda explícita de ese campo. Ver ROADMAP.md → "Copia superficial".
        var renamed = (SampleItem)MemberwiseClone();
        renamed.Name = normalizedName;
        return renamed;
    }

    // Misma regla para crear y para renombrar: se recortan los espacios exteriores, y el resultado
    // no puede quedar vacío ni superar MaxNameLength.
    private static string NormalizeName(string name)
    {
        var normalizedName = name?.Trim() ?? string.Empty;

        if (normalizedName.Length == 0)
            throw new DomainException("El nombre es obligatorio.");

        if (normalizedName.Length > MaxNameLength)
            throw new DomainException($"El nombre no puede superar {MaxNameLength} caracteres.");

        return normalizedName;
    }
}
