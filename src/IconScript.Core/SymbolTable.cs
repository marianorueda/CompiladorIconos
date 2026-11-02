namespace IconScript;

/// <summary>
/// Un símbolo de la tabla. Slot es el índice de la variable en el arreglo de
/// variables de la máquina virtual (operandos de LOAD/STORE).
/// </summary>
public class Symbol
{
    public string Name;
    public IconType Type;
    public int Slot;

    public Symbol(string name, IconType type, int slot) { Name = name; Type = type; Slot = slot; }
}

/// <summary>
/// Tabla de símbolos según el modelo visto en la materia (como la de Taste):
/// una pila de scopes, cada uno con sus símbolos. En el alcance base de IconScript
/// hay un único scope global (no hay procedimientos). OpenScope/CloseScope quedan
/// implementados como extensión futura para procedimientos TO ... END.
///
/// Los nombres de variables no distinguen mayúsculas (coherente con IGNORECASE):
/// se guardan en minúscula.
/// </summary>
public class SymbolTable
{
    readonly Errors errors;
    readonly List<Dictionary<string, Symbol>> scopes = new();
    int nextSlot = 0;

    /// <summary>
    /// Símbolo "comodín" que devuelve Find cuando el nombre no existe. Tiene tipo
    /// Undef, así el resto del análisis no genera errores en cascada.
    /// </summary>
    public readonly Symbol undefSym = new("undef", IconType.Undef, 0);

    public SymbolTable(Errors errors)
    {
        this.errors = errors;
        OpenScope(); // scope global
    }

    /// <summary>Cantidad de variables (tamaño del arreglo de variables de la VM).</summary>
    public int VarCount => nextSlot;

    /// <summary>Extensión futura: abre un scope nuevo (p. ej. para un procedimiento).</summary>
    public void OpenScope() => scopes.Add(new Dictionary<string, Symbol>());

    /// <summary>Extensión futura: cierra el scope actual.</summary>
    public void CloseScope()
    {
        if (scopes.Count > 1) scopes.RemoveAt(scopes.Count - 1);
    }

    /// <summary>Inserta un símbolo nuevo en el scope actual y le asigna un slot.</summary>
    public Symbol Insert(string name, IconType type)
    {
        name = name.ToLowerInvariant();
        var sym = new Symbol(name, type, nextSlot++);
        scopes[^1][name] = sym;
        return sym;
    }

    /// <summary>Busca sin reportar errores (null si no existe). Recorre los scopes de adentro hacia afuera.</summary>
    public Symbol Lookup(string name)
    {
        name = name.ToLowerInvariant();
        for (int i = scopes.Count - 1; i >= 0; i--)
            if (scopes[i].TryGetValue(name, out var s)) return s;
        return null;
    }

    /// <summary>
    /// Busca un símbolo que se está USANDO. Si no existe reporta
    /// "variable 'x' no declarada" en la posición del token y devuelve undefSym.
    /// </summary>
    public Symbol Find(string name, Token at)
    {
        var s = Lookup(name);
        if (s != null) return s;
        errors.SemErr(at.line, at.col, $"variable '{name}' no declarada");
        return undefSym;
    }
}
