using System.Globalization;
using System.Text;

namespace IconScript;

/// <summary>
/// Traduce la salida de la VM (Drawing) a SVG (sección 5.5 del plan).
/// Es la última etapa del back-end: transforma coordenadas de la tortuga
/// (y hacia arriba, origen en el centro) a coordenadas de pantalla (y hacia abajo)
/// aplicando además el autofit. La salida es determinística: el mismo programa
/// produce exactamente el mismo texto (necesario para los tests snapshot).
/// </summary>
public static class SvgWriter
{
    /// <summary>Número con a lo sumo 2 decimales, punto decimal y sin "-0".</summary>
    public static string Num(double v)
    {
        double r = Math.Round(v, 2, MidpointRounding.AwayFromZero);
        if (r == 0) r = 0; // elimina el -0
        return r.ToString("0.##", CultureInfo.InvariantCulture);
    }

    static string Hex(int rgb) => "#" + (rgb & 0xFFFFFF).ToString("X6");

    /// <summary>Transformación lineal tortuga → pantalla: sx = tx + s·x ; sy = ty − s·y.</summary>
    readonly record struct Xform(double S, double Tx, double Ty)
    {
        public double X(double x) => Tx + S * x;
        public double Y(double y) => Ty - S * y;
    }

    /// <summary>
    /// Autofit: centra la caja envolvente en la zona segura de la plantilla y
    /// escala SOLO hacia abajo si no entra. NOAUTOFIT: el origen queda en el centro.
    /// </summary>
    static Xform ComputeTransform(Drawing d, Template tpl, bool autoFit)
    {
        var b = d.Bounds();
        if (!autoFit || b.IsEmpty) return new Xform(1, Template.Size / 2.0, Template.Size / 2.0);
        double s = 1;
        if (b.Width > tpl.SafeSize) s = Math.Min(s, tpl.SafeSize / b.Width);
        if (b.Height > tpl.SafeSize) s = Math.Min(s, tpl.SafeSize / b.Height);
        return new Xform(s, tpl.SafeCx - s * b.CenterX, tpl.SafeCy + s * b.CenterY);
    }

    public static string Write(Drawing d, Template tpl, bool autoFit)
    {
        var xf = ComputeTransform(d, tpl, autoFit);
        var sb = new StringBuilder();
        var gradients = new List<Paint>();

        // Primero el cuerpo (para saber qué degradados se usan), después se arma el documento.
        var body = new StringBuilder();
        foreach (var s in d.Shapes) WriteShape(body, s, xf, gradients);

        string bgFill = PaintRef(d.Background, gradients);

        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1024 1024\" width=\"1024\" height=\"1024\">\n");
        sb.Append("<defs>\n");
        sb.Append($"<clipPath id=\"clip\"><path d=\"{tpl.ShapePath}\"/></clipPath>\n");
        for (int i = 0; i < gradients.Count; i++)
        {
            // Degradado diagonal a 135°: de arriba a la izquierda hacia abajo a la derecha.
            sb.Append($"<linearGradient id=\"grad{i + 1}\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">")
              .Append($"<stop offset=\"0\" stop-color=\"{Hex(gradients[i].C1)}\"/>")
              .Append($"<stop offset=\"1\" stop-color=\"{Hex(gradients[i].C2!.Value)}\"/>")
              .Append("</linearGradient>\n");
        }
        if (d.Shadow)
            sb.Append("<filter id=\"shadow\" x=\"-20%\" y=\"-20%\" width=\"140%\" height=\"140%\">" +
                      "<feDropShadow dx=\"0\" dy=\"12\" stdDeviation=\"16\" flood-color=\"#000000\" flood-opacity=\"0.35\"/>" +
                      "</filter>\n");
        sb.Append("</defs>\n");

        // Fondo = forma de la plantilla rellena con la pintura de BACKGROUND.
        sb.Append("<g id=\"background\">\n");
        sb.Append($"<path d=\"{tpl.ShapePath}\" fill=\"{bgFill}\"/>\n");
        if (tpl.DecorationPath != null)
        {
            // Hoja de "file": contorno suave para que se distinga sobre fondos claros + esquina doblada.
            sb.Append($"<path d=\"{tpl.ShapePath}\" fill=\"none\" stroke=\"#000000\" stroke-opacity=\"0.12\" stroke-width=\"8\"/>\n");
            sb.Append($"<path d=\"{tpl.DecorationPath}\" fill=\"#000000\" fill-opacity=\"0.15\"/>\n");
        }
        sb.Append("</g>\n");

        // Primer plano recortado por la forma de la plantilla.
        sb.Append("<g id=\"foreground\" clip-path=\"url(#clip)\">\n");
        if (d.Shadow) sb.Append("<g filter=\"url(#shadow)\">\n");
        sb.Append(body);
        if (d.Shadow) sb.Append("</g>\n");
        sb.Append("</g>\n");
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    static string PaintRef(Paint p, List<Paint> gradients)
    {
        if (!p.IsGradient) return Hex(p.C1);
        int i = gradients.IndexOf(p);
        if (i < 0) { gradients.Add(p); i = gradients.Count - 1; }
        return $"url(#grad{i + 1})";
    }

    static void WriteShape(StringBuilder sb, Shape s, Xform xf, List<Paint> gradients)
    {
        string style = s.IsStroke
            ? $"fill=\"none\" stroke=\"{Hex(s.StrokeColor)}\" stroke-width=\"{Num(s.StrokeWidth * xf.S)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\""
            : $"fill=\"{PaintRef(s.Fill, gradients)}\"";

        if (s.Kind == ShapeKind.Circle)
        {
            sb.Append($"<circle cx=\"{Num(xf.X(s.Cx))}\" cy=\"{Num(xf.Y(s.Cy))}\" r=\"{Num(s.R * xf.S)}\" {style}/>\n");
            return;
        }

        var d = new StringBuilder();
        foreach (var seg in s.Segs)
        {
            if (d.Length > 0) d.Append(' ');
            switch (seg.Kind)
            {
                case SegKind.Move: d.Append($"M{Num(xf.X(seg.X))} {Num(xf.Y(seg.Y))}"); break;
                case SegKind.Line: d.Append($"L{Num(xf.X(seg.X))} {Num(xf.Y(seg.Y))}"); break;
                case SegKind.Arc:
                {
                    // A rx ry rotación largeArc sweep x y. Los tramos son de <= 120°, así que
                    // largeArc es siempre 0. sweep = 1 si el arco gira en sentido horario:
                    // como la pantalla tiene el eje y invertido, el sentido visual se conserva.
                    string r = Num(seg.R * xf.S);
                    d.Append($"A{r} {r} 0 0 {(seg.Clockwise ? 1 : 0)} {Num(xf.X(seg.X))} {Num(xf.Y(seg.Y))}");
                    break;
                }
                case SegKind.Close: d.Append('Z'); break;
            }
        }
        sb.Append($"<path d=\"{d}\" {style}/>\n");
    }
}
