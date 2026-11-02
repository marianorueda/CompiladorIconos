# Gramática de IconScript (EBNF, sin acciones semánticas)

Versión "limpia" de `src/IconScript.Core/IconScript.ATG`: mismos tokens y producciones,
sin atributos ni acciones.

## Tokens

```ebnf
letter  = 'A'..'Z' | 'a'..'z' .
digit   = '0'..'9' .
hex     = digit | 'A'..'F' | 'a'..'f' .

number  = digit { digit } [ "." digit { digit } ] .      (* 100   2.5      *)
color   = "#" hex hex hex hex hex hex .                  (* #4F46E5       *)
ident   = letter { letter | digit | "_" } .              (* ocean  app    *)
varDecl = '"' letter { letter | digit | "_" } .          (* "lado  (MAKE) *)
varRef  = ":" letter { letter | digit | "_" } .          (* :lado         *)
```

- Comentarios: `// ... fin de línea` y `/* ... */` (anidables).
- Se ignoran: espacio, tabulación, retorno de carro y salto de línea.
- `IGNORECASE`: las palabras clave no distinguen mayúsculas (`FD = fd = Fd`).
  Los nombres de variables tampoco (decisión semántica, ver `diseno.md`).

Palabras clave: `TEMPLATE FD FORWARD BK BACK RT RIGHT LT LEFT PU PENUP PD PENDOWN ARC
MOVETO REPEAT MIRROR COLOR WIDTH PALETTE SHADOW BACKGROUND GRADIENT FILL CIRCLE RECT
MAKE NOAUTOFIT`.

## Producciones

```ebnf
IconScript   = { Statement } .

Statement    = TemplateDecl
             | Move | Turn | Pen | Arc | MoveTo | Repeat | Mirror
             | ColorCmd | WidthCmd | PaletteCmd | ShadowCmd
             | Background | Fill | Circle | Rect | Make | NoAutofit .

TemplateDecl = "TEMPLATE" ident .                  (* app | file | badge; una vez y al inicio *)
Move         = ( "FD" | "FORWARD" | "BK" | "BACK" ) Expr .
Turn         = ( "RT" | "RIGHT" | "LT" | "LEFT" ) Expr .
Pen          = "PU" | "PENUP" | "PD" | "PENDOWN" .
Arc          = "ARC" Expr Expr .                   (* radio, grados *)
MoveTo       = "MOVETO" Expr Expr .                (* x, y relativos al centro *)
Repeat       = "REPEAT" Expr "[" { Statement } "]" .
Mirror       = "MIRROR" "[" { Statement } "]" .
ColorCmd     = "COLOR" Expr .
WidthCmd     = "WIDTH" Expr .
PaletteCmd   = "PALETTE" ident .
ShadowCmd    = "SHADOW" .
Background   = "BACKGROUND" Paint .
Fill         = "FILL" Paint "[" { Statement } "]" .
Paint        = "GRADIENT" Expr Expr | Expr .
Circle       = "CIRCLE" Expr .
Rect         = "RECT" Expr Expr .
Make         = "MAKE" varDecl Expr .
NoAutofit    = "NOAUTOFIT" .

Expr         = Term { ( "+" | "-" ) Term } .
Term         = Factor { ( "*" | "/" ) Factor } .
Factor       = number | color | varRef | "(" Expr ")" | "-" Factor .
```

### Diferencia con la gramática de referencia del plan

El plan propone `IconScript = [ TemplateDecl ] { Statement }`. Se implementó
`IconScript = { Statement }` con `TemplateDecl` como alternativa de `Statement`, y la regla
"una vez y al inicio" se verifica en el análisis semántico. Ventajas: un `TEMPLATE` fuera de
lugar recibe el mensaje claro *"TEMPLATE solo puede aparecer una vez, al inicio del programa"*
en lugar de un error sintáctico genérico, y la gramática sigue siendo LL(1) (ver
`diseno.md`, sección 3).

## Conjuntos First relevantes

| No terminal | First |
|---|---|
| `Statement` | `TEMPLATE FD FORWARD BK BACK RT RIGHT LT LEFT PU PENUP PD PENDOWN ARC MOVETO REPEAT MIRROR COLOR WIDTH PALETTE SHADOW BACKGROUND FILL CIRCLE RECT MAKE NOAUTOFIT` |
| `Expr`, `Term`, `Factor` | `number color varRef ( -` |
| `Paint` | `GRADIENT number color varRef ( -` |

Las alternativas de `Statement` empiezan con palabras clave distintas, las de `Factor` con
tokens distintos y las de `Paint` se distinguen por `GRADIENT`: todas son disjuntas.
