namespace IconScript;

/// <summary>Rectángulo envolvente (bounding box) en coordenadas de la tortuga.</summary>
public struct BBox
{
    public double MinX, MinY, MaxX, MaxY;
    public bool IsEmpty;

    public static BBox Empty => new() { IsEmpty = true };

    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public double CenterX => (MinX + MaxX) / 2;
    public double CenterY => (MinY + MaxY) / 2;

    public void Include(double x, double y)
    {
        if (IsEmpty) { MinX = MaxX = x; MinY = MaxY = y; IsEmpty = false; return; }
        MinX = Math.Min(MinX, x); MaxX = Math.Max(MaxX, x);
        MinY = Math.Min(MinY, y); MaxY = Math.Max(MaxY, y);
    }

    public void Include(BBox b)
    {
        if (b.IsEmpty) return;
        Include(b.MinX, b.MinY);
        Include(b.MaxX, b.MaxY);
    }

    /// <summary>Agranda la caja d unidades en cada dirección (medio grosor del trazo).</summary>
    public void Expand(double d)
    {
        if (IsEmpty) return;
        MinX -= d; MinY -= d; MaxX += d; MaxY += d;
    }
}

/// <summary>Un tramo de arco (siempre de a lo sumo 120°) listo para SVG.</summary>
public readonly record struct ArcPiece(double EndX, double EndY, double Radius, bool Clockwise);

/// <summary>
/// Geometría de la tortuga (sección 5.3 del plan).
/// Convención: origen en el centro del lienzo, eje y hacia arriba, dirección
/// (heading) en grados con 0 = norte y sentido horario positivo (RT suma).
/// </summary>
public static class Geometry
{
    const double Deg = Math.PI / 180;

    /// <summary>Vector unitario hacia adelante para la dirección h: (sin h, cos h).</summary>
    public static (double X, double Y) Forward(double heading) =>
        (Math.Sin(heading * Deg), Math.Cos(heading * Deg));

    /// <summary>Vector unitario hacia la derecha de la tortuga: (cos h, -sin h).</summary>
    public static (double X, double Y) Right(double heading) =>
        (Math.Cos(heading * Deg), -Math.Sin(heading * Deg));

    /// <summary>Normaliza un ángulo a [0, 360).</summary>
    public static double Normalize(double deg)
    {
        deg %= 360;
        if (deg < 0) deg += 360;
        if (Math.Abs(deg - 360) < 1e-9 || Math.Abs(deg) < 1e-9) deg = 0;
        return deg;
    }

    /// <summary>
    /// ARC r g desde P=(x,y) con dirección h:
    ///  1. Centro C = P + r·derecha si g > 0 (gira a la derecha), P − r·derecha si g &lt; 0.
    ///  2. El punto final es P rotado alrededor de C en g grados (horario si g > 0).
    ///     En ángulos matemáticos (antihorarios) eso es sumar −g al ángulo inicial.
    ///  3. Nueva dirección = h + g.
    /// Se parte en tramos de a lo sumo 120°: un único comando "A" de SVG no puede
    /// dibujar un círculo completo (ni distinguir bien el caso de 180° exactos).
    /// También se actualiza la caja envolvente: extremos + puntos cardinales del
    /// círculo que caen dentro del barrido.
    /// </summary>
    public static List<ArcPiece> Arc(double x, double y, double heading, double r, double g,
                                     ref BBox box, out double endX, out double endY, out double endHeading)
    {
        var pieces = new List<ArcPiece>();
        var right = Right(heading);
        double sign = g >= 0 ? 1 : -1;
        double cx = x + sign * r * right.X;
        double cy = y + sign * r * right.Y;

        double a0 = Math.Atan2(y - cy, x - cx) / Deg; // ángulo inicial (matemático)
        double sweep = -g;                             // horario => ángulo decreciente

        int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(g) / 120 - 1e-9));
        double step = sweep / n;
        box.Include(x, y);
        for (int i = 1; i <= n; i++)
        {
            double a = a0 + step * i;
            double px = cx + r * Math.Cos(a * Deg);
            double py = cy + r * Math.Sin(a * Deg);
            pieces.Add(new ArcPiece(px, py, r, g > 0));
            box.Include(px, py);
        }

        // Puntos cardinales (0°, 90°, 180°, 270°) estrictamente dentro del barrido.
        double lo = Math.Min(a0, a0 + sweep), hi = Math.Max(a0, a0 + sweep);
        for (double k = Math.Ceiling(lo / 90) * 90; k < hi; k += 90)
            if (k > lo) box.Include(cx + r * Math.Cos(k * Deg), cy + r * Math.Sin(k * Deg));

        endX = pieces[^1].EndX;
        endY = pieces[^1].EndY;
        endHeading = Normalize(heading + g);
        return pieces;
    }

    /// <summary>
    /// Refleja el punto p respecto de la recta que pasa por (ax, ay) con dirección h.
    /// Lo usa MIRROR para MOVETO (posición absoluta) en la pasada reflejada.
    /// </summary>
    public static (double X, double Y) Reflect(double px, double py, double ax, double ay, double heading)
    {
        var d = Forward(heading);
        double vx = px - ax, vy = py - ay;
        double dot = vx * d.X + vy * d.Y;
        return (ax + 2 * dot * d.X - vx, ay + 2 * dot * d.Y - vy);
    }

    /// <summary>Esquinas de un rectángulo w×h centrado en (x,y): w a lo ancho y h a lo largo de la dirección.</summary>
    public static (double X, double Y)[] RectCorners(double x, double y, double heading, double w, double h)
    {
        var f = Forward(heading);
        var r = Right(heading);
        double hw = w / 2, hh = h / 2;
        return new[]
        {
            (x - hw * r.X + hh * f.X, y - hw * r.Y + hh * f.Y),
            (x + hw * r.X + hh * f.X, y + hw * r.Y + hh * f.Y),
            (x + hw * r.X - hh * f.X, y + hw * r.Y - hh * f.Y),
            (x - hw * r.X - hh * f.X, y - hw * r.Y - hh * f.Y),
        };
    }
}
