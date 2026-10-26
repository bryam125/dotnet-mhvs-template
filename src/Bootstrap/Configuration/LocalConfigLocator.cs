namespace MyApp.Bootstrap.Configuration;

/// <summary>
/// Localiza <c>config/connectionstrings.local.json</c> (credenciales locales, no versionadas).
/// </summary>
/// <remarks>
/// <c>AddJsonFile("config/...")</c> con ruta relativa se resuelve contra el content root, que con
/// <c>dotnet run</c> o Visual Studio es <c>src/Bootstrap/</c>, no la raíz del repo donde el README
/// indica crear el archivo; y con <c>optional: true</c> el fallo era silencioso. Aquí se busca
/// hacia arriba desde el content root, así funciona igual si el archivo vive en la raíz del repo
/// (caso normal), junto al proyecto, o en un <c>config/</c> montado junto a la app publicada.
/// </remarks>
public static class LocalConfigLocator
{
    public const string RelativePath = "config/connectionstrings.local.json";

    // Content root (src/Bootstrap) + src/ + raíz del repo. Un límite corto evita recoger por
    // accidente un archivo homónimo de un directorio ajeno más arriba en el disco.
    private const int MaxLevelsUp = 2;

    /// <returns>La ruta absoluta del primer archivo encontrado, o <c>null</c> si no existe ninguno.</returns>
    public static string? Locate(string contentRootPath)
    {
        var directory = new DirectoryInfo(contentRootPath);

        for (var level = 0; level <= MaxLevelsUp && directory is not null; level++, directory = directory.Parent)
        {
            var candidate = Path.GetFullPath(Path.Combine(directory.FullName, RelativePath));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
