# Documento de diseño — IconScript

IconScript es un lenguaje de dibujo tipo Logo para diseñar íconos (apps, archivos, badges).
El compilador traduce un programa `.icon` a código intermedio para una máquina de pila, lo
ejecuta en una "máquina virtual de la tortuga" y genera un SVG.

```
fuente .icon ──► Scanner ──► Parser LL(1) + acciones semánticas ──► código intermedio
                (Coco/R)     (tabla de símbolos, tipos, CodeGen)        (List<Instr>)
                                                                             │
                     SVG ◄── SvgWriter (autofit) ◄── Drawing ◄── TurtleVM ◄──┘
```

| Etapa | Archivo |
|---|---|
| Gramática atribuida (fuente de scanner y parser) | `src/IconScript.Core/IconScript.ATG` |
| Scanner y parser **generados** | `Scanner.cs`, `Parser.cs` |
| Tipos y tabla de símbolos | `Types.cs`, `SymbolTable.cs`, `Palettes.cs` |
| Código intermedio | `CodeGen.cs` |
| Máquina virtual | `TurtleVM.cs`, `Drawing.cs` |
| Geometría | `Geometry.cs` |
| Plantillas y SVG | `Templates.cs`, `SvgWriter.cs` |
| Fachada | `Compiler.cs` (`IconCompiler.Compile`) |

---

## 1. Herramientas y generación del parser

- **Coco/R para C#** (versión 19/04/2011) compilado desde las fuentes oficiales de la JKU
  (`tools/CocoR/`, sin modificar su código). Se ejecuta con
  `dotnet tools/CocoR/bin/<Config>/net8.0/Coco.dll`.
- `IconScript.ATG` es la única fuente del scanner y del parser. `Scanner.cs` y `Parser.cs`
  son **generados** y nunca se editan a mano (se commitean igual).
- El target MSBuild `GenerarParserConCoco` (en `IconScript.Core.csproj`) corre Coco antes de
  compilar, solo si el `.ATG` o los frames cambiaron (`Inputs`/`Outputs`). Los avisos
  `LL1 warning` de Coco aparecen como warnings del build. Alternativa manual:
  `scripts/gen.ps1` / `scripts/gen.sh`.
- Opciones: `-namespace IconScript` (para que el código generado compile con el resto) y
  `-frames tools/coco` (frames modificados).

### 1.1 Frames modificados (`tools/coco/`)

`Scanner.frame` es el original. `Parser.frame` tiene dos cambios, marcados con `[IconScript]`:

1. **Clase `Errors`**: en lugar de imprimir, acumula `Diagnostic { Line, Col, Message, Kind }`
   en `Errors.List`. `SynErr` → `Sintactico`, `SemErr` → `Semantico` y un método nuevo
   `LexErr` → `Lexico`. Los warnings se guardan aparte en `Errors.Warnings`.
2. **`Get()`**: cuando el scanner devuelve el token especial `noSym` (kind `maxT`, el terminal
   `???` de Coco: una secuencia de caracteres que no forma ningún token), se reporta un
   **error léxico** y se descarta el token. Así el parser continúa sin un error sintáctico en
   cascada.

El frame es solo ASCII: Coco lo copia byte a byte (como Latin-1) y escribe la salida en UTF-8,
así que un acento en el frame saldría doble-codificado. Los acentos del mensaje léxico se
escriben con escapes `í`. El `.ATG` sí lleva acentos: tiene BOM UTF-8, que el scanner
de Coco reconoce.

### 1.2 Lectura desde memoria

El scanner se construye con `new Scanner(Stream)`. La fachada antepone el BOM de UTF-8 al
`MemoryStream` para que Coco use su `UTF8Buffer`; así se compila el texto recibido por la API
sin crear archivos.

### 1.3 Versión de .NET

Los proyectos apuntan a `net8.0` con `RollForward=Major`: corren con .NET 8 o con un runtime
más nuevo (en la máquina de desarrollo solo había .NET 10).

---

## 2. Análisis léxico

Los tokens se definen en la sección `TOKENS` del `.ATG` (ver `gramatica-ebnf.md`). Coco/R
construye un autómata finito determinista a partir de ellos.

- `IGNORECASE`: las palabras clave se reconocen sin distinguir mayúsculas. Coco las trata como
  *literales*: el DFA reconoce un `ident` y después lo busca en la tabla de palabras clave.
- `number`: si después del punto no hay dígitos (`100.`), el scanner vuelve al último estado
  final (`100`) y el punto queda como símbolo inválido.
- Errores léxicos: `@`, `#12G` (color incompleto) → `símbolo inválido '...'`.

---

## 3. Análisis sintáctico y conflictos LL(1)

Parser descendente recursivo LL(1) generado por Coco/R: un método por no terminal, decisiones
con un símbolo de anticipación (`la`).

### 3.1 Conflicto conocido: el `-` en comandos con dos expresiones

Coco/R informa un único warning:

```
LL1 warning in Expr: "-" is start & successor of deletable structure
```

En `Expr = Term { ("+" | "-") Term }` la iteración es *borrable* (puede repetirse cero veces).
En `ARC Expr Expr` lo que sigue a la primera `Expr` es otra `Expr`, cuyo First incluye `-`
(menos unario). Entonces, al ver `-` después de `ARC 30`, el parser no sabe si:

- **continuar** la primera expresión (resta: `30 - 180`), o
- **terminarla** y empezar la segunda (negativo: `-180`).

Coco/R resuelve como en el *else colgante* de Taste: elige la primera alternativa, es decir,
**seguir la iteración** (resta). Decisión: se acepta el comportamiento por defecto y se
documenta que **un segundo argumento negativo se escribe entre paréntesis**:
`ARC 30 (-180)`, `MOVETO 0 (-220)`.

Afecta a `ARC`, `MOVETO`, `RECT` y `GRADIENT`. El test
`LexSyntaxTests.ConflictoLL1_MenosEnSegundoArgumento` lo verifica. Ejemplo real: al escribir
`corazon.icon`, `MOVETO 0 -220 PD` dio *"se esperaba una expresión"*, porque se leyó como
`MOVETO (0 - 220) <falta>`.

Alternativas descartadas: exigir una coma entre argumentos (cambia el lenguaje de referencia),
o un resolvedor `IF(...)` de Coco que mire el espacio en blanco (frágil y difícil de defender).

### 3.2 TEMPLATE

`TEMPLATE` es una alternativa de `Statement` (ver `gramatica-ebnf.md`). Con la forma del plan
(`[TemplateDecl] {Statement}`) un `TEMPLATE` en el medio daría un error sintáctico genérico;
con esta forma se verifica semánticamente y el mensaje es claro. No agrega conflictos: First
de `TemplateDecl` es `{TEMPLATE}`, disjunto del resto.

### 3.3 Warning `IconScript deletable`

Coco/R informa que `IconScript` es borrable (deriva la cadena vacía). Es correcto: el
programa vacío es válido y dibuja un ícono solo con el fondo.

### 3.4 Recuperación de errores

Se usa la recuperación por defecto de Coco/R. `minErrDist = 2` evita reportar errores
sintácticos demasiado próximos, y los tokens inválidos se descartan en `Get()`. La fachada
traduce los mensajes de Coco: `"X expected"` → `se esperaba X`, `invalid Statement` →
`comando inválido`, `invalid Factor` → `se esperaba una expresión`.

---

## 4. Análisis semántico

### 4.1 Atributos

Traducción dirigida por la sintaxis en **una sola pasada**. Cada expresión sintetiza dos
atributos de salida:

```
Expr<out IconType typ, out double? cv>
```

- `typ`: el tipo (`Number`, `Color` o `Undef`).
- `cv`: el valor si la expresión es constante, o `null` si depende de variables. Se usa para
  detectar en compilación radios `<= 0`, divisiones por cero, etc.

`Undef` aparece cuando ya hubo un error (por ejemplo, una variable no declarada). Las reglas
no vuelven a reportar sobre un operando `Undef`, así se evitan los errores en cascada:
`FD :x * 2 + 1` da **un** error.

### 4.2 Reglas

| Regla | Mensaje |
|---|---|
| `+ - * /` y `-` unario solo entre números | operadores aritméticos requieren números |
| FD BK RT LT WIDTH ARC MOVETO REPEAT CIRCLE RECT requieren número | se esperaba un número |
| COLOR, BACKGROUND, FILL, GRADIENT requieren color | se esperaba un color |
| `:x` declarada antes de usarse | variable 'x' no declarada |
| una variable no cambia de tipo | 'x' es de tipo color, no se le puede asignar un número |
| TEMPLATE: app, file o badge, una vez y al inicio | plantilla desconocida 'x' / TEMPLATE solo puede aparecer una vez, al inicio del programa |
| PALETTE: nombres existentes | paleta desconocida 'x' |
| constantes: radio > 0, WIDTH > 0, REPEAT ≥ 0, RECT > 0 | el radio debe ser mayor que 0, … |
| división por cero con constantes | división por cero |
| FILL no se anida | no se puede anidar un FILL dentro de otro FILL |

Si el valor depende de variables, la verificación se hace en ejecución (`TurtleVM`).

`MAKE "x expr` inserta el símbolo **después** de analizar `expr`. Por eso `MAKE "x :x + 1` es
un error si `x` no existía.

### 4.3 Tabla de símbolos

`SymbolTable.cs` sigue el modelo de la materia:

- `Symbol { Name, Type, Slot }`: `Slot` es el índice en el arreglo de variables de la VM
  (operando de `LOAD`/`STORE`).
- `Insert(name, type)`, `Lookup(name)` (sin error) y `Find(name, token)`. `Find` reporta
  *variable no declarada* y devuelve `undefSym` (tipo `Undef`) para evitar cascadas.
- Pila de scopes con un único scope global. `OpenScope`/`CloseScope` quedan implementados para
  una extensión futura con procedimientos `TO ... END`.
- Los nombres no distinguen mayúsculas, coherente con `IGNORECASE`.
- `PALETTE ocean` inserta o reasigna `primary`, `secondary`, `accent`, `light` y `dark` (tipo
  `Color`), emitiendo `PUSH color` + `STORE slot` por cada una.

Paletas (`Palettes.cs`): `ocean`, `sunset`, `forest`, `candy` y `mono`, con colores de escalas
tipo Tailwind.

---

## 5. Código intermedio

`List<Instr>` con `Instr { Op, Arg, Line, Col, Comment }`. La dirección de una instrucción es
su índice. `Line`/`Col` permiten ubicar los errores de ejecución y `Comment` solo sirve para
el listado.

Todos los valores de la pila son `double`. Los colores se codifican como `0xRRGGBB`: el tipo
ya se verificó en compilación, así que la VM no necesita etiquetas de tipo.

Expresiones en **notación postfija**: primero los operandos, después el operador.
`FD 1 + 2 * 3` → `PUSH 1, PUSH 2, PUSH 3, MUL, ADD, MOVE`.

Traducciones que no necesitan instrucciones propias: `BK d` = `d, NEG, MOVE` y
`LT g` = `g, NEG, TURN`.

### 5.1 REPEAT y backpatching

```
        <código de n>
  L:    LOOPBEGIN ?        ; salto hacia adelante: el destino todavía no se conoce
  B:    <cuerpo>
        LOOPEND B          ; salto hacia atrás: B ya se conoce
  fin:                     ; Patch(L, fin)
```

Es el mismo esquema que el `while` de Taste (`FJMP` + `Patch`). Ejemplo real (`estrella.icon`):

```
 6: PUSH 5
 7: LOOPBEGIN 13      ; completado por Patch
 8: PUSH 300
 9: MOVE
10: PUSH 144
11: TURN
12: LOOPEND 8
13: HALT
```

### 5.2 MIRROR

```
MIRRORBEGIN          ; guarda el estado de la tortuga
B: <cuerpo>
   MIRROREND B       ; 1ª vez: restaura el estado, activa el espejo y salta a B; 2ª vez: lo desactiva
```

Los metadatos (plantilla, `NOAUTOFIT`) no forman parte del IR: se guardan en `IconProgram`.

---

## 6. Máquina virtual (`TurtleVM.cs`)

- **Estado**: `x`, `y` (origen en el centro, y hacia arriba), `heading` (0 = norte, sentido
  horario positivo), `penDown` (al inicio `true`), `color` (`#000000`), `width` (8), `mirror` y
  `shadow`.
- **Estructuras**: pila de valores, pila de contadores de bucle, pila de estados guardados
  (MIRROR) y arreglo de variables.
- **Salida (`Drawing`)**: fondo y una lista de `Shape`: caminos con trazo, caminos rellenos y
  círculos. Los rectángulos se emiten como caminos de 4 lados.
- Los trazos consecutivos con el mismo color y grosor y el lápiz bajo se acumulan en el mismo
  camino. `PU`, `MOVETO`, un cambio de estilo, `CIRCLE`, `RECT` o `FILL` cierran el camino
  actual.
- **FILL**: dentro del bloque, todo movimiento (con el lápiz bajo o alto) se agrega al camino
  del relleno, que se cierra con `Z` en `FILLEND`. `CIRCLE` y `RECT` dentro de un `FILL` se
  rellenan con esa pintura. `MOVETO` dentro de un `FILL` abre un subcamino (`M`).
- **MIRROR**: en modo espejo se invierten `TURN` y el sentido de `ARC`, lo que equivale a
  reflejar respecto del eje de la dirección inicial del bloque. `MOVETO`, que es absoluto,
  también se refleja: se aplican los espejos activos del más externo al más interno. Al
  terminar, la tortuga queda donde la dejó la pasada reflejada.
- **REPEAT** con un valor no entero usa la parte entera (`floor`).
- **Límites**: 1.000.000 de instrucciones, 50.000 formas y 500.000 tramos. Si se supera alguno
  → *programa demasiado largo*. Además, un token de cancelación permite cortar a los 2 s en la
  API.
- **Errores de ejecución**: división por cero, radio, grosor o dimensiones `<= 0` calculados, y
  repeticiones negativas, con la línea y columna de la instrucción.

---

## 7. Geometría del ARC (`Geometry.cs`)

`ARC r g` desde `P` con dirección `h`:

1. `adelante = (sin h, cos h)` y `derecha = (cos h, −sin h)`.
2. El centro es `C = P + r·derecha` si `g > 0` (gira a la derecha) y `C = P − r·derecha` si
   `g < 0`.
3. El punto final es `P` rotado alrededor de `C` `g` grados en sentido horario. En ángulos
   matemáticos (antihorarios) eso es `α = α₀ − g`, con `α₀ = atan2(P − C)`.
4. La nueva dirección es `h + g`.
5. **SVG**: `A r r 0 largeArc sweep x y`, con `sweep = 1` si `g > 0`. Como la pantalla tiene
   el eje y invertido, el sentido visual se conserva.
6. **Arcos largos**: se parten en tramos de **a lo sumo 120°**. Un único `A` no puede dibujar
   un círculo completo (los extremos coinciden) y con 180° exactos el arco es ambiguo. Con
   tramos ≤ 120°, `largeArc` es siempre 0.
7. **Bounding box**: los extremos de cada tramo más los puntos cardinales (0°, 90°, 180°, 270°)
   del círculo que caen estrictamente dentro del barrido.

Verificación (`ARC 50 90` desde `(0,0)` mirando al norte): `C = (50, 0)`, `α₀ = 180°`,
`α = 90°` → `(50, 50)`, dirección `90°`. Lo comprueba `GeometryTests`, junto con
`REPEAT 4 [FD 100 RT 90]` y `ARC 50 360`, que vuelven al origen.

---

## 8. Plantillas y autofit (`Templates.cs`, `SvgWriter.cs`)

| Plantilla | Lienzo | Forma de fondo / máscara | Zona segura |
|---|---|---|---|
| app (por defecto) | 1024×1024 | cuadrado con esquinas redondeadas (rx = 22,5 %) | 60 % central |
| file | 1024×1024 | hoja con la esquina superior derecha doblada | 55 %, desplazada hacia abajo (centro y = 590) |
| badge | 1024×1024 | círculo | 65 % central |

**Autofit**: al terminar la ejecución se calcula la caja envolvente de todas las formas (sin
el fondo, incluyendo medio grosor de los trazos). Se traslada para centrarla en la zona segura
y se escala **solo hacia abajo** si no entra. `NOAUTOFIT` deja el origen de la tortuga en el
centro del lienzo con escala 1. La transformación es lineal: `sx = tx + s·x`, `sy = ty − s·y`.
Se aplica a los puntos, los radios y los grosores, así el SVG no necesita `transform`.

**SVG**:
- `<defs>` con un `clipPath` con la forma de la plantilla, un `linearGradient` diagonal de 135°
  por cada degradado usado y un `filter` con `feDropShadow` si hay `SHADOW`.
- `<g id="background">` con la forma de la plantilla rellena (y el doblez en `file`).
- `<g id="foreground" clip-path="url(#clip)">` con el primer plano.
- Trazos con `stroke-linecap="round"`, `stroke-linejoin="round"` y `fill="none"`.
- Números con hasta 2 decimales, sin `-0`. La salida es determinística (tests snapshot).

Los ids `background` y `foreground` permiten que la web genere el ícono adaptativo de Android
(solo el primer plano) y la versión de fondo completo para iOS.

---

## 9. Errores y fachada

```csharp
public record Diagnostic(int Line, int Col, string Message, string Kind); // Lexico|Sintactico|Semantico|Ejecucion
public record CompileResult(bool Success, IReadOnlyList<Diagnostic> Errors,
                            string? IntermediateCode, string? Svg, string Template);
IconCompiler.Compile(string source)
```

Los errores se ordenan por posición. **Si hay errores de compilación no se ejecuta la VM ni se
genera SVG.** Si falla la ejecución, se devuelve el código intermedio (útil para depurar)
pero no el SVG.

---

## 10. Interfaces

- **CLI `iconc`**: `-o`, `-code` y `-open`. Arrastrar y soltar en Windows se detecta con
  `GetConsoleProcessList == 1` (la consola fue creada para el proceso): en ese caso compila,
  abre el SVG y pausa si hubo errores.
- **API**: `POST /compile`, `GET /examples`, CORS para `http://localhost:5173` y 2 s máximo por
  compilación.
- **Web** (Vite + React + TS):
  - editor CodeMirror 6 con resaltado propio y subrayado de errores;
  - vista previa con máscaras (iOS, Android círculo, Android squircle) y modo pantalla de inicio;
  - pestañas de código intermedio y ayuda;
  - exportación de PNG y del paquete de app en ZIP, toda en el navegador.

## 11. Fuera de alcance

Texto y tipografías, imágenes externas, procedimientos `TO ... END`, animaciones y edición
visual con el mouse. Los procedimientos requerirían scopes en la tabla de símbolos (ya
previstos) e instrucciones `CALL`/`RET` en la VM.
