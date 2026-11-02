namespace IconScript.Tests;

static class TestUtil
{
    /// <summary>Raíz del repositorio (donde está IconScript.sln), buscando hacia arriba desde bin/.</summary>
    public static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "IconScript.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("no se encontró la raíz del repositorio");
        }
    }

    public static string Examples => Path.Combine(RepoRoot, "examples");

    public static IEnumerable<object[]> ExampleFiles(string sub = "") =>
        Directory.GetFiles(Path.Combine(Examples, sub), "*.icon").OrderBy(f => f)
                 .Select(f => new object[] { Path.GetFileName(f) });

    /// <summary>Analiza y devuelve el programa; falla el test si hay errores.</summary>
    public static IconProgram Program(string source)
    {
        var p = IconCompiler.Analyze(source, out var errors);
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        return p!;
    }

    /// <summary>Compila y ejecuta; devuelve la VM para inspeccionar el estado final de la tortuga.</summary>
    public static TurtleVM Run(string source)
    {
        var vm = new TurtleVM();
        vm.Run(Program(source));
        return vm;
    }

    public static IReadOnlyList<Diagnostic> Errors(string source)
    {
        IconCompiler.Analyze(source, out var errors);
        return errors;
    }
}
