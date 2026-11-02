namespace IconScript.Tests;

/// <summary>FASE 3: código intermedio (tests golden del listado).</summary>
public class CodeGenTests
{
    static string Listing(string src) => CodeGen.Listing(TestUtil.Program(src).Code);

    [Fact]
    public void Estrella_ListadoGolden()
    {
        var src = File.ReadAllText(Path.Combine(TestUtil.Examples, "estrella.icon"));
        Assert.Equal("""
             0: PUSH 988970   ; #0F172A
             1: BG
             2: PUSH 16436245   ; #FACC15
             3: SETCOLOR
             4: PUSH 24
             5: SETWIDTH
             6: PUSH 5
             7: LOOPBEGIN 13
             8: PUSH 300
             9: MOVE
            10: PUSH 144
            11: TURN
            12: LOOPEND 8
            13: HALT

            """.ReplaceLineEndings("\n"), Listing(src));
    }

    [Fact]
    public void Repeat_BackpatchingDelSaltoHaciaAdelante()
    {
        var code = TestUtil.Program("REPEAT 3 [ FD 10 ] RT 90").Code;
        int loop = code.FindIndex(i => i.Op == Op.LOOPBEGIN);
        int end = code.FindIndex(i => i.Op == Op.LOOPEND);
        Assert.Equal(end + 1, (int)code[loop].Arg);   // LOOPBEGIN salta a la instrucción siguiente al LOOPEND
        Assert.Equal(loop + 1, (int)code[end].Arg);   // LOOPEND vuelve al inicio del cuerpo
    }

    [Fact]
    public void Repeat_Anidado()
    {
        var code = TestUtil.Program("REPEAT 2 [ REPEAT 3 [ FD 1 ] RT 1 ]").Code;
        var begins = code.Select((ins, i) => (ins, i)).Where(x => x.ins.Op == Op.LOOPBEGIN).ToList();
        var ends = code.Select((ins, i) => (ins, i)).Where(x => x.ins.Op == Op.LOOPEND).ToList();
        // El externo termina después del interno; cada LOOPBEGIN salta justo detrás de su LOOPEND.
        Assert.Equal(ends[1].i + 1, (int)begins[0].ins.Arg);
        Assert.Equal(ends[0].i + 1, (int)begins[1].ins.Arg);
    }

    [Fact]
    public void Bk_Lt_SeTraducenConNeg()
    {
        Assert.Equal("""
            0: PUSH 10
            1: NEG
            2: MOVE
            3: PUSH 90
            4: NEG
            5: TURN
            6: HALT

            """.ReplaceLineEndings("\n"), Listing("BK 10 LT 90"));
    }

    [Fact]
    public void Expresiones_EnPostfijoConPrecedencia()
    {
        var ops = TestUtil.Program("FD 1 + 2 * 3 - 4").Code.Select(i => i.Op).ToArray();
        Assert.Equal(new[] { Op.PUSH, Op.PUSH, Op.PUSH, Op.MUL, Op.ADD, Op.PUSH, Op.SUB, Op.MOVE, Op.HALT }, ops);
    }

    [Fact]
    public void Variables_LoadStoreConSlots()
    {
        Assert.Equal("""
            0: PUSH 5
            1: STORE 0   ; :a
            2: PUSH 16711680   ; #FF0000
            3: STORE 1   ; :b
            4: LOAD 0   ; :a
            5: PUSH 2
            6: MUL
            7: MOVE
            8: HALT

            """.ReplaceLineEndings("\n"), Listing("MAKE \"a 5 MAKE \"b #FF0000 FD :a * 2"));
    }

    [Fact]
    public void Mirror_YFill()
    {
        var ops = TestUtil.Program("MIRROR [ FILL GRADIENT #000000 #FFFFFF [ FD 1 ] ]").Code;
        Assert.Equal(Op.MIRRORBEGIN, ops[0].Op);
        Assert.Equal(Op.FILLGRADBEGIN, ops[3].Op);
        Assert.Equal(Op.FILLEND, ops[6].Op);
        Assert.Equal(Op.MIRROREND, ops[7].Op);
        Assert.Equal(1, ops[7].Arg);              // vuelve al inicio del cuerpo
    }

    [Fact]
    public void Metadatos_FueraDelIR()
    {
        var p = TestUtil.Program("TEMPLATE badge NOAUTOFIT FD 1");
        Assert.Equal("badge", p.Template);
        Assert.False(p.AutoFit);
        Assert.DoesNotContain(p.Code, i => i.Op.ToString().Contains("AUTOFIT"));
    }
}
