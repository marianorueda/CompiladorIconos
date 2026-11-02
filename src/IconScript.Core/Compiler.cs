// El Core compila con <Nullable>disable</Nullable> por el código generado por Coco;
// en nuestros propios archivos sí usamos anotaciones de nulabilidad.
#nullable enable
using System.Text;
using System.Text.RegularExpressions;

namespace IconScript;

/// <summary>Categorías de error (sección 5.6 del plan).</summary>
public static class DiagnosticKind
{
    public const string Lexico = "Lexico";
    public const string Sintactico = "Sintactico";
    public const string Semantico = "Semantico";
    public const string Ejecucion = "Ejecucion";
}

/// <summary>Un error con su posición en el fuente. Lo crea la clase Errors (Parser.frame modificado).</summary>
public record Diagnostic(int Line, int Col, string Message, string Kind);

/// <summary>Resultado de compilar un programa IconScript.</summary>
public record CompileResult(bool Success, IReadOnlyList<Diagnostic> Errors,
                            string? IntermediateCode, string? Svg, string Template);

/// <summary>
/// Fachada del compilador: única puerta de entrada para la CLI, la API y los tests.
/// Encadena las fases: análisis (scanner + parser + semántica + generación de código,
/// en una sola pasada dirigida por la sintaxis) → máquina virtual → SVG.
/// </summary>
public static class IconCompiler
{
    public static CompileResult Compile(string source) => Compile(source, CancellationToken.None);

    public static CompileResult Compile(string source, CancellationToken ct)
    {
        var program = Analyze(source, out var errors);
        if (errors.Count > 0 || program == null)
            // Si hay errores de compilación no se ejecuta la VM ni se genera SVG.
            return new CompileResult(false, errors, null, null, program?.Template ?? "app");

        string listing = CodeGen.Listing(program.Code);
        Drawing drawing;
        try
        {
            drawing = new TurtleVM().Run(program, ct);
        }
        catch (RuntimeError e)
        {
            var diag = new Diagnostic(e.Line, e.Col, e.Message, DiagnosticKind.Ejecucion);
            return new CompileResult(false, new[] { diag }, listing, null, program.Template);
        }

        string svg = SvgWriter.Write(drawing, Templates.Get(program.Template), program.AutoFit);
        return new CompileResult(true, Array.Empty<Diagnostic>(), listing, svg, program.Template);
    }

    /// <summary>
    /// Front-end: análisis léxico, sintáctico y semántico + generación del código
    /// intermedio. Devuelve el programa (aunque haya errores) y la lista de errores
    /// ordenada por posición, con los mensajes de Coco traducidos al español.
    /// </summary>
    public static IconProgram? Analyze(string source, out IReadOnlyList<Diagnostic> errors)
    {
        // El scanner de Coco/R lee bytes desde un Stream. Anteponemos el BOM de UTF-8
        // para que use su UTF8Buffer y decodifique bien los acentos de los comentarios;
        // así compilamos texto que llega de la web sin crear archivos.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray();
        var scanner = new Scanner(new MemoryStream(bytes));
        var parser = new Parser(scanner);
        parser.tab = new SymbolTable(parser.errors);
        parser.gen = new CodeGen();
        try
        {
            parser.Parse();
        }
        catch (FatalError e)
        {
            errors = new[] { new Diagnostic(0, 0, e.Message, DiagnosticKind.Lexico) };
            return null;
        }

        errors = parser.errors.List
            .Select(d => d.Kind == DiagnosticKind.Sintactico ? d with { Message = TranslateSyntax(d.Message) } : d)
            .OrderBy(d => d.Line).ThenBy(d => d.Col)
            .ToList();

        return new IconProgram
        {
            Code = parser.gen.Code,
            Template = parser.template,
            AutoFit = parser.autoFit,
            VarCount = parser.tab.VarCount,
        };
    }

    // ------------------------------------------------------------------ mensajes

    /// <summary>
    /// Traduce los mensajes sintácticos que genera Coco/R ("X expected", "invalid X")
    /// al español. Coco los genera a partir de los nombres de los tokens y de las
    /// producciones del .ATG.
    /// </summary>
    public static string TranslateSyntax(string msg)
    {
        var m = Regex.Match(msg, "^(.*) expected$");
        if (m.Success) return "se esperaba " + Friendly(m.Groups[1].Value);

        m = Regex.Match(msg, "^invalid (.*)$");
        if (m.Success)
            return m.Groups[1].Value switch
            {
                "Statement" => "comando inválido",
                "Factor" => "se esperaba una expresión (número, color, :variable o paréntesis)",
                "Paint" => "se esperaba un color o GRADIENT",
                var nt => $"{nt} inválido",
            };
        return msg;
    }

    static string Friendly(string symbol) => symbol switch
    {
        "EOF" => "un comando",
        "number" => "un número",
        "color" => "un color (#RRGGBB)",
        "ident" => "un nombre",
        "varDecl" => "un nombre de variable (\"nombre)",
        "varRef" => "una variable (:nombre)",
        "???" => "un símbolo válido",
        // Literales: Coco los escribe entre comillas dobles ("[" expected)
        _ when symbol.Length >= 2 && symbol[0] == '"' && symbol[^1] == '"' => "'" + symbol[1..^1] + "'",
        _ => symbol,
    };
}
