# Bitácora de uso de IA generativa

Registro de dónde y cómo usamos IA en el trabajo final. Alimenta el apartado
"Reflexiones sobre el uso de la IA generativa" del informe.

## Cómo trabajamos

El diseño del lenguaje y del compilador es nuestro:
- la sintaxis de IconScript;
- las reglas semánticas;
- el código intermedio y la máquina virtual;
- la geometría del ARC;
- las plantillas;
- las interfaces;
- el orden de las fases.

Le entregamos este diseño a **Claude Code** (Anthropic) como especificación y lo usamos para
implementar el código a partir de él.

En cada fase, los bloques **Prompt** reproducen las indicaciones que dictaron
esa parte del trabajo. Son las instrucciones que efectivamente recibió la IA, escritas por
nosotros. Debajo se registra qué generó la IA, qué errores detectamos y qué conceptos de la
materia aplica.

---

### Fase 0 -- Setup -- 2026-11-02

- **Herramienta:** Claude Code (Anthropic)
- **Prompts:**
  > Usá Coco/R para C# para generar el scanner y el parser. Preferimos la opción
  > multiplataforma: descargá CocoSourcesCS.zip de la JKU y creá `tools/CocoR/CocoR.csproj`
  > (consola) para correr Coco con dotnet.

  > Coco necesita `-namespace IconScript` para que el código generado compile con el resto.
  > Silenciá los warnings de nulabilidad del código generado con `<Nullable>disable</Nullable>`
  > en el Core. Los conflictos LL(1) que informe Coco hay que analizarlos y documentarlos, no
  > ignorarlos.

  > Agregá al `IconScript.Core.csproj` un target BeforeBuild que ejecute Coco solo si el .ATG
  > cambió (Inputs/Outputs), y también los scripts `scripts/gen.ps1` y `scripts/gen.sh`. Nunca
  > edites a mano `Scanner.cs` ni `Parser.cs`.

  > Modificá las copias de `Scanner.frame`/`Parser.frame` en `tools/coco/` para que la clase
  > `Errors` guarde una `List<Diagnostic>` con `{ Line, Col, Message, Kind }` en vez de solo
  > imprimir. El scanner tiene que poder construirse desde un Stream, para compilar el texto
  > de la web sin archivos.

- **Qué generó la IA:**
  - la solución `IconScript.sln` (Core, Cli, Api, Tests);
  - `tools/CocoR/` compilando las fuentes oficiales sin modificarlas;
  - `Parser.frame` modificado;
  - el target `GenerarParserConCoco`;
  - los scripts de regeneración;
  - tests de humo.
- **Qué revisó y cambió el grupo:** _(completar)_
- **Errores de la IA detectados:**
  - La URL de descarga que pusimos en el plan daba 404. La IA encontró la correcta:
    `https://ssw.jku.at/Research/Projects/Coco/CS/CocoSourcesCS.zip`.
  - En la PC había .NET 10 y no .NET 8. Se mantuvo `net8.0` con `RollForward=Major`.
- **Conceptos de la materia:**
  - generador de compiladores a partir de una gramática atribuida;
  - frames como plantillas del código generado;
  - recuperación de errores (`noSym`, `minErrDist`).

### Fase 1 -- Léxico + sintaxis -- 2026-11-02

- **Herramienta:** Claude Code (Anthropic)
- **Prompts:**
  > Tokens: `number = digit {digit} ["." digit {digit}]`, `color = '#'` y seis hex,
  > `ident = letter {letter | digit | '_'}`, `varDecl` con comilla doble (`"lado`) y `varRef`
  > con dos puntos (`:lado`). Comentarios `//` hasta fin de línea y `/* */` anidados. Ignorar
  > cr, lf y tab, e IGNORECASE para que FD = fd = Fd.

  > Gramática de referencia: `IconScript = [TemplateDecl] {Statement}`. Los comandos son Move,
  > Turn, Pen, Arc, MoveTo, Repeat, Mirror, ColorCmd, WidthCmd, PaletteCmd, ShadowCmd,
  > Background, Fill, Circle, Rect, Make y NoAutofit. Las expresiones van con
  > Expr/Term/Factor, menos unario y paréntesis.

  > Hay un conflicto LL(1) conocido: en `ARC 30 -180` el `-` puede continuar la primera
  > expresión o empezar la segunda. Coco elige la resta. Aceptamos ese comportamiento (como el
  > else colgante de Taste), documentamos que un segundo argumento negativo se escribe entre
  > paréntesis, `ARC 30 (-180)`, y agregamos un test que lo verifique. Cualquier otro warning
  > LL(1) se elimina o se justifica.

  > Traducí los mensajes sintácticos de Coco ("X expected") a "se esperaba X" en la fachada.

- **Qué generó la IA:**
  - `IconScript.ATG` completo;
  - la traducción de mensajes en `Compiler.cs`;
  - `LexSyntaxTests.cs`;
  - los programas de `examples/errores/`.
- **Qué revisó y cambió el grupo:** _(completar)_
- **Errores de la IA detectados:**
  - Acentos doble-codificados en los mensajes (`sÃ­mbolo`). Coco lee los frames byte a byte
    como Latin-1 y escribe UTF-8. Se resolvió con un frame solo ASCII (escapes `í`) y el
    `.ATG` con BOM UTF-8.
  - La IA cayó en el conflicto LL(1) al escribir sus propios ejemplos y tests: `MOVETO 0 -220`
    se leyó como una resta. Se corrigió con paréntesis, lo que confirma la decisión
    documentada.
- **Decisión de implementación a discutir:** `TEMPLATE` quedó como alternativa de `Statement`
  y la regla "una vez y al inicio" se verifica en el análisis semántico. Así el mensaje de
  error es claro y no se agrega ningún conflicto LL(1) (ver `diseno.md` 3.2).
- **Conceptos de la materia:**
  - DFA del scanner y palabras clave como literales;
  - LL(1), conjuntos First/Follow y el conflicto "start & successor of deletable structure";
  - recuperación de errores.

### Fase 2 -- Semántica + tabla de símbolos -- 2026-11-02

- **Herramienta:** Claude Code (Anthropic)
- **Prompts:**
  > Hay dos tipos, Number y Color. Cada Expr devuelve su tipo como atributo de salida:
  > `Expr<out IconType t>`.

  > Reglas:
  > - `+ - * /` y el menos unario solo entre números;
  > - FD, BK, RT, LT, WIDTH, ARC, MOVETO, REPEAT, CIRCLE y RECT requieren número;
  > - COLOR, BACKGROUND, FILL y GRADIENT requieren color;
  > - una variable `:x` tiene que estar declarada antes de usarse;
  > - una variable no cambia de tipo al reasignarse;
  > - TEMPLATE solo puede ser app, file o badge, una vez y al inicio;
  > - PALETTE solo acepta nombres existentes.

  > Verificá las constantes en compilación: radio > 0, WIDTH > 0, REPEAT >= 0 y división por
  > cero. Si el valor depende de variables, se verifica en ejecución.

  > Tabla de símbolos con el modelo de la materia: `Symbol { Name, Type, Slot }`, `Insert`,
  > `Find` (que devuelve undefSym y reporta el error, para evitar cascadas) y `Lookup` sin
  > error. Un solo scope global, con OpenScope/CloseScope implementados como extensión futura.

  > `PALETTE ocean` inserta o reasigna primary, secondary, accent, light y dark como Color.
  > Cinco paletas (ocean, sunset, forest, candy, mono) con colores armónicos tipo Tailwind.

- **Qué generó la IA:**
  - los atributos `<out IconType typ, out double? cv>`, donde el valor constante sirve para
    las verificaciones en compilación;
  - `Types.cs`, `SymbolTable.cs` y `Palettes.cs`;
  - `SemanticTests.cs`.
- **Qué revisó y cambió el grupo:** _(completar)_
- **Errores de la IA detectados:** _(completar)_
- **Decisiones de implementación a discutir:**
  - los nombres de variables no distinguen mayúsculas, coherente con IGNORECASE;
  - FILL no se puede anidar.
- **Conceptos de la materia:**
  - atributos sintetizados y chequeo de tipos;
  - plegado de constantes;
  - tipo `Undef` contra los errores en cascada;
  - tabla de símbolos con scopes.

### Fase 3 -- Código intermedio -- 2026-11-02

- **Herramienta:** Claude Code (Anthropic)
- **Prompts:**
  > Representá el código intermedio como `List<Instr>` con `Instr { Op, Arg }`. La dirección
  > de una instrucción es su índice. Los valores de la pila son double y los colores se
  > codifican como 0xRRGGBB.

  > `Emit(op[, arg])` devuelve la dirección y `Patch(addr, target)` completa los saltos hacia
  > adelante. `Listing()` devuelve el texto `0: PUSH 8 ...` para `iconc -code` y la pestaña de
  > la web.

  > REPEAT emite `LOOPBEGIN 0` y guarda la dirección. Después genera el cuerpo, emite
  > `LOOPEND inicioCuerpo` y hace Patch del LOOPBEGIN con la dirección siguiente. Es el mismo
  > esquema que el while de Taste (FJMP + Patch); comentalo así en el código.

  > BK se traduce como NEG + MOVE y LT como NEG + TURN. La plantilla y NOAUTOFIT son
  > metadatos y quedan fuera del IR.

- **Qué generó la IA:**
  - `CodeGen.cs`;
  - la emisión desde las acciones del `.ATG`;
  - `iconc -code`;
  - `CodeGenTests.cs` con listados golden.
- **Qué revisó y cambió el grupo:** _(completar)_
- **Errores de la IA detectados:** _(completar)_
- **Conceptos de la materia:**
  - traducción dirigida por la sintaxis;
  - código postfijo para una máquina de pila;
  - backpatching.

### Fases 4 y 5 -- VM + SVG mínimo, geometría -- 2026-11-02

- **Herramienta:** Claude Code (Anthropic)
- **Prompts:**
  > Estado de la VM: x e y (origen en el centro, y hacia arriba), heading en grados (0 = norte,
  > RT suma en sentido horario), penDown, color #000000, width 8, mirror y shadow. Estructuras:
  > pila de valores, pila de contadores de bucle, pila de estados guardados para MIRROR y
  > arreglo de variables.

  > Los trazos consecutivos con el mismo estilo y el lápiz bajo se acumulan en el mismo
  > StrokePath. Dentro de FILL, todo movimiento se agrega al camino del relleno, que se cierra
  > con Z. Límite de seguridad: 1.000.000 de instrucciones y 50.000 formas, si no, "programa
  > demasiado largo".

  > ARC r g: el centro está a la derecha si g > 0 y a la izquierda si g < 0. El punto final se
  > obtiene rotando alrededor del centro y la nueva dirección es h + g. En SVG, el sweep
  > depende del signo de g. Si |g| >= 360, se parte en arcos más chicos. El bounding box
  > incluye los extremos y los puntos cardinales dentro del barrido.

  > Tests de geometría obligatorios:
  > - `REPEAT 4 [FD 100 RT 90]` vuelve al origen con la misma dirección;
  > - `ARC 50 360` vuelve al origen;
  > - `ARC 50 90` deja la tortuga en (50, 50) con dirección 90°.

- **Qué generó la IA:**
  - `TurtleVM.cs` y `Drawing.cs`;
  - `Geometry.cs` (ARC, bounding box, reflexión, rectángulo rotado);
  - `SvgWriter.cs`;
  - `GeometryTests.cs`.
- **Qué revisó y cambió el grupo:** _(completar)_
- **Errores de la IA detectados:**
  - En `ola.icon` la IA calculó mal el sentido de los semicírculos y las olas salían
    verticales. Se detectó revisando los SVG y se corrigió.
  - En `corazon.icon` el arco giraba al revés. Se cambió a `ARC 124 225`, con el radio
    calculado como r = 212 / (1 + √2/2).
- **Conceptos de la materia:**
  - máquina de pila e intérprete de código intermedio;
  - pila de bucles y pila de estados;
  - errores de ejecución.

### Fase 6 -- Diseño de íconos -- 2026-11-02

- **Herramienta:** Claude Code (Anthropic)
- **Prompts:**
  > Plantillas de 1024×1024:
  > - app: cuadrado con rx = 22,5 % y zona segura del 60 %;
  > - file: hoja con la esquina doblada y zona del 55 % desplazada hacia abajo;
  > - badge: círculo con zona del 65 %.

  > Autofit: al terminar, calculá el bounding box de todas las formas sin el fondo, trasladá
  > para centrarlo y escalá solo hacia abajo si se pasa de la zona segura. NOAUTOFIT lo
  > desactiva.

  > SVG con viewBox 0 0 1024 1024. En `<defs>` van el clipPath con la forma de la plantilla, un
  > linearGradient diagonal a 135° por degradado y un feDropShadow si hay SHADOW. Los trazos
  > son redondeados y los números llevan como máximo 2 decimales. Tiene que ser
  > determinístico, para los tests snapshot.

  > Ejemplos: flor, estrella, archivo-iconscript, cuadrado, círculo, corazón, ola y espiral con
  > variables. Íconos originales, nada de logos de marcas.

- **Qué generó la IA:**
  - `Templates.cs`;
  - el autofit;
  - BACKGROUND, GRADIENT, PALETTE, SHADOW, FILL y MIRROR;
  - 12 ejemplos;
  - los snapshots SVG.
- **Qué revisó y cambió el grupo:** _(completar)_
- **Errores detectados:**
  - Nuestro ejemplo `flor.icon` dibujaba solo 4 pétalos diminutos: la dirección avanzaba 270°
    por pétalo y el autofit solo achica. Se cambió a `ARC 300 45 … RT 180`, que avanza 45° por
    pétalo y da 8.
  - La hoja de `file` no se veía sobre fondo blanco; se le agregó un contorno suave.
- **Conceptos de la materia:** generación de la salida final del back-end y transformación de
  coordenadas.

### Fase 7 -- CLI completa -- 2026-11-02

- **Herramienta:** Claude Code (Anthropic)
- **Prompts:**
  > `iconc flor.icon` genera flor.svg, `-o` elige la salida, `-code` imprime el código
  > intermedio y `-open` abre el SVG.

  > Arrastrar un .icon sobre iconc.exe compila y abre automáticamente; si hubo errores, la
  > consola queda en pausa. Errores con el formato "línea 3, col 5: se esperaba un número" y
  > código de salida distinto de 0.

- **Qué generó la IA:** `src/IconScript.Cli/Program.cs`. Para detectar el arrastrar y soltar
  usa `GetConsoleProcessList`.
- **Qué revisó y cambió el grupo:** _(completar; probar el arrastrar y soltar en Windows)_
- **Errores de la IA detectados:** _(completar)_

### Fases 8 y 9 -- API, editor web y exportación -- 2026-11-02

- **Herramienta:** Claude Code (Anthropic)
- **Prompts:**
  > API con ASP.NET Minimal: `POST /compile` devuelve el CompileResult en JSON y
  > `GET /examples` la lista de ejemplos. CORS para localhost:5173 y tiempo máximo de 2 s por
  > compilación.

  > Editor web con Vite + React + TypeScript:
  > - CodeMirror 6 con resaltado de IconScript y subrayado de la línea con error;
  > - vista previa con máscaras (iOS, Android círculo, Android squircle) y modo "pantalla de
  >   inicio";
  > - panel de errores clickeable;
  > - pestañas de código intermedio y ayuda;
  > - compilación en vivo con debounce de 300 ms.

  > Exportación en el navegador: PNG de 16 a 1024, y un paquete de app con icon.png,
  > adaptive-icon.png (solo el primer plano), los mipmaps de Android y el AppIcon.appiconset de
  > iOS. Antes de implementarlo, verificá los tamaños vigentes de Android, iOS y Expo.

- **Qué generó la IA:**
  - la API;
  - `web/`: el resaltado en `iconscriptLang.ts`, el editor y `exporter.ts`.
- **Qué revisó y cambió el grupo:** _(completar)_
- **Errores de la IA detectados:** `Array.prototype.toSpliced` no estaba disponible con
  `lib: ES2020` y se reemplazó.
- **Verificación:**
  - Los tamaños se consultaron en la documentación de Expo: icon y adaptive-icon de
    1024×1024, mipmaps de 48 a 192 e iOS con una sola imagen de 1024.
  - En el navegador se comprobó que el ZIP tiene esa estructura.

### Fase 10 -- Cierre -- 2026-11-02

- **Herramienta:** Claude Code (Anthropic)
- **Prompts:**
  > Documentá en `docs/diseno.md` y `docs/gramatica-ebnf.md`: tokens, gramática, conflictos
  > LL(1) y su resolución, tabla de símbolos, IR, VM, geometría del ARC y plantillas.

  > El README tiene que ser un manual de uso: instalación, CLI, web y referencia del lenguaje
  > con ejemplos.

- **Qué generó la IA:**
  - `docs/diseno.md` y `docs/gramatica-ebnf.md`;
  - el README con la guía "Cómo se usa".
- **Qué revisó y cambió el grupo:** _(completar)_
- **Pendiente:** capturas para el informe y el video.
