using System.Xml.Linq;

namespace IconScript.Tests;

/// <summary>
/// FASES 4-6: SVG. Tests snapshot: el SVG de cada ejemplo se compara con
/// tests/IconScript.Tests/Snapshots/&lt;nombre&gt;.svg. Para regenerarlos después de un
/// cambio intencional: UPDATE_SNAPSHOTS=1 dotnet test (y revisar el diff).
/// </summary>
public class SvgTests
{
    static string SnapshotDir => Path.Combine(TestUtil.RepoRoot, "tests", "IconScript.Tests", "Snapshots");

    public static IEnumerable<object[]> Ejemplos => TestUtil.ExampleFiles();

    [Theory]
    [MemberData(nameof(Ejemplos))]
    public void Snapshot(string file)
    {
        var r = IconCompiler.Compile(File.ReadAllText(Path.Combine(TestUtil.Examples, file)));
        Assert.True(r.Success, string.Join("; ", r.Errors));
        var path = Path.Combine(SnapshotDir, Path.ChangeExtension(file, ".svg"));

        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(SnapshotDir);
            File.WriteAllText(path, r.Svg);
        }
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), r.Svg);
    }

    [Theory]
    [MemberData(nameof(Ejemplos))]
    public void SvgEsXmlValido(string file)
    {
        var r = IconCompiler.Compile(File.ReadAllText(Path.Combine(TestUtil.Examples, file)));
        var doc = XDocument.Parse(r.Svg!);
        Assert.Equal("svg", doc.Root!.Name.LocalName);
    }

    [Fact]
    public void EsDeterministico()
    {
        var src = File.ReadAllText(Path.Combine(TestUtil.Examples, "flor.icon"));
        Assert.Equal(IconCompiler.Compile(src).Svg, IconCompiler.Compile(src).Svg);
    }

    [Fact]
    public void NoAutofit_OrigenEnElCentro()
    {
        var svg = IconCompiler.Compile("NOAUTOFIT FD 100").Svg!;
        Assert.Contains("d=\"M512 512 L512 412\"", svg);
    }

    [Fact]
    public void Autofit_CentraYSoloAchica()
    {
        // Una línea corta se centra en la zona segura sin escalar (grosor 8 se mantiene).
        var chica = IconCompiler.Compile("FD 100").Svg!;
        Assert.Contains("d=\"M512 562 L512 462\"", chica);
        Assert.Contains("stroke-width=\"8\"", chica);

        // Una línea de 2000 se achica para entrar en el 60 % (614,4 px) de "app".
        var grande = IconCompiler.Compile("FD 2000").Svg!;
        Assert.DoesNotContain("stroke-width=\"8\"", grande);
    }

    [Fact]
    public void Gradientes_Sombra_Y_Plantilla()
    {
        var svg = IconCompiler.Compile("TEMPLATE badge SHADOW BACKGROUND GRADIENT #000000 #FFFFFF CIRCLE 10").Svg!;
        Assert.Contains("<linearGradient id=\"grad1\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">", svg);
        Assert.Contains("<feDropShadow", svg);
        Assert.Contains("clip-path=\"url(#clip)\"", svg);
        Assert.Contains("fill=\"url(#grad1)\"", svg);
    }

    [Theory]
    [InlineData(1.006, "1.01")]
    [InlineData(1.004, "1")]
    [InlineData(-0.001, "0")]
    [InlineData(2.5, "2.5")]
    [InlineData(100, "100")]
    public void Numeros_ConHastaDosDecimales(double v, string esperado)
    {
        Assert.Equal(esperado, SvgWriter.Num(v));
    }
}
