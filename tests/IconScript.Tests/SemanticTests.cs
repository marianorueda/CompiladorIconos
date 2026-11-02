namespace IconScript.Tests;

/// <summary>FASE 2: reglas semánticas de la sección 4.3 del plan.</summary>
public class SemanticTests
{
    static Diagnostic Single(string src)
    {
        var errors = TestUtil.Errors(src);
        var e = Assert.Single(errors);
        Assert.Equal(DiagnosticKind.Semantico, e.Kind);
        return e;
    }

    [Theory]
    [InlineData("FD #FF0000 + 1")]
    [InlineData("FD 2 * #FF0000")]
    [InlineData("FD -#FF0000")]
    public void Aritmetica_SoloEntreNumeros(string src)
    {
        Assert.Equal("operadores aritméticos requieren números", Single(src).Message);
    }

    [Theory]
    [InlineData("FD #000000")]
    [InlineData("BK #000000")]
    [InlineData("RT #000000")]
    [InlineData("LT #000000")]
    [InlineData("WIDTH #000000")]
    [InlineData("ARC #000000 90")]
    [InlineData("MOVETO 0 #000000")]
    [InlineData("REPEAT #000000 [ ]")]
    [InlineData("CIRCLE #000000")]
    [InlineData("RECT 10 #000000")]
    public void Comandos_RequierenNumero(string src)
    {
        Assert.Equal("se esperaba un número", Single(src).Message);
    }

    [Theory]
    [InlineData("COLOR 5")]
    [InlineData("BACKGROUND 5")]
    [InlineData("FILL 5 [ ]")]
    [InlineData("BACKGROUND GRADIENT #000000 5")]
    public void Comandos_RequierenColor(string src)
    {
        Assert.Equal("se esperaba un color", Single(src).Message);
    }

    [Fact]
    public void Variable_NoDeclarada()
    {
        var e = Single("FD :lado");
        Assert.Equal("variable 'lado' no declarada", e.Message);
        Assert.Equal(4, e.Col);
    }

    [Fact]
    public void Variable_NoDeclarada_SinCascada()
    {
        // :x es Undef: no debe reportar además "se esperaba un número" ni errores aritméticos.
        Single("FD :x * 2 + 1");
    }

    [Fact]
    public void Variable_NoCambiaDeTipo()
    {
        Assert.Equal("'c' es de tipo color, no se le puede asignar un número",
                     Single("MAKE \"c #FF0000 MAKE \"c 5").Message);
        Assert.Equal("'n' es de tipo número, no se le puede asignar un color",
                     Single("MAKE \"n 5 MAKE \"n #FF0000").Message);
    }

    [Fact]
    public void Variable_SeUsaConSuTipo()
    {
        Assert.Empty(TestUtil.Errors("MAKE \"c #FF0000 MAKE \"n 5 COLOR :c FD :n MAKE \"n :n + 1"));
        Assert.Empty(TestUtil.Errors("MAKE \"Lado 5 FD :LADO"));   // sin distinción de mayúsculas
    }

    [Fact]
    public void Make_LaVariableNoExisteDentroDeSuPropiaDefinicion()
    {
        Assert.Equal("variable 'x' no declarada", Single("MAKE \"x :x + 1").Message);
    }

    [Fact]
    public void Template_Desconocida()
    {
        Assert.Equal("plantilla desconocida 'poster'", Single("TEMPLATE poster").Message);
    }

    [Theory]
    [InlineData("FD 10 TEMPLATE app")]
    [InlineData("TEMPLATE app TEMPLATE badge")]
    [InlineData("REPEAT 2 [ TEMPLATE app ]")]
    public void Template_UnaVezYAlInicio(string src)
    {
        Assert.Equal("TEMPLATE solo puede aparecer una vez, al inicio del programa", Single(src).Message);
    }

    [Theory]
    [InlineData("app")]
    [InlineData("FILE")]
    [InlineData("badge")]
    public void Template_Validas(string name)
    {
        var p = TestUtil.Program("TEMPLATE " + name);
        Assert.Equal(name.ToLowerInvariant(), p.Template);
    }

    [Fact]
    public void Palette_Desconocida()
    {
        Assert.Equal("paleta desconocida 'arcoiris'", Single("PALETTE arcoiris").Message);
    }

    [Fact]
    public void Palette_DeclaraLosCincoColores()
    {
        Assert.Empty(TestUtil.Errors("PALETTE ocean COLOR :primary COLOR :secondary COLOR :accent COLOR :light COLOR :dark"));
        Assert.Empty(TestUtil.Errors("PALETTE ocean PALETTE sunset COLOR :primary")); // reasigna
    }

    [Fact]
    public void Palette_NoPisaUnaVariableNumerica()
    {
        Assert.Contains("'primary' es de tipo número", Single("MAKE \"primary 3 PALETTE mono").Message);
    }

    [Theory]
    [InlineData("ARC 0 90", "el radio debe ser mayor que 0")]
    [InlineData("ARC -5 90", "el radio debe ser mayor que 0")]
    [InlineData("CIRCLE 10 - 10", "el radio debe ser mayor que 0")]
    [InlineData("WIDTH 0", "el grosor debe ser mayor que 0")]
    [InlineData("REPEAT -1 [ ]", "la cantidad de repeticiones no puede ser negativa")]
    [InlineData("RECT 0 10", "el ancho debe ser mayor que 0")]
    [InlineData("FD 10 / 0", "división por cero")]
    [InlineData("FD 10 / (2 - 2)", "división por cero")]
    public void Constantes_SeVerificanEnCompilacion(string src, string msg)
    {
        Assert.Equal(msg, Single(src).Message);
    }

    [Fact]
    public void ValoresConVariables_SeVerificanEnEjecucion()
    {
        Assert.Empty(TestUtil.Errors("MAKE \"r 0 CIRCLE :r"));
        var r = IconCompiler.Compile("MAKE \"r 0\nCIRCLE :r");
        var e = Assert.Single(r.Errors);
        Assert.Equal(DiagnosticKind.Ejecucion, e.Kind);
        Assert.Equal("el radio debe ser mayor que 0", e.Message);
        Assert.Equal(2, e.Line);
    }

    [Fact]
    public void Fill_NoSeAnida()
    {
        Assert.Equal("no se puede anidar un FILL dentro de otro FILL",
                     Single("FILL #000000 [ FILL #FFFFFF [ CIRCLE 10 ] ]").Message);
    }

    [Fact]
    public void ConErrores_NoSeGeneraSvg()
    {
        var r = IconCompiler.Compile("FD :nada");
        Assert.False(r.Success);
        Assert.Null(r.Svg);
        Assert.Null(r.IntermediateCode);
    }
}
