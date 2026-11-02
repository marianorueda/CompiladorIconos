namespace IconScript;

/// <summary>Pintura: un color sólido (C2 == null) o un degradado de C1 a C2. Colores 0xRRGGBB.</summary>
public record Paint(int C1, int? C2 = null)
{
    public bool IsGradient => C2.HasValue;
}

public enum SegKind { Move, Line, Arc, Close }

/// <summary>Un tramo de camino en coordenadas de la tortuga.</summary>
public readonly record struct Seg(SegKind Kind, double X, double Y, double R = 0, bool Clockwise = false);

public enum ShapeKind { Path, Circle }

/// <summary>
/// Una forma de la salida de la VM: un camino (StrokePath o FilledPath) o un círculo.
/// Si Fill != null la forma se rellena; si no, se dibuja su contorno con
/// StrokeColor y StrokeWidth.
/// </summary>
public class Shape
{
    public ShapeKind Kind;
    public List<Seg> Segs = new();
    public double Cx, Cy, R;            // solo para círculos
    public Paint Fill;                  // relleno (null = trazo)
    public int StrokeColor;
    public double StrokeWidth;
    public BBox Bounds = BBox.Empty;    // sin contar el grosor del trazo

    public bool IsStroke => Fill == null;
}

/// <summary>Resultado de ejecutar un programa: fondo + formas en orden de dibujo.</summary>
public class Drawing
{
    public Paint Background = new(0xFFFFFF);
    public List<Shape> Shapes = new();
    public bool Shadow;

    /// <summary>Caja envolvente de todas las formas (sin el fondo), incluyendo el grosor de los trazos.</summary>
    public BBox Bounds()
    {
        var b = BBox.Empty;
        foreach (var s in Shapes)
        {
            var sb = s.Bounds;
            if (s.IsStroke) sb.Expand(s.StrokeWidth / 2);
            b.Include(sb);
        }
        return b;
    }
}
