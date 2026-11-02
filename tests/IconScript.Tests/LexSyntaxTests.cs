namespace IconScript.Tests;

/// <summary>FASE 1: análisis léxico y sintáctico.</summary>
public class LexSyntaxTests
{
    public static IEnumerable<object[]> Validos => TestUtil.ExampleFiles();

    [Theory]
    [MemberData(nameof(Validos))]
    public void TodosLosEjemplosSeReconocen(string file)
    {
        var src = File.ReadAllText(Path.Combine(TestUtil.Examples, file));
        var errors = TestUtil.Errors(src);
        Assert.True(errors.Count == 0, $"{file}: {string.Join("; ", errors)}");
    }

    [Theory]
    [InlineData("lexico-caracter.icon", DiagnosticKind.Lexico, 4, 8)]
    [InlineData("lexico-color.icon", DiagnosticKind.Lexico, 3, 12)]
    [InlineData("sintactico-corchete.icon", DiagnosticKind.Sintactico, 4, 1)]
    [InlineData("sintactico-argumento.icon", DiagnosticKind.Sintactico, 4, 1)]
    [InlineData("semantico-tipos.icon", DiagnosticKind.Semantico, 4, 7)]
    [InlineData("semantico-declaracion.icon", DiagnosticKind.Semantico, 2, 10)]
    public void EjemplosConErrores_PrimerErrorConTipoYPosicion(string file, string kind, int line, int col)
    {
        var src = File.ReadAllText(Path.Combine(TestUtil.Examples, "errores", file));
        var first = TestUtil.Errors(src).First();
        Assert.Equal(kind, first.Kind);
        Assert.Equal(line, first.Line);
        Assert.Equal(col, first.Col);
    }

    [Fact]
    public void PalabrasClave_NoDistinguenMayusculas()
    {
        Assert.Empty(TestUtil.Errors("fd 10 Fd 10 FORWARD 10 repeat 2 [ rt 90 ]"));
    }

    [Fact]
    public void Comentarios_DeLineaYAnidados()
    {
        Assert.Empty(TestUtil.Errors("// línea\nFD 10 /* bloque /* anidado */ sigue */ RT 90"));
    }

    [Theory]
    [InlineData("FD 2.5", 2.5)]
    [InlineData("FD 100", 100)]
    public void Numeros_EnterosYDecimales(string src, double v)
    {
        var p = TestUtil.Program(src);
        Assert.Equal(v, p.Code[0].Arg);
    }

    [Fact]
    public void ColorMalFormado_EsErrorLexico()
    {
        var e = TestUtil.Errors("COLOR #12345").First();
        Assert.Equal(DiagnosticKind.Lexico, e.Kind);
        Assert.Contains("#12345", e.Message);
    }

    [Fact]
    public void MensajesSintacticos_TraducidosAlEspanol()
    {
        var e = Assert.Single(TestUtil.Errors("REPEAT 4 [ FD 10"));
        Assert.Equal("se esperaba ']'", e.Message);
    }

    /// <summary>
    /// Conflicto LL(1) documentado: en "ARC 30 -180" el '-' continúa la primera
    /// expresión (resta 30 - 180) y falta el segundo argumento. Con paréntesis
    /// el segundo argumento negativo se reconoce bien.
    /// </summary>
    [Fact]
    public void ConflictoLL1_MenosEnSegundoArgumento()
    {
        // Se reconoce ARC (30 - 180) <falta> : radio constante -150 (semántico) y falta el 2º argumento.
        var errs = TestUtil.Errors("ARC 30 -180");
        Assert.Equal(new[] { DiagnosticKind.Semantico, DiagnosticKind.Sintactico }, errs.Select(e => e.Kind));
        Assert.Equal("el radio debe ser mayor que 0", errs[0].Message);

        var p = TestUtil.Program("ARC 30 (-180)");
        var ops = p.Code.Select(i => i.Op).ToArray();
        Assert.Equal(new[] { Op.PUSH, Op.PUSH, Op.NEG, Op.ARC, Op.HALT }, ops);

        // "ARC 300 -180 90" se reconoce como ARC (300 - 180) 90
        var resta = TestUtil.Program("ARC 300 -180 90");
        Assert.Contains(resta.Code, i => i.Op == Op.SUB);
    }
}
