using System.Globalization;



using System;

namespace IconScript {



public class Parser {
	public const int _EOF = 0;
	public const int _number = 1;
	public const int _color = 2;
	public const int _ident = 3;
	public const int _varDecl = 4;
	public const int _varRef = 5;
	public const int maxT = 42;

	const bool _T = true;
	const bool _x = false;
	const int minErrDist = 2;
	
	public Scanner scanner;
	public Errors  errors;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;

public SymbolTable tab;              // tabla de símbolos (la crea la fachada)
  public CodeGen gen;                  // generador de código intermedio
  public string template = "app";      // metadato: plantilla elegida
  public bool autoFit = true;          // metadato: NOAUTOFIT lo pone en false

  bool templateSeen = false;           // para validar "TEMPLATE una vez y al inicio"
  int statementCount = 0;              // comandos ya reconocidos
  int blockDepth = 0;                  // profundidad de bloques [ ... ]
  int fillDepth = 0;                   // FILL no se puede anidar

  // Error semántico en la posición de un token concreto (no siempre el último leído).
  void Error(Token tok, string msg) { errors.SemErr(tok.line, tok.col, msg); }

  // Emite una instrucción registrando la posición del último token leído,
  // para poder reportar errores de ejecución con línea y columna.
  int Emit(Op op, double arg = 0, string comment = null) {
    return gen.Emit(op, arg, t.line, t.col, comment);
  }

  static double ParseNumber(string s) { return double.Parse(s, CultureInfo.InvariantCulture); }
  static int ParseColor(string s) { return int.Parse(s.Substring(1), NumberStyles.HexNumber); }
  // "lado o :lado -> lado (sin el prefijo)
  static string VarName(string s) { return s.Substring(1); }

  // Reglas de tipo: si el operando es Undef ya se informó un error antes y no se repite.
  void CheckNumber(IconType typ, Token at) {
    if (typ == IconType.Color) Error(at, "se esperaba un número");
  }
  void CheckColor(IconType typ, Token at) {
    if (typ == IconType.Number) Error(at, "se esperaba un color");
  }
  // Verificación de constantes: solo si el valor se conoce en compilación (cv != null).
  // Si depende de variables se verifica en ejecución (TurtleVM).
  void CheckPositive(double? cv, Token at, string msg) {
    if (cv.HasValue && cv.Value <= 0) Error(at, msg);
  }
  // Tipo del resultado de una operación aritmética binaria.
  IconType Arith(IconType a, IconType b, Token op) {
    if (a == IconType.Color || b == IconType.Color) {
      Error(op, "operadores aritméticos requieren números");
      return IconType.Undef;
    }
    if (a == IconType.Undef || b == IconType.Undef) return IconType.Undef;
    return IconType.Number;
  }



	public Parser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
		errDist = 0;
	}
	
	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			// [IconScript] Error LEXICO: el scanner devuelve el token especial noSym
			// (kind == maxT, el terminal "???" de Coco) cuando un caracter no forma
			// ningun token valido. Lo reportamos como error lexico y lo descartamos,
			// asi el parser sigue analizando sin generar un error sintactico en cascada.
			if (la.kind == maxT) { errors.LexErr(la.line, la.col, la.val); la = t; continue; }
			if (la.kind <= maxT) { ++errDist; break; }

			la = t;
		}
	}
	
	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}
	
	bool StartOf (int s) {
		return set[s, la.kind];
	}
	
	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}

	
	void IconScript() {
		while (StartOf(1)) {
			Statement();
		}
		Emit(Op.HALT); 
	}

	void Statement() {
		switch (la.kind) {
		case 6: {
			TemplateDecl();
			break;
		}
		case 7: case 8: case 9: case 10: {
			Move();
			break;
		}
		case 11: case 12: case 13: case 14: {
			Turn();
			break;
		}
		case 15: case 16: case 17: case 18: {
			Pen();
			break;
		}
		case 19: {
			Arc();
			break;
		}
		case 20: {
			MoveTo();
			break;
		}
		case 21: {
			Repeat();
			break;
		}
		case 24: {
			Mirror();
			break;
		}
		case 25: {
			ColorCmd();
			break;
		}
		case 26: {
			WidthCmd();
			break;
		}
		case 27: {
			PaletteCmd();
			break;
		}
		case 28: {
			ShadowCmd();
			break;
		}
		case 29: {
			Background();
			break;
		}
		case 30: {
			Fill();
			break;
		}
		case 32: {
			Circle();
			break;
		}
		case 33: {
			Rect();
			break;
		}
		case 34: {
			Make();
			break;
		}
		case 35: {
			NoAutofit();
			break;
		}
		default: SynErr(43); break;
		}
		statementCount++; 
	}

	void TemplateDecl() {
		Token kw; 
		Expect(6);
		kw = t; 
		Expect(3);
		string name = t.val.ToLowerInvariant();
		if (templateSeen || statementCount > 0 || blockDepth > 0)
		 Error(kw, "TEMPLATE solo puede aparecer una vez, al inicio del programa");
		else if (!Templates.IsKnown(name))
		 Error(t, "plantilla desconocida '" + t.val + "'");
		else template = name;
		templateSeen = true; 
	}

	void Move() {
		bool back = false; IconType typ; double? cv; Token start; 
		if (la.kind == 7) {
			Get();
		} else if (la.kind == 8) {
			Get();
		} else if (la.kind == 9 || la.kind == 10) {
			if (la.kind == 9) {
				Get();
			} else {
				Get();
			}
			back = true; 
		} else SynErr(44);
		start = la; 
		Expr(out typ, out cv);
		CheckNumber(typ, start);
		if (back) Emit(Op.NEG);
		Emit(Op.MOVE); 
	}

	void Turn() {
		bool left = false; IconType typ; double? cv; Token start; 
		if (la.kind == 11) {
			Get();
		} else if (la.kind == 12) {
			Get();
		} else if (la.kind == 13 || la.kind == 14) {
			if (la.kind == 13) {
				Get();
			} else {
				Get();
			}
			left = true; 
		} else SynErr(45);
		start = la; 
		Expr(out typ, out cv);
		CheckNumber(typ, start);
		if (left) Emit(Op.NEG);
		Emit(Op.TURN); 
	}

	void Pen() {
		if (la.kind == 15 || la.kind == 16) {
			if (la.kind == 15) {
				Get();
			} else {
				Get();
			}
			Emit(Op.PENUP); 
		} else if (la.kind == 17 || la.kind == 18) {
			if (la.kind == 17) {
				Get();
			} else {
				Get();
			}
			Emit(Op.PENDOWN); 
		} else SynErr(46);
	}

	void Arc() {
		IconType t1, t2; double? r, g; Token s1, s2; 
		Expect(19);
		s1 = la; 
		Expr(out t1, out r);
		s2 = la; 
		Expr(out t2, out g);
		CheckNumber(t1, s1); CheckNumber(t2, s2);
		CheckPositive(r, s1, "el radio debe ser mayor que 0");
		Emit(Op.ARC); 
	}

	void MoveTo() {
		IconType t1, t2; double? cx, cy; Token s1, s2; 
		Expect(20);
		s1 = la; 
		Expr(out t1, out cx);
		s2 = la; 
		Expr(out t2, out cy);
		CheckNumber(t1, s1); CheckNumber(t2, s2);
		Emit(Op.MOVETO); 
	}

	void Repeat() {
		IconType typ; double? n; Token start; int loop, body; 
		Expect(21);
		start = la; 
		Expr(out typ, out n);
		CheckNumber(typ, start);
		if (n.HasValue && n.Value < 0)
		 Error(start, "la cantidad de repeticiones no puede ser negativa");
		loop = Emit(Op.LOOPBEGIN, 0);   // destino pendiente
		body = gen.Pc;
		blockDepth++; 
		Expect(22);
		while (StartOf(1)) {
			Statement();
		}
		Expect(23);
		blockDepth--;
		Emit(Op.LOOPEND, body);
		gen.Patch(loop, gen.Pc); 
	}

	void Mirror() {
		int body; 
		Expect(24);
		Emit(Op.MIRRORBEGIN);
		body = gen.Pc;
		blockDepth++; 
		Expect(22);
		while (StartOf(1)) {
			Statement();
		}
		Expect(23);
		blockDepth--;
		Emit(Op.MIRROREND, body); 
	}

	void ColorCmd() {
		IconType typ; double? cv; Token start; 
		Expect(25);
		start = la; 
		Expr(out typ, out cv);
		CheckColor(typ, start); Emit(Op.SETCOLOR); 
	}

	void WidthCmd() {
		IconType typ; double? cv; Token start; 
		Expect(26);
		start = la; 
		Expr(out typ, out cv);
		CheckNumber(typ, start);
		CheckPositive(cv, start, "el grosor debe ser mayor que 0");
		Emit(Op.SETWIDTH); 
	}

	void PaletteCmd() {
		Expect(27);
		Expect(3);
		var colors = Palettes.Find(t.val);
		if (colors == null) Error(t, "paleta desconocida '" + t.val + "'");
		else foreach (var (name, rgb) in colors) {
		 Symbol sym = tab.Lookup(name) ?? tab.Insert(name, IconType.Color);
		 if (sym.Type == IconType.Number)
		   Error(t, "'" + name + "' es de tipo número, no se le puede asignar un color");
		 else {
		   Emit(Op.PUSH, rgb, "#" + rgb.ToString("X6"));
		   Emit(Op.STORE, sym.Slot, ":" + name);
		 }
		} 
	}

	void ShadowCmd() {
		Expect(28);
		Emit(Op.SHADOW); 
	}

	void Background() {
		bool grad; 
		Expect(29);
		Paint(out grad);
		Emit(grad ? Op.BGGRAD : Op.BG); 
	}

	void Fill() {
		bool grad; Token kw; 
		Expect(30);
		kw = t;
		if (fillDepth > 0) Error(kw, "no se puede anidar un FILL dentro de otro FILL"); 
		Paint(out grad);
		Emit(grad ? Op.FILLGRADBEGIN : Op.FILLBEGIN);
		fillDepth++; blockDepth++; 
		Expect(22);
		while (StartOf(1)) {
			Statement();
		}
		Expect(23);
		fillDepth--; blockDepth--;
		Emit(Op.FILLEND); 
	}

	void Circle() {
		IconType typ; double? r; Token start; 
		Expect(32);
		start = la; 
		Expr(out typ, out r);
		CheckNumber(typ, start);
		CheckPositive(r, start, "el radio debe ser mayor que 0");
		Emit(Op.CIRCLE); 
	}

	void Rect() {
		IconType t1, t2; double? w, h; Token s1, s2; 
		Expect(33);
		s1 = la; 
		Expr(out t1, out w);
		s2 = la; 
		Expr(out t2, out h);
		CheckNumber(t1, s1); CheckNumber(t2, s2);
		CheckPositive(w, s1, "el ancho debe ser mayor que 0");
		CheckPositive(h, s2, "el alto debe ser mayor que 0");
		Emit(Op.RECT); 
	}

	void Make() {
		Token nameTok; string name; IconType typ; double? cv; 
		Expect(34);
		Expect(4);
		nameTok = t; name = VarName(t.val); 
		Expr(out typ, out cv);
		Symbol sym = tab.Lookup(name);
		if (sym == null) sym = tab.Insert(name, typ);
		else if (sym.Type == IconType.Undef) sym.Type = typ;
		else if (typ != IconType.Undef && sym.Type != typ)
		 Error(nameTok, "'" + name + "' es de tipo " + sym.Type.Spanish()
		       + ", no se le puede asignar un " + typ.Spanish());
		Emit(Op.STORE, sym.Slot, ":" + sym.Name); 
	}

	void NoAutofit() {
		Expect(35);
		autoFit = false; 
	}

	void Expr(out IconType typ, out double? cv) {
		IconType t2; double? c2; Token op = null; bool plus = false; 
		Term(out typ, out cv);
		while (la.kind == 36 || la.kind == 37) {
			if (la.kind == 36) {
				Get();
				plus = true; 
			} else {
				Get();
				plus = false; 
			}
			op = t; 
			Term(out t2, out c2);
			typ = Arith(typ, t2, op);
			cv = (cv.HasValue && c2.HasValue)
			    ? (plus ? cv.Value + c2.Value : cv.Value - c2.Value) : (double?)null;
			Emit(plus ? Op.ADD : Op.SUB); 
		}
	}

	void Paint(out bool gradient) {
		IconType t1, t2; double? cv; Token s1, s2; gradient = false; 
		if (la.kind == 31) {
			Get();
			gradient = true; s1 = la; 
			Expr(out t1, out cv);
			s2 = la; 
			Expr(out t2, out cv);
			CheckColor(t1, s1); CheckColor(t2, s2); 
		} else if (StartOf(2)) {
			s1 = la; 
			Expr(out t1, out cv);
			CheckColor(t1, s1); 
		} else SynErr(47);
	}

	void Term(out IconType typ, out double? cv) {
		IconType t2; double? c2; Token op = null; bool mul = false; 
		Factor(out typ, out cv);
		while (la.kind == 38 || la.kind == 39) {
			if (la.kind == 38) {
				Get();
				mul = true; 
			} else {
				Get();
				mul = false; 
			}
			op = t; 
			Factor(out t2, out c2);
			typ = Arith(typ, t2, op);
			bool divZero = !mul && c2.HasValue && c2.Value == 0;
			if (divZero) Error(op, "división por cero");
			cv = (cv.HasValue && c2.HasValue && !divZero)
			    ? (mul ? cv.Value * c2.Value : cv.Value / c2.Value) : (double?)null;
			Emit(mul ? Op.MUL : Op.DIV); 
		}
	}

	void Factor(out IconType typ, out double? cv) {
		typ = IconType.Undef; cv = null; Token op = null; 
		if (la.kind == 1) {
			Get();
			cv = ParseNumber(t.val);
			typ = IconType.Number;
			Emit(Op.PUSH, cv.Value); 
		} else if (la.kind == 2) {
			Get();
			int rgb = ParseColor(t.val);
			typ = IconType.Color;
			Emit(Op.PUSH, rgb, "#" + rgb.ToString("X6")); 
		} else if (la.kind == 5) {
			Get();
			Symbol sym = tab.Find(VarName(t.val), t);
			typ = sym.Type;
			Emit(Op.LOAD, sym.Slot, ":" + sym.Name); 
		} else if (la.kind == 40) {
			Get();
			Expr(out typ, out cv);
			Expect(41);
		} else if (la.kind == 37) {
			Get();
			op = t; 
			Factor(out typ, out cv);
			if (typ == IconType.Color) {
			 Error(op, "operadores aritméticos requieren números");
			 typ = IconType.Undef;
			}
			if (cv.HasValue) cv = -cv.Value;
			Emit(Op.NEG); 
		} else SynErr(48);
	}



	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		IconScript();
		Expect(0);

	}
	
	static readonly bool[,] set = {
		{_T,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x},
		{_x,_x,_x,_x, _x,_x,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _T,_T,_x,_x, _T,_T,_T,_T, _T,_T,_T,_x, _T,_T,_T,_T, _x,_x,_x,_x, _x,_x,_x,_x},
		{_x,_T,_T,_x, _x,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_T,_x,_x, _T,_x,_x,_x}

	};
} // end Parser


// [IconScript] Clase Errors MODIFICADA respecto del Parser.frame original de Coco/R.
// En lugar de solo imprimir en consola, guarda cada error como un Diagnostic
// { Line, Col, Message, Kind } para que la fachada (Compiler.cs), la CLI y la API
// puedan mostrarlos. Los mensajes sintacticos que genera Coco ("X expected") se
// traducen al espanol en la fachada.
public class Errors {
	public int count = 0;                                    // cantidad de errores detectados
	public readonly System.Collections.Generic.List<Diagnostic> List =
		new System.Collections.Generic.List<Diagnostic>();  // errores en orden de aparicion
	public readonly System.Collections.Generic.List<Diagnostic> Warnings =
		new System.Collections.Generic.List<Diagnostic>();
	public System.IO.TextWriter errorStream = null;          // eco opcional (null = no imprimir)
	public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=linea, 1=columna, 2=texto

	void Add (int line, int col, string s, string kind) {
		List.Add(new Diagnostic(line, col, s, kind));
		if (errorStream != null) errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}

	public virtual void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "number expected"; break;
			case 2: s = "color expected"; break;
			case 3: s = "ident expected"; break;
			case 4: s = "varDecl expected"; break;
			case 5: s = "varRef expected"; break;
			case 6: s = "\"template\" expected"; break;
			case 7: s = "\"fd\" expected"; break;
			case 8: s = "\"forward\" expected"; break;
			case 9: s = "\"bk\" expected"; break;
			case 10: s = "\"back\" expected"; break;
			case 11: s = "\"rt\" expected"; break;
			case 12: s = "\"right\" expected"; break;
			case 13: s = "\"lt\" expected"; break;
			case 14: s = "\"left\" expected"; break;
			case 15: s = "\"pu\" expected"; break;
			case 16: s = "\"penup\" expected"; break;
			case 17: s = "\"pd\" expected"; break;
			case 18: s = "\"pendown\" expected"; break;
			case 19: s = "\"arc\" expected"; break;
			case 20: s = "\"moveto\" expected"; break;
			case 21: s = "\"repeat\" expected"; break;
			case 22: s = "\"[\" expected"; break;
			case 23: s = "\"]\" expected"; break;
			case 24: s = "\"mirror\" expected"; break;
			case 25: s = "\"color\" expected"; break;
			case 26: s = "\"width\" expected"; break;
			case 27: s = "\"palette\" expected"; break;
			case 28: s = "\"shadow\" expected"; break;
			case 29: s = "\"background\" expected"; break;
			case 30: s = "\"fill\" expected"; break;
			case 31: s = "\"gradient\" expected"; break;
			case 32: s = "\"circle\" expected"; break;
			case 33: s = "\"rect\" expected"; break;
			case 34: s = "\"make\" expected"; break;
			case 35: s = "\"noautofit\" expected"; break;
			case 36: s = "\"+\" expected"; break;
			case 37: s = "\"-\" expected"; break;
			case 38: s = "\"*\" expected"; break;
			case 39: s = "\"/\" expected"; break;
			case 40: s = "\"(\" expected"; break;
			case 41: s = "\")\" expected"; break;
			case 42: s = "??? expected"; break;
			case 43: s = "invalid Statement"; break;
			case 44: s = "invalid Move"; break;
			case 45: s = "invalid Turn"; break;
			case 46: s = "invalid Pen"; break;
			case 47: s = "invalid Paint"; break;
			case 48: s = "invalid Factor"; break;

			default: s = "error " + n; break;
		}
		Add(line, col, s, DiagnosticKind.Sintactico);
	}

	// Error lexico: 'text' es el lexema que no corresponde a ningun token.
	public virtual void LexErr (int line, int col, string text) {
		Add(line, col, "s\u00edmbolo inv\u00e1lido '" + text + "'", DiagnosticKind.Lexico);
	}

	public virtual void SemErr (int line, int col, string s) {
		Add(line, col, s, DiagnosticKind.Semantico);
	}

	public virtual void SemErr (string s) {
		Add(0, 0, s, DiagnosticKind.Semantico);
	}

	public virtual void Warning (int line, int col, string s) {
		Warnings.Add(new Diagnostic(line, col, s, DiagnosticKind.Semantico));
	}

	public virtual void Warning(string s) {
		Warnings.Add(new Diagnostic(0, 0, s, DiagnosticKind.Semantico));
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}
}