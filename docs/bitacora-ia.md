# Bitácora de uso de IA generativa

Registro obligatorio de dónde y cómo se usó IA en el trabajo final (una entrada por fase).
Alimenta el apartado "Reflexiones sobre el uso de la IA generativa" del informe.

### Fase 0 -- Setup -- 2026-11-02
- **Herramienta:** Claude Code (Anthropic)
- **Prompt(s) principales:** "Ejecutá este plan de implementación teniendo en cuenta todas las consideraciones" (con el archivo PLAN-IconScript adjunto).
- **Qué generó la IA:**
  - `IconScript.sln` con los proyectos `IconScript.Core`, `IconScript.Cli` (`iconc`), `IconScript.Api` y `tests/IconScript.Tests` (xUnit); `Directory.Build.props`.
  - `tools/CocoR/CocoR.csproj`: compila Coco/R (C#, 19/04/2011) desde `CocoSourcesCS.zip` oficial, sin modificar su código.
  - `tools/coco/Parser.frame` **modificado** (clase `Errors` con `List<Diagnostic>`, errores léxicos en `Get()`).
  - Target MSBuild `GenerarParserConCoco` (Inputs/Outputs) + `scripts/gen.ps1` y `scripts/gen.sh`.
- **Qué revisó y cambió el grupo:** _(completa el grupo)_
- **Errores de la IA detectados:**
  - La URL de descarga del plan (`.../Coco/CocoSourcesCS.zip`) da 404; la correcta es `https://ssw.jku.at/Research/Projects/Coco/CS/CocoSourcesCS.zip`.
  - En la máquina no había .NET 8 sino .NET 10: se mantuvo `net8.0` con `RollForward=Major`.
- **Conceptos de la materia aplicados:** generador de compiladores a partir de una gramática atribuida; frames como plantillas del código generado; recuperación de errores (`noSym`, `minErrDist`).

### Fase 1 -- Léxico + sintaxis -- 2026-11-02
- **Herramienta:** Claude Code (Anthropic)
- **Prompt(s) principales:** continuación del plan (secciones 4.1 y 4.2).
- **Qué generó la IA:** `IconScript.ATG` completo (CHARACTERS, TOKENS, COMMENTS anidados, IGNORECASE, producciones); traducción de mensajes de Coco en `Compiler.cs`; `LexSyntaxTests.cs`; ejemplos de `examples/errores/`.
- **Qué revisó y cambió el grupo:** _(completa el grupo)_
- **Errores de la IA detectados:**
  - Acentos doble-codificados (`sÃ­mbolo`) en los mensajes: Coco lee los frames byte a byte como Latin-1 y escribe UTF-8. Se resolvió con un frame solo ASCII (escapes `í`) y el `.ATG` con BOM UTF-8.
  - Al escribir los propios ejemplos y tests la IA cayó en el conflicto LL(1) que había documentado: `MOVETO 0 -220` y `MOVETO 10 -20` se leyeron como restas. Se corrigieron con paréntesis, lo que confirma la decisión documentada.
- **Conceptos de la materia aplicados:** DFA del scanner, palabras clave como literales, LL(1), conjuntos First/Follow, conflicto "start & successor of deletable structure" (análogo al else colgante), recuperación de errores.

### Fase 2 -- Semántica + tabla de símbolos -- 2026-11-02
- **Herramienta:** Claude Code (Anthropic)
- **Prompt(s) principales:** continuación del plan (sección 4.3).
- **Qué generó la IA:** atributos `<out IconType typ, out double? cv>`, `Types.cs`, `SymbolTable.cs` (scopes, `Insert`/`Find`/`Lookup`, `undefSym`), `Palettes.cs`, todas las reglas de 4.3 y `SemanticTests.cs`.
- **Qué revisó y cambió el grupo:** _(completa el grupo)_
- **Errores de la IA detectados:** _(completa el grupo)_
- **Decisiones a discutir:** `TEMPLATE` como alternativa de `Statement` con verificación semántica (en lugar de `[TemplateDecl]`); nombres de variables sin distinción de mayúsculas; prohibición de `FILL` anidado.
- **Conceptos de la materia aplicados:** atributos sintetizados, chequeo de tipos, plegado de constantes para verificaciones en compilación, tipo `Undef` para evitar errores en cascada, tabla de símbolos con scopes.

### Fase 3 -- Código intermedio -- 2026-11-02
- **Herramienta:** Claude Code (Anthropic)
- **Prompt(s) principales:** continuación del plan (sección 5.1).
- **Qué generó la IA:** `CodeGen.cs` (`Op`, `Instr`, `Emit`, `Patch`, `Listing`), emisión desde las acciones del `.ATG`, `iconc -code`, `CodeGenTests.cs` (golden).
- **Qué revisó y cambió el grupo:** _(completa el grupo)_
- **Errores de la IA detectados:** _(completa el grupo)_
- **Conceptos de la materia aplicados:** traducción dirigida por la sintaxis, código postfijo para máquina de pila, backpatching de saltos hacia adelante (REPEAT ≈ while de Taste).

### Fases 4 y 5 -- VM + SVG mínimo, geometría -- 2026-11-02
- **Herramienta:** Claude Code (Anthropic)
- **Prompt(s) principales:** continuación del plan (secciones 5.2, 5.3 y 5.5).
- **Qué generó la IA:** `TurtleVM.cs`, `Drawing.cs`, `Geometry.cs` (ARC, bounding box, reflexión, rectángulo rotado), `SvgWriter.cs`, `GeometryTests.cs`.
- **Qué revisó y cambió el grupo:** _(completa el grupo)_
- **Errores de la IA detectados:**
  - En el ejemplo `ola.icon` la IA calculó mal el sentido de los semicírculos (las olas salían verticales). Se detectó al revisar visualmente los SVG y se corrigió.
  - `corazon.icon` tenía el giro del arco al revés (`ARC 106 (-225)` → `ARC 124 225`). El radio se recalculó a mano: r = 212 / (1 + √2/2).
- **Conceptos de la materia aplicados:** máquina de pila, intérprete de código intermedio, pila de contadores de bucle, pila de estados (MIRROR), errores de ejecución.

### Fase 6 -- Diseño de íconos -- 2026-11-02
- **Herramienta:** Claude Code (Anthropic)
- **Prompt(s) principales:** continuación del plan (sección 5.4).
- **Qué generó la IA:** `Templates.cs` (app, file, badge), autofit en `SvgWriter.cs`, BACKGROUND/GRADIENT/PALETTE/SHADOW/FILL/MIRROR, 12 ejemplos, snapshots SVG.
- **Qué revisó y cambió el grupo:** _(completa el grupo)_
- **Errores de la IA detectados:**
  - El ejemplo `flor.icon` **del propio plan** dibuja solo 4 pétalos diminutos: después de cada pétalo la dirección avanza 225° + 45° = 270°, y el autofit solo achica. Se cambió a `ARC 300 45 … RT 180` (avance neto de 45° → 8 pétalos).
  - La hoja de `file` era invisible sobre fondo blanco; se le agregó un contorno suave.
- **Conceptos de la materia aplicados:** generación de la salida final (back-end), transformación de coordenadas.

### Fase 7 -- CLI completa -- 2026-11-02
- **Herramienta:** Claude Code (Anthropic)
- **Qué generó la IA:** `src/IconScript.Cli/Program.cs` (`-o`, `-code`, `-open`, arrastrar y soltar con `GetConsoleProcessList`, mensajes en español, código de salida).
- **Qué revisó y cambió el grupo:** _(completa el grupo; probar el arrastrar y soltar en Windows)_
- **Errores de la IA detectados:** _(completa el grupo)_

### Fases 8 y 9 -- API, editor web y exportación -- 2026-11-02
- **Herramienta:** Claude Code (Anthropic)
- **Prompt(s) principales:** continuación del plan (secciones 6.2 y 6.3).
- **Qué generó la IA:** `src/IconScript.Api/Program.cs` (CORS, timeout de 2 s, `/examples`); `web/` con React + CodeMirror 6 (resaltado `iconscriptLang.ts`, lint de errores), máscaras, pantalla de inicio, exportación de PNG y del ZIP de app (`exporter.ts`).
- **Qué revisó y cambió el grupo:** _(completa el grupo)_
- **Errores de la IA detectados:** `Array.prototype.toSpliced` no estaba disponible con `lib: ES2020`; se reemplazó por `order` de CSS.
- **Verificación:** tamaños de Android/iOS/Expo consultados en la documentación de Expo (icon y adaptive-icon de 1024×1024; mipmaps de 48 a 192; iOS con una sola imagen de 1024 en `Contents.json`). Se verificó en el navegador que el ZIP tiene esa estructura y esos tamaños.

### Fase 10 -- Cierre -- 2026-11-02
- **Herramienta:** Claude Code (Anthropic)
- **Qué generó la IA:** `docs/diseno.md`, `docs/gramatica-ebnf.md`, `README.md` (manual de uso), esta bitácora.
- **Qué revisó y cambió el grupo:** _(completa el grupo)_
- **Pendiente para el grupo:** capturas para el informe y el video; revisar y reescribir con palabras propias las partes del diseño que se van a defender.
