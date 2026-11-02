#nullable enable
namespace IconScript;

/// <summary>
/// Plantilla de ícono (sección 5.4 del plan). Coordenadas de pantalla (SVG):
/// lienzo de 1024×1024, origen arriba a la izquierda, y hacia abajo.
/// </summary>
public class Template
{
    public string Name = "";
    public const int Size = 1024;
    /// <summary>Forma de fondo / máscara como datos de un path SVG.</summary>
    public string ShapePath = "";
    /// <summary>Detalle decorativo opcional sobre el fondo (esquina doblada de "file").</summary>
    public string? DecorationPath;
    /// <summary>Zona segura para el autofit: centro y lado (en píxeles).</summary>
    public double SafeCx, SafeCy, SafeSize;
}

public static class Templates
{
    static readonly Dictionary<string, Template> all = new()
    {
        // app: cuadrado con esquinas redondeadas (rx = 22,5 % del lado); zona segura 60 %
        // central, compatible con los íconos adaptativos de Android.
        ["app"] = new Template
        {
            Name = "app",
            ShapePath = RoundedSquare(0, 0, 1024, 1024 * 0.225),
            SafeCx = 512, SafeCy = 512, SafeSize = 1024 * 0.60,
        },
        // file: hoja con la esquina superior derecha doblada; zona segura 55 %
        // desplazada hacia abajo (para no tapar el doblez).
        ["file"] = new Template
        {
            Name = "file",
            ShapePath = "M208 64 L664 64 L864 264 L864 912 Q864 960 816 960 L208 960 Q160 960 160 912 L160 112 Q160 64 208 64 Z",
            DecorationPath = "M664 64 L664 216 Q664 264 712 264 L864 264 Z",
            SafeCx = 512, SafeCy = 590, SafeSize = 1024 * 0.55,
        },
        // badge: círculo; zona segura 65 % central.
        ["badge"] = new Template
        {
            Name = "badge",
            ShapePath = "M512 0 A512 512 0 1 1 512 1024 A512 512 0 1 1 512 0 Z",
            SafeCx = 512, SafeCy = 512, SafeSize = 1024 * 0.65,
        },
    };

    public static IEnumerable<string> Names => all.Keys;

    public static bool IsKnown(string name) => all.ContainsKey(name.ToLowerInvariant());

    public static Template Get(string name) => all[name.ToLowerInvariant()];

    static string RoundedSquare(double x, double y, double size, double r)
    {
        string n(double v) => SvgWriter.Num(v);
        double x2 = x + size, y2 = y + size;
        return $"M{n(x + r)} {n(y)} L{n(x2 - r)} {n(y)} A{n(r)} {n(r)} 0 0 1 {n(x2)} {n(y + r)} " +
               $"L{n(x2)} {n(y2 - r)} A{n(r)} {n(r)} 0 0 1 {n(x2 - r)} {n(y2)} " +
               $"L{n(x + r)} {n(y2)} A{n(r)} {n(r)} 0 0 1 {n(x)} {n(y2 - r)} " +
               $"L{n(x)} {n(y + r)} A{n(r)} {n(r)} 0 0 1 {n(x + r)} {n(y)} Z";
    }
}
