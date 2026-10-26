using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// Dependencias entre módulos (ARCHITECTURE.md → "Contrato entre módulos"):
/// A) un módulo solo referencia <c>Shared</c> y proyectos <c>*.Contracts</c>, nunca el proyecto principal de otro módulo;
/// B) <c>Shared</c> no referencia ningún proyecto de <c>src/Modules</c>;
/// C) un <c>*.Contracts</c> no referencia ningún proyecto de <c>src/Modules</c>, ni siquiera otro <c>*.Contracts</c>.
/// </summary>
/// <remarks>
/// Cada regla se comprueba con dos fuentes, porque ninguna basta sola:
/// - Las <c>ProjectReference</c> de los <c>.csproj</c>: ven una referencia aunque ningún tipo se use todavía,
///   pero no evalúan propiedades ni condiciones de MSBuild (se lee el atributo <c>Include</c> tal cual).
/// - <see cref="Assembly.GetReferencedAssemblies"/>: solo ve lo que el IL usa de verdad (una referencia sin
///   tipos usados no aparece), pero también ve lo que llega por transitividad.
/// Ninguna distingue capas dentro de un ensamblado: que solo el <c>Infrastructure/</c> del consumidor use el
/// Contracts de otro módulo es una regla de documentación y revisión, no de este test.
/// </remarks>
public class ArchitectureTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ModulesRoot = Path.Combine(RepoRoot, "src", "Modules");
    private static readonly string SharedProject = Path.Combine(RepoRoot, "src", "Shared", "Shared.csproj");

    [Fact]
    public void Discovery_FindsModuleProjects_SoTheRulesBelowAreNotVacuous()
    {
        Assert.NotEmpty(ModuleProjects());
        Assert.True(File.Exists(SharedProject), $"No se encontró {SharedProject}.");
    }

    [Fact]
    public void ModuleProjects_ReferenceOnlySharedAndContracts()
    {
        var violations = ModuleProjects()
            .SelectMany(project => ProjectReferences(project)
                .Where(reference => !IsSameFile(reference, SharedProject) && !IsContractsProject(reference))
                .Select(reference => $"{Relative(project)} -> {Relative(reference)}"))
            .ToList();

        Assert.True(violations.Count == 0,
            "Un módulo solo puede referenciar Shared y proyectos *.Contracts, nunca el proyecto principal de otro módulo:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void ModuleAssemblies_ReferenceNoModuleAssemblyExceptContracts()
    {
        var violations = ModuleProjects()
            .SelectMany(project => ReferencedModuleAssemblies(LoadAssemblyOf(project))
                .Where(name => !ContractsAssemblyNames().Contains(name))
                .Select(name => $"{AssemblyNameOf(project)} -> {name}"))
            .ToList();

        Assert.True(violations.Count == 0,
            "Un ensamblado de módulo usa tipos del proyecto principal de otro módulo:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void SharedProject_ReferencesNoModule()
    {
        var violations = ProjectReferences(SharedProject).Where(IsUnderModules).Select(Relative).ToList();

        Assert.True(violations.Count == 0,
            "Shared no puede referenciar ningún proyecto de src/Modules:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void SharedAssembly_ReferencesNoModuleAssembly()
    {
        var violations = ReferencedModuleAssemblies(LoadAssemblyOf(SharedProject)).ToList();

        Assert.True(violations.Count == 0,
            "El ensamblado Shared usa tipos de un módulo:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void ContractsProjects_ReferenceNoModule()
    {
        var violations = ContractsProjects()
            .SelectMany(project => ProjectReferences(project)
                .Where(IsUnderModules)
                .Select(reference => $"{Relative(project)} -> {Relative(reference)}"))
            .ToList();

        Assert.True(violations.Count == 0,
            "Un proyecto *.Contracts no puede referenciar ningún proyecto de src/Modules (ni otro *.Contracts):\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void ContractsAssemblies_ReferenceNoModuleAssembly()
    {
        // Un Contracts que ningún proyecto referencia todavía no se copia a la salida de este proyecto de
        // tests, así que aquí no se puede cargar; para ese caso queda la comprobación de su .csproj.
        var violations = ContractsProjects()
            .Select(TryLoadAssemblyOf)
            .OfType<Assembly>()
            .SelectMany(assembly => ReferencedModuleAssemblies(assembly)
                .Select(name => $"{assembly.GetName().Name} -> {name}"))
            .ToList();

        Assert.True(violations.Count == 0,
            "Un ensamblado *.Contracts usa tipos de un módulo (ni otro *.Contracts está permitido):\n"
            + string.Join("\n", violations));
    }

    // Todo .csproj bajo src/Modules que no sea un Contracts es (parte de) un módulo.
    private static IEnumerable<string> ModuleProjects() => ProjectsUnderModules().Where(p => !IsContractsProject(p));

    private static IEnumerable<string> ContractsProjects() => ProjectsUnderModules().Where(IsContractsProject);

    private static IEnumerable<string> ProjectsUnderModules() =>
        Directory.EnumerateFiles(ModulesRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) == ".csproj")
            .Where(path => !Relative(path).Split('/').Any(segment => segment is "bin" or "obj"));

    // src/Modules/{Modulo}.Contracts/{Modulo}.Contracts.csproj: carpeta hermana del módulo, no dentro de él
    // (dentro, el globbing por defecto del SDK compilaría los archivos del Contracts también en el módulo).
    private static bool IsContractsProject(string projectPath)
    {
        var directory = Path.GetDirectoryName(projectPath)!;
        var directoryName = Path.GetFileName(directory);
        return IsSameFile(Path.GetDirectoryName(directory)!, ModulesRoot)
            && directoryName.EndsWith(".Contracts", StringComparison.Ordinal)
            && Path.GetFileName(projectPath) == directoryName + ".csproj";
    }

    private static bool IsUnderModules(string path)
    {
        var relative = Path.GetRelativePath(ModulesRoot, path);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static IEnumerable<string> ProjectReferences(string projectPath)
    {
        var directory = Path.GetDirectoryName(projectPath)!;
        return XDocument.Load(projectPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(
                directory, include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))));
    }

    private static HashSet<string> ModuleAssemblyNames() =>
        ProjectsUnderModules().Select(AssemblyNameOf).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> ContractsAssemblyNames() =>
        ContractsProjects().Select(AssemblyNameOf).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> ReferencedModuleAssemblies(Assembly assembly)
    {
        var moduleNames = ModuleAssemblyNames();
        return assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(moduleNames.Contains);
    }

    // <AssemblyName> si el .csproj lo define; si no, el nombre del archivo (el valor por defecto del SDK).
    private static string AssemblyNameOf(string projectPath) =>
        XDocument.Load(projectPath).Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "AssemblyName")?.Value
        ?? Path.GetFileNameWithoutExtension(projectPath);

    private static Assembly LoadAssemblyOf(string projectPath) =>
        TryLoadAssemblyOf(projectPath)
        ?? throw new InvalidOperationException(
            $"No se pudo cargar el ensamblado {AssemblyNameOf(projectPath)} ({Relative(projectPath)}). Todo módulo debe "
            + "estar referenciado desde Bootstrap.csproj (ARCHITECTURE.md → \"Cómo agregar un módulo nuevo\").");

    private static Assembly? TryLoadAssemblyOf(string projectPath)
    {
        try
        {
            return Assembly.Load(new AssemblyName(AssemblyNameOf(projectPath)));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static bool IsSameFile(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static string Relative(string path) => Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');

    // Lee los .csproj del repo: sube desde la salida del build hasta encontrar MyApp.slnx.
    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyApp.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No se encontró MyApp.slnx subiendo desde {AppContext.BaseDirectory}.");
    }
}
