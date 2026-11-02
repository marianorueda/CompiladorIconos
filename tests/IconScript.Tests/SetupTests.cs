namespace IconScript.Tests;

/// <summary>
/// FASE 0: pruebas de humo. Verifican que el parser generado por Coco/R desde el
/// .ATG compila, reconoce un programa válido y que la clase Errors modificada
/// (Parser.frame) devuelve los errores como Diagnostic con línea y columna.
/// </summary>
public class SetupTests
{
    [Fact]
    public void ProgramaValido_CompilaSinErrores()
    {
        var r = IconCompiler.Compile("FD 100\nFD 50 // comentario con acentos: ñandú");
        Assert.True(r.Success);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void FaltaNumero_DaErrorSintacticoConPosicion()
    {
        var r = IconCompiler.Compile("FD 100\nFD FD");
        Assert.False(r.Success);
        var e = Assert.Single(r.Errors);
        Assert.Equal(DiagnosticKind.Sintactico, e.Kind);
        Assert.Equal(2, e.Line);
        Assert.Equal(4, e.Col);
    }

    [Fact]
    public void CaracterInvalido_DaErrorLexico()
    {
        var r = IconCompiler.Compile("FD 100 @");
        Assert.False(r.Success);
        var e = Assert.Single(r.Errors);
        Assert.Equal(DiagnosticKind.Lexico, e.Kind);
        Assert.Equal("símbolo inválido '@'", e.Message);
        Assert.Equal(1, e.Line);
        Assert.Equal(8, e.Col);
    }
}
