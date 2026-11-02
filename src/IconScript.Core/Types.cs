namespace IconScript;

/// <summary>
/// Tipos del lenguaje. Cada expresión sintetiza su tipo como atributo de salida
/// (Expr&lt;out IconType typ&gt; en el .ATG). Undef se usa cuando ya hubo un error:
/// las reglas semánticas no vuelven a reportar sobre un operando Undef, así se
/// evitan los errores en cascada.
/// </summary>
public enum IconType { Undef, Number, Color }

public static class IconTypes
{
    /// <summary>Nombre del tipo en español, para los mensajes de error.</summary>
    public static string Spanish(this IconType t) => t switch
    {
        IconType.Number => "número",
        IconType.Color => "color",
        _ => "indefinido",
    };
}
