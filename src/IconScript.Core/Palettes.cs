namespace IconScript;

/// <summary>
/// Paletas predefinidas. PALETTE nombre declara (o reasigna) las variables de tipo
/// Color primary, secondary, accent, light y dark. Los colores se tomaron de escalas
/// tipo Tailwind para que combinen entre sí.
/// </summary>
public static class Palettes
{
    public static readonly string[] Names = { "primary", "secondary", "accent", "light", "dark" };

    static readonly Dictionary<string, int[]> palettes = new()
    {
        //                 primary   secondary accent    light     dark
        ["ocean"]  = new[] { 0x0284C7, 0x0D9488, 0x22D3EE, 0xE0F2FE, 0x0C4A6E },
        ["sunset"] = new[] { 0xF97316, 0xDB2777, 0xFACC15, 0xFFEDD5, 0x7C2D12 },
        ["forest"] = new[] { 0x16A34A, 0x65A30D, 0xFBBF24, 0xDCFCE7, 0x14532D },
        ["candy"]  = new[] { 0xEC4899, 0x8B5CF6, 0x2DD4BF, 0xFCE7F3, 0x831843 },
        ["mono"]   = new[] { 0x334155, 0x64748B, 0x94A3B8, 0xF1F5F9, 0x0F172A },
    };

    public static IEnumerable<string> All => palettes.Keys;

    /// <summary>Devuelve los pares (nombre de variable, color 0xRRGGBB) o null si la paleta no existe.</summary>
    public static IReadOnlyList<(string Name, int Rgb)> Find(string name)
    {
        if (!palettes.TryGetValue(name.ToLowerInvariant(), out var colors)) return null;
        return Names.Select((n, i) => (n, colors[i])).ToList();
    }
}
