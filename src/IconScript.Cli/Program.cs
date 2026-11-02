using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using IconScript;

// iconc -- compilador de línea de comandos de IconScript (sección 6.1 del plan).
//
//   iconc flor.icon                  genera flor.svg
//   iconc flor.icon -o salida.svg    elige el archivo de salida
//   iconc flor.icon -code            además imprime el código intermedio
//   iconc flor.icon -open            abre el SVG en el navegador
//
// Arrastrar un .icon sobre iconc.exe = compilar y abrir automáticamente
// (y pausar la consola si hubo errores para poder leerlos).

Console.OutputEncoding = Encoding.UTF8;

string? input = null, output = null;
bool showCode = false, open = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-o":
            if (i + 1 >= args.Length) return Usage("falta el nombre de archivo después de -o");
            output = args[++i];
            break;
        case "-code": showCode = true; break;
        case "-open": open = true; break;
        case "-h": case "--help": case "-?": return Usage(null);
        default:
            if (args[i].StartsWith('-')) return Usage($"opción desconocida '{args[i]}'");
            if (input != null) return Usage("solo se puede compilar un archivo por vez");
            input = args[i];
            break;
    }
}

if (input == null) return Usage(null);

// "Arrastrar y soltar": el Explorador de Windows lanza el .exe con un único argumento
// y una consola propia (nadie más está adjunto a ella).
bool dragAndDrop = args.Length == 1 && LaunchedWithOwnConsole();
if (dragAndDrop) open = true;

if (!File.Exists(input))
{
    Console.Error.WriteLine($"no se encontró el archivo '{input}'");
    return Finish(2);
}

output ??= Path.ChangeExtension(input, ".svg");

var result = IconCompiler.Compile(File.ReadAllText(input));

if (showCode && result.IntermediateCode != null)
{
    Console.WriteLine("Código intermedio:");
    Console.Write(result.IntermediateCode);
    Console.WriteLine();
}

if (!result.Success)
{
    foreach (var e in result.Errors)
        Console.Error.WriteLine($"{Path.GetFileName(input)}: línea {e.Line}, col {e.Col}: {e.Message}  [{KindName(e.Kind)}]");
    Console.Error.WriteLine(result.Errors.Count == 1 ? "1 error" : $"{result.Errors.Count} errores");
    return Finish(1);
}

File.WriteAllText(output, result.Svg!);
Console.WriteLine($"generado {output} (plantilla {result.Template})");

if (open)
{
    try
    {
        Process.Start(new ProcessStartInfo(Path.GetFullPath(output)) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"no se pudo abrir el SVG: {ex.Message}");
    }
}
return 0;

// ----------------------------------------------------------------------------

int Finish(int code)
{
    if (code != 0 && dragAndDrop)
    {
        Console.Error.WriteLine("Presioná una tecla para cerrar...");
        try { Console.ReadKey(true); } catch (InvalidOperationException) { }
    }
    return code;
}

static int Usage(string? error)
{
    if (error != null) Console.Error.WriteLine($"iconc: {error}");
    Console.Error.WriteLine("""
        uso: iconc <archivo.icon> [-o salida.svg] [-code] [-open]

          -o archivo   archivo SVG de salida (por defecto, el mismo nombre con .svg)
          -code        imprime el código intermedio
          -open        abre el SVG generado con la aplicación predeterminada
        """);
    return 2;
}

static string KindName(string kind) => kind switch
{
    DiagnosticKind.Lexico => "léxico",
    DiagnosticKind.Sintactico => "sintáctico",
    DiagnosticKind.Semantico => "semántico",
    DiagnosticKind.Ejecucion => "ejecución",
    _ => kind,
};

// En Windows, si este proceso es el único adjunto a la consola, la consola fue
// creada para él: se lanzó con doble clic o arrastrando un archivo sobre el .exe.
static bool LaunchedWithOwnConsole()
{
    if (!OperatingSystem.IsWindows()) return false;
    try
    {
        var list = new uint[2];
        return Native.GetConsoleProcessList(list, 2) == 1;
    }
    catch { return false; }
}

static partial class Native
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetConsoleProcessList(uint[] processList, uint processCount);
}
