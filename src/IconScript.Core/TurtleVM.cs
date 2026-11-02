namespace IconScript;

/// <summary>Error de ejecución (división por cero, radio calculado &lt;= 0, programa demasiado largo...).</summary>
public class RuntimeError : Exception
{
    public int Line { get; }
    public int Col { get; }
    public RuntimeError(string message, int line, int col) : base(message) { Line = line; Col = col; }
}

/// <summary>
/// Máquina virtual de la tortuga (sección 5.2 del plan): una MÁQUINA DE PILA que
/// interpreta el código intermedio generado por el parser.
///
/// Estructuras:
///   - pila de valores (operandos de las instrucciones; todo es double, los
///     colores van codificados como 0xRRGGBB porque los tipos ya se verificaron
///     en compilación),
///   - pila de contadores de bucle (REPEAT),
///   - pila de estados guardados (MIRROR),
///   - arreglo de variables (indexado por el Slot de la tabla de símbolos).
/// </summary>
public class TurtleVM
{
    public const int MaxSteps = 1_000_000;
    public const int MaxShapes = 50_000;
    const int MaxSegments = 500_000;

    // --- Estado de la tortuga ---
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Heading { get; private set; }       // 0 = norte, horario positivo
    public bool PenDown { get; private set; } = true;
    public bool Mirror { get; private set; }
    int color = 0x000000;
    double width = 8;

    // --- Estructuras de la máquina ---
    readonly Stack<double> stack = new();
    readonly Stack<int> loops = new();
    readonly Stack<MirrorFrame> mirrors = new();
    double[] vars = Array.Empty<double>();

    // --- Salida ---
    readonly Drawing drawing = new();
    Shape stroke;          // StrokePath abierto (trazos consecutivos con el mismo estilo)
    Shape fill;            // FilledPath abierto entre FILLBEGIN y FILLEND
    int segments;
    Instr cur;             // instrucción en ejecución (para ubicar errores)

    class MirrorFrame
    {
        public double X, Y, Heading;   // estado al entrar al bloque (y eje del espejo)
        public bool OuterMirror;        // modo espejo que había antes del bloque
        public int Pass = 1;
    }

    public Drawing Run(IconProgram program, CancellationToken ct = default)
    {
        var code = program.Code;
        vars = new double[Math.Max(1, program.VarCount)];
        int pc = 0, steps = 0;

        while (true)
        {
            if (++steps > MaxSteps) Fail("programa demasiado largo");
            if ((steps & 0x3FF) == 0 && ct.IsCancellationRequested)
                Fail("tiempo de ejecución excedido");

            cur = code[pc++];
            switch (cur.Op)
            {
                case Op.PUSH: stack.Push(cur.Arg); break;
                case Op.LOAD: stack.Push(vars[(int)cur.Arg]); break;
                case Op.STORE: vars[(int)cur.Arg] = stack.Pop(); break;

                case Op.ADD: { double b = stack.Pop(), a = stack.Pop(); stack.Push(a + b); break; }
                case Op.SUB: { double b = stack.Pop(), a = stack.Pop(); stack.Push(a - b); break; }
                case Op.MUL: { double b = stack.Pop(), a = stack.Pop(); stack.Push(a * b); break; }
                case Op.DIV:
                {
                    double b = stack.Pop(), a = stack.Pop();
                    if (b == 0) Fail("división por cero");
                    stack.Push(a / b);
                    break;
                }
                case Op.NEG: stack.Push(-stack.Pop()); break;

                case Op.MOVE: Move(stack.Pop()); break;
                case Op.TURN:
                {
                    double g = stack.Pop();
                    Heading = Geometry.Normalize(Heading + (Mirror ? -g : g));
                    break;
                }
                case Op.PENUP: PenDown = false; EndStroke(); break;
                case Op.PENDOWN: PenDown = true; break;

                case Op.ARC:
                {
                    double g = stack.Pop(), r = stack.Pop();
                    if (r <= 0) Fail("el radio debe ser mayor que 0");
                    Arc(r, Mirror ? -g : g);
                    break;
                }
                case Op.MOVETO:
                {
                    double y = stack.Pop(), x = stack.Pop();
                    // En la pasada reflejada de MIRROR también se reflejan las posiciones absolutas,
                    // aplicando los espejos activos desde el más externo al más interno.
                    foreach (var m in mirrors.Reverse())
                        if (m.Pass == 2) (x, y) = Geometry.Reflect(x, y, m.X, m.Y, m.Heading);
                    if (fill != null) AddSeg(fill, new Seg(SegKind.Move, x, y));
                    else EndStroke();
                    X = x; Y = y;
                    break;
                }

                case Op.SETCOLOR: color = (int)stack.Pop(); break;
                case Op.SETWIDTH:
                {
                    double w = stack.Pop();
                    if (w <= 0) Fail("el grosor debe ser mayor que 0");
                    width = w;
                    break;
                }
                case Op.SHADOW: drawing.Shadow = true; break;
                case Op.BG: drawing.Background = new Paint((int)stack.Pop()); break;
                case Op.BGGRAD:
                {
                    int c2 = (int)stack.Pop(), c1 = (int)stack.Pop();
                    drawing.Background = new Paint(c1, c2);
                    break;
                }

                case Op.FILLBEGIN: BeginFill(new Paint((int)stack.Pop())); break;
                case Op.FILLGRADBEGIN:
                {
                    int c2 = (int)stack.Pop(), c1 = (int)stack.Pop();
                    BeginFill(new Paint(c1, c2));
                    break;
                }
                case Op.FILLEND:
                    AddSeg(fill, new Seg(SegKind.Close, X, Y));
                    if (fill.Segs.Count > 2) AddShape(fill);
                    fill = null;
                    break;

                case Op.CIRCLE:
                {
                    double r = stack.Pop();
                    if (r <= 0) Fail("el radio debe ser mayor que 0");
                    Circle(r);
                    break;
                }
                case Op.RECT:
                {
                    double h = stack.Pop(), w = stack.Pop();
                    if (w <= 0 || h <= 0) Fail("las dimensiones del rectángulo deben ser mayores que 0");
                    Rect(w, h);
                    break;
                }

                // REPEAT: LOOPBEGIN a  saca n; si n <= 0 salta al final (a, completado por
                // backpatching); si no, apila el contador. LOOPEND a lo decrementa y vuelve
                // al inicio del cuerpo mientras sea > 0.
                case Op.LOOPBEGIN:
                {
                    double n = Math.Floor(stack.Pop());
                    if (n < 0) Fail("la cantidad de repeticiones no puede ser negativa");
                    if (n == 0) pc = (int)cur.Arg;
                    else loops.Push((int)Math.Min(n, int.MaxValue));
                    break;
                }
                case Op.LOOPEND:
                {
                    int left = loops.Pop() - 1;
                    if (left > 0) { loops.Push(left); pc = (int)cur.Arg; }
                    break;
                }

                // MIRROR: el cuerpo se ejecuta dos veces. Al terminar la 1ª pasada se
                // restaura el estado guardado, se invierte el modo espejo (TURN y ARC
                // giran al revés) y se vuelve al inicio del cuerpo.
                case Op.MIRRORBEGIN:
                    mirrors.Push(new MirrorFrame { X = X, Y = Y, Heading = Heading, OuterMirror = Mirror });
                    break;
                case Op.MIRROREND:
                {
                    var m = mirrors.Peek();
                    EndStroke();
                    if (m.Pass == 1)
                    {
                        m.Pass = 2;
                        X = m.X; Y = m.Y; Heading = m.Heading;
                        if (fill != null) AddSeg(fill, new Seg(SegKind.Move, X, Y));
                        Mirror = !m.OuterMirror;
                        pc = (int)cur.Arg;
                    }
                    else
                    {
                        Mirror = m.OuterMirror;
                        mirrors.Pop();
                    }
                    break;
                }

                case Op.HALT:
                    EndStroke();
                    return drawing;

                default:
                    Fail($"instrucción desconocida {cur.Op}");
                    break;
            }
        }
    }

    void Fail(string msg) => throw new RuntimeError(msg, cur.Line, cur.Col);

    // ---------------------------------------------------------------- trazos

    void Move(double d)
    {
        var f = Geometry.Forward(Heading);
        double nx = X + d * f.X, ny = Y + d * f.Y;
        if (fill != null) AddSeg(fill, new Seg(SegKind.Line, nx, ny));
        else if (PenDown)
        {
            var s = OpenStroke();
            AddSeg(s, new Seg(SegKind.Line, nx, ny));
        }
        X = nx; Y = ny;
    }

    void Arc(double r, double g)
    {
        if (g == 0) return;
        Shape target = fill ?? (PenDown ? OpenStroke() : null);
        var box = BBox.Empty;
        var pieces = Geometry.Arc(X, Y, Heading, r, g, ref box, out double ex, out double ey, out double eh);
        if (target != null)
        {
            foreach (var p in pieces) AddSeg(target, new Seg(SegKind.Arc, p.EndX, p.EndY, p.Radius, p.Clockwise));
            target.Bounds.Include(box);
        }
        X = ex; Y = ey; Heading = eh;
    }

    /// <summary>
    /// Devuelve el StrokePath abierto si tiene el mismo estilo; si no, cierra el
    /// anterior y abre uno nuevo que empieza en la posición actual.
    /// </summary>
    Shape OpenStroke()
    {
        if (stroke != null && (stroke.StrokeColor != color || stroke.StrokeWidth != width)) EndStroke();
        if (stroke == null)
        {
            stroke = new Shape { Kind = ShapeKind.Path, StrokeColor = color, StrokeWidth = width };
            AddSeg(stroke, new Seg(SegKind.Move, X, Y));
        }
        return stroke;
    }

    void EndStroke()
    {
        if (stroke != null && stroke.Segs.Count > 1) AddShape(stroke);
        stroke = null;
    }

    void BeginFill(Paint paint)
    {
        EndStroke();
        fill = new Shape { Kind = ShapeKind.Path, Fill = paint };
        AddSeg(fill, new Seg(SegKind.Move, X, Y));
    }

    void Circle(double r)
    {
        EndStroke();
        var c = new Shape
        {
            Kind = ShapeKind.Circle, Cx = X, Cy = Y, R = r,
            Fill = fill?.Fill, StrokeColor = color, StrokeWidth = width,
        };
        c.Bounds.Include(X - r, Y - r);
        c.Bounds.Include(X + r, Y + r);
        AddShape(c);
    }

    void Rect(double w, double h)
    {
        EndStroke();
        var s = new Shape { Kind = ShapeKind.Path, Fill = fill?.Fill, StrokeColor = color, StrokeWidth = width };
        var corners = Geometry.RectCorners(X, Y, Heading, w, h);
        AddSeg(s, new Seg(SegKind.Move, corners[0].X, corners[0].Y));
        for (int i = 1; i < 4; i++) AddSeg(s, new Seg(SegKind.Line, corners[i].X, corners[i].Y));
        AddSeg(s, new Seg(SegKind.Close, corners[0].X, corners[0].Y));
        AddShape(s);
    }

    void AddSeg(Shape s, Seg seg)
    {
        if (++segments > MaxSegments) Fail("programa demasiado largo");
        s.Segs.Add(seg);
        if (seg.Kind != SegKind.Close) s.Bounds.Include(seg.X, seg.Y);
    }

    void AddShape(Shape s)
    {
        if (drawing.Shapes.Count >= MaxShapes) Fail("programa demasiado largo");
        drawing.Shapes.Add(s);
    }
}
