using System.Globalization;
using System.Text;

namespace IconScript;

/// <summary>
/// Instrucciones de la máquina de pila de la tortuga (sección 5.1 del plan).
/// La columna "pila" indica qué consume y qué produce cada una.
/// </summary>
public enum Op
{
    PUSH,          // ...           -> ..., v        constante
    LOAD,          // ...           -> ..., var[s]
    STORE,         // ..., v        -> ...           var[s] = v
    ADD, SUB, MUL, DIV, // ..., a, b -> ..., a op b
    NEG,           // ..., a        -> ..., -a
    MOVE,          // ..., d        -> ...           avanza d (BK = NEG + MOVE)
    TURN,          // ..., g        -> ...           gira g a la derecha (LT = NEG + TURN)
    PENUP, PENDOWN,
    ARC,           // ..., r, g     -> ...           arco de radio r y g grados (g > 0 derecha)
    MOVETO,        // ..., x, y     -> ...           posición absoluta sin trazo
    SETCOLOR,      // ..., c        -> ...
    SETWIDTH,      // ..., w        -> ...
    SHADOW,
    BG,            // ..., c        -> ...           fondo sólido
    BGGRAD,        // ..., c1, c2   -> ...           fondo degradado
    FILLBEGIN,     // ..., c        -> ...           abre relleno
    FILLGRADBEGIN, // ..., c1, c2   -> ...           abre relleno degradado
    FILLEND,
    CIRCLE,        // ..., r        -> ...
    RECT,          // ..., w, h     -> ...
    LOOPBEGIN,     // ..., n        -> ...           si n <= 0 salta a 'a'; si no apila el contador
    LOOPEND,       // decrementa el contador; si > 0 salta a 'a' (inicio del cuerpo)
    MIRRORBEGIN,   // guarda el estado de la tortuga
    MIRROREND,     // 1ª pasada: restaura, activa espejo y salta a 'a'; 2ª: desactiva
    HALT,
}

/// <summary>
/// Una instrucción del código intermedio. Su dirección es su índice en la lista.
/// Line/Col indican la posición del fuente que la originó (para errores de ejecución).
/// Comment es solo informativo para el listado (p. ej. el nombre de la variable).
/// </summary>
public record struct Instr(Op Op, double Arg, int Line, int Col, string Comment);

/// <summary>Programa compilado: código intermedio + metadatos que no forman parte del IR.</summary>
public class IconProgram
{
    public List<Instr> Code = new();
    public string Template = "app";
    public bool AutoFit = true;
    public int VarCount;
}

/// <summary>
/// Generador de código intermedio. El parser llama a Emit desde las acciones
/// semánticas del .ATG (traducción dirigida por la sintaxis).
/// </summary>
public class CodeGen
{
    public readonly List<Instr> Code = new();

    /// <summary>Dirección de la próxima instrucción a emitir.</summary>
    public int Pc => Code.Count;

    /// <summary>Agrega una instrucción al final y devuelve su dirección.</summary>
    public int Emit(Op op, double arg = 0, int line = 0, int col = 0, string comment = null)
    {
        Code.Add(new Instr(op, arg, line, col, comment));
        return Code.Count - 1;
    }

    /// <summary>
    /// Backpatching: completa el destino de un salto hacia adelante que se emitió
    /// cuando todavía no se conocía la dirección destino (como el FJMP del while de Taste).
    /// </summary>
    public void Patch(int addr, int target)
    {
        Code[addr] = Code[addr] with { Arg = target };
    }

    static bool HasArg(Op op) => op is Op.PUSH or Op.LOAD or Op.STORE
        or Op.LOOPBEGIN or Op.LOOPEND or Op.MIRROREND;

    public static string FormatNumber(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Listado legible "0: PUSH 8" para `iconc -code` y la pestaña de la web.</summary>
    public static string Listing(IReadOnlyList<Instr> code)
    {
        var sb = new StringBuilder();
        int width = (code.Count - 1).ToString().Length;
        for (int i = 0; i < code.Count; i++)
        {
            var ins = code[i];
            sb.Append(i.ToString().PadLeft(width)).Append(": ").Append(ins.Op);
            if (HasArg(ins.Op)) sb.Append(' ').Append(FormatNumber(ins.Arg));
            if (ins.Comment != null) sb.Append("   ; ").Append(ins.Comment);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public string Listing() => Listing(Code);
}
