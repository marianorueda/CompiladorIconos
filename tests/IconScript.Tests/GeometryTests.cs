namespace IconScript.Tests;

/// <summary>FASES 4-5: máquina virtual y geometría (tests obligatorios de la sección 5.3).</summary>
public class GeometryTests
{
    const int P = 6; // precisión (decimales) de las comparaciones

    [Fact]
    public void Cuadrado_VuelveAlOrigenConLaMismaDireccion()
    {
        var vm = TestUtil.Run("REPEAT 4 [ FD 100 RT 90 ]");
        Assert.Equal(0, vm.X, P);
        Assert.Equal(0, vm.Y, P);
        Assert.Equal(0, vm.Heading, P);
    }

    [Fact]
    public void Arc360_VuelveAlOrigen()
    {
        var vm = TestUtil.Run("ARC 50 360");
        Assert.Equal(0, vm.X, P);
        Assert.Equal(0, vm.Y, P);
        Assert.Equal(0, vm.Heading, P);
    }

    [Fact]
    public void Arc90_TerminaEn50_50MirandoAlEste()
    {
        var vm = TestUtil.Run("ARC 50 90");
        Assert.Equal(50, vm.X, P);
        Assert.Equal(50, vm.Y, P);
        Assert.Equal(90, vm.Heading, P);
    }

    [Fact]
    public void ArcNegativo_GiraALaIzquierda()
    {
        var vm = TestUtil.Run("ARC 50 (-90)");
        Assert.Equal(-50, vm.X, P);
        Assert.Equal(50, vm.Y, P);
        Assert.Equal(270, vm.Heading, P);
    }

    [Fact]
    public void Movimientos_Basicos()
    {
        var vm = TestUtil.Run("FD 100 RT 90 FD 50 BK 20 LT 90");
        Assert.Equal(30, vm.X, P);
        Assert.Equal(100, vm.Y, P);
        Assert.Equal(0, vm.Heading, P);
    }

    [Fact]
    public void MoveTo_EsAbsolutoYSinTrazo()
    {
        var vm = new TurtleVM();
        var d = vm.Run(TestUtil.Program("RT 45 MOVETO 10 (-20)"));
        Assert.Equal(10, vm.X, P);
        Assert.Equal(-20, vm.Y, P);
        Assert.Equal(45, vm.Heading, P);
        Assert.Empty(d.Shapes);
    }

    [Fact]
    public void Mirror_DibujaDosVecesYTerminaReflejado()
    {
        var vm = new TurtleVM();
        var d = vm.Run(TestUtil.Program("MIRROR [ RT 90 FD 100 ]"));
        Assert.Equal(2, d.Shapes.Count);          // trazo original + reflejado
        Assert.Equal(-100, vm.X, P);              // la 2ª pasada gira a la izquierda
        Assert.Equal(270, vm.Heading, P);
        Assert.False(vm.Mirror);
    }

    [Fact]
    public void Mirror_ReflejaMoveTo()
    {
        var vm = TestUtil.Run("MIRROR [ MOVETO 30 40 ]");
        Assert.Equal(-30, vm.X, P);
        Assert.Equal(40, vm.Y, P);
    }

    [Fact]
    public void Trazos_ConElMismoEstiloSeAcumulan()
    {
        var d = new TurtleVM().Run(TestUtil.Program("FD 10 RT 90 FD 10 COLOR #FF0000 FD 10 PU FD 10 PD FD 10"));
        Assert.Equal(3, d.Shapes.Count);
        Assert.Equal(3, d.Shapes[0].Segs.Count);  // M + 2 L
    }

    [Fact]
    public void Fill_AgregaTodoMovimientoYCierra()
    {
        var d = new TurtleVM().Run(TestUtil.Program("FILL #FF0000 [ FD 10 PU RT 90 FD 10 ]"));
        var s = Assert.Single(d.Shapes);
        Assert.False(s.IsStroke);
        Assert.Equal(SegKind.Close, s.Segs[^1].Kind);
        Assert.Equal(4, s.Segs.Count);
    }

    [Fact]
    public void BoundingBox_DeUnArcoIncluyeLosPuntosCardinales()
    {
        var box = BBox.Empty;
        Geometry.Arc(0, 0, 0, 50, 180, ref box, out _, out _, out _);
        // Semicírculo a la derecha: centro (50,0); el punto más alto es (50,50).
        Assert.Equal(0, box.MinX, P);
        Assert.Equal(100, box.MaxX, P);
        Assert.Equal(50, box.MaxY, P);
        Assert.Equal(0, box.MinY, P);
    }

    [Fact]
    public void Arcos_SePartenEnTramosDeHasta120Grados()
    {
        var box = BBox.Empty;
        Assert.Equal(3, Geometry.Arc(0, 0, 0, 50, 360, ref box, out _, out _, out _).Count);
        Assert.Single(Geometry.Arc(0, 0, 0, 50, 90, ref box, out _, out _, out _));
    }

    [Theory]
    [InlineData("MAKE \"n 0 FD 10 / :n", "división por cero")]
    [InlineData("MAKE \"w 0 WIDTH :w", "el grosor debe ser mayor que 0")]
    [InlineData("MAKE \"n -2 REPEAT :n [ FD 1 ]", "la cantidad de repeticiones no puede ser negativa")]
    [InlineData("REPEAT 2000000 [ FD 1 ]", "programa demasiado largo")]
    public void ErroresDeEjecucion(string src, string msg)
    {
        var r = IconCompiler.Compile(src);
        var e = Assert.Single(r.Errors);
        Assert.Equal(DiagnosticKind.Ejecucion, e.Kind);
        Assert.Equal(msg, e.Message);
        Assert.Null(r.Svg);
    }

    [Fact]
    public void RepeatCero_NoEjecutaElCuerpo()
    {
        var vm = TestUtil.Run("REPEAT 0 [ FD 100 ] FD 1");
        Assert.Equal(1, vm.Y, P);
    }
}
