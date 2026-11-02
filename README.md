# IconScript

Compilador de un lenguaje de dibujo tipo Logo para diseñar íconos (apps, archivos, badges),
construido con Coco/R (C#). Trabajo Final de Compiladores — UNSJ 2026.

```
TEMPLATE badge
BACKGROUND #0F172A
COLOR #FACC15
WIDTH 24
REPEAT 5 [ FD 300 RT 144 ]
```

## Instalación

Requisitos:
- [.NET SDK 8](https://dotnet.microsoft.com/download) o superior (`dotnet --version`).
- Node 20+ (solo para el editor web).

```bash
dotnet build
dotnet test
```

`dotnet build` compila Coco/R (`tools/CocoR`) y regenera `Scanner.cs`/`Parser.cs` desde
`src/IconScript.Core/IconScript.ATG` cuando cambia la gramática. Coco informa como *warning* un
conflicto LL(1) esperado (ver "Referencia" más abajo y `docs/diseno.md`). Para regenerar a mano:
`scripts/gen.ps1` (Windows) o `scripts/gen.sh`.

## Línea de comandos: `iconc`

```bash
dotnet run --project src/IconScript.Cli -- examples/flor.icon
```

| Comando | Efecto |
|---|---|
| `iconc flor.icon` | genera `flor.svg` |
| `iconc flor.icon -o salida.svg` | elige el archivo de salida |
| `iconc flor.icon -code` | además imprime el código intermedio |
| `iconc flor.icon -open` | abre el SVG en el navegador |

En Windows se puede **arrastrar un `.icon` sobre `iconc.exe`**
(`src/IconScript.Cli/bin/Debug/net8.0/iconc.exe`): compila y abre el SVG; si hay errores, la
consola queda abierta para leerlos.

Los errores se muestran como `flor.icon: línea 3, col 5: se esperaba un número  [semántico]`
y el código de salida es distinto de 0.

## Editor web

En dos terminales:

```bash
dotnet run --project src/IconScript.Api
```

```bash
cd web
npm install
npm run dev
```

Abrir <http://localhost:5173>. La API escucha en <http://localhost:5080>; si se usa otra URL,
se configura con la variable `VITE_API_URL`.

- **Izquierda**: editor con resaltado de sintaxis; las líneas con error quedan subrayadas.
- **Derecha**: vista previa, que se recompila 300 ms después de dejar de escribir.
  - Máscaras: plantilla, iOS, Android círculo y Android squircle.
  - Modo *pantalla de inicio*: muestra el ícono entre íconos falsos.
  - Pestañas: *Código intermedio* y *Ayuda*.
- **Abajo**: panel de errores; un clic lleva a la línea.
- **Barra**: menú de ejemplos, *Descargar SVG*, *Exportar PNG* (16 a 1024, o todos en ZIP) y
  *Exportar paquete de app*. El paquete es un ZIP con esta estructura:
  ```
  icon.png                        1024×1024 (Expo)
  adaptive-icon.png               1024×1024, solo primer plano, fondo transparente
  android/mipmap-{mdpi,hdpi,xhdpi,xxhdpi,xxxhdpi}/ic_launcher.png   48/72/96/144/192
  ios/AppIcon.appiconset/icon-1024.png + Contents.json
  ```

## Referencia del lenguaje

Lienzo: la tortuga empieza en el centro, mirando al norte, con el lápiz bajo, color negro y
grosor 8. El eje y va hacia arriba y los ángulos están en grados (`RT` gira en sentido horario).

| Comando | Descripción | Ejemplo |
|---|---|---|
| `TEMPLATE app\|file\|badge` | plantilla (una vez, al inicio; por defecto `app`) | `TEMPLATE badge` |
| `FD n` / `BK n` | avanza / retrocede (`FORWARD`, `BACK`) | `FD 100` |
| `RT g` / `LT g` | gira a la derecha / izquierda (`RIGHT`, `LEFT`) | `RT 90` |
| `PU` / `PD` | levanta / baja el lápiz (`PENUP`, `PENDOWN`) | `PU FD 50 PD` |
| `ARC r g` | arco de radio `r` y `g` grados (`g > 0` hacia la derecha) | `ARC 50 90` |
| `MOVETO x y` | va a `(x, y)` relativo al centro, sin trazar | `MOVETO 0 (-200)` |
| `REPEAT n [ … ]` | repite el bloque | `REPEAT 4 [ FD 100 RT 90 ]` |
| `MIRROR [ … ]` | ejecuta el bloque y su reflejo | `MIRROR [ ARC 80 90 ]` |
| `COLOR c` / `WIDTH n` | color y grosor del trazo | `COLOR #FF0000 WIDTH 20` |
| `PALETTE p` | `ocean`, `sunset`, `forest`, `candy` o `mono`; define `:primary :secondary :accent :light :dark` | `PALETTE ocean` |
| `BACKGROUND pintura` | fondo: color o `GRADIENT c1 c2` | `BACKGROUND GRADIENT :primary :secondary` |
| `FILL pintura [ … ]` | rellena el camino recorrido dentro del bloque | `FILL :light [ CIRCLE 100 ]` |
| `CIRCLE r` / `RECT w h` | círculo / rectángulo centrado en la tortuga | `RECT 200 100` |
| `SHADOW` | sombra suave | `SHADOW` |
| `MAKE "x expr` | declara o reasigna una variable; se usa como `:x` | `MAKE "lado 100 FD :lado` |
| `NOAUTOFIT` | desactiva el centrado y escalado automático | `NOAUTOFIT` |

- **Expresiones**: números (`100`, `2.5`), colores (`#4F46E5`), variables (`:x`),
  `+ - * /`, paréntesis y menos unario. Los colores no admiten aritmética y una variable no
  cambia de tipo.
- **Argumento negativo**: en comandos con dos argumentos, si el segundo es negativo va entre
  paréntesis: `ARC 30 (-180)`. `ARC 30 -180` se lee como una resta (conflicto LL(1)
  documentado).
- **Comentarios**: `// …` y `/* … */` (anidables). Las palabras clave y las variables no
  distinguen mayúsculas.
- **Autofit**: el dibujo se centra en la zona segura de la plantilla y se achica si no entra.

## Ejemplos

En `examples/` hay 12 íconos: flor, estrella, archivo-iconscript, cuadrado, círculo, corazón,
ola, espiral, sol, check, engranaje y nota. En `examples/errores/` hay programas con errores
léxicos, sintácticos, semánticos y de ejecución.

```bash
for f in examples/*.icon; do dotnet run --project src/IconScript.Cli -- "$f"; done
```

## Estructura

| Carpeta | Contenido |
|---|---|
| `src/IconScript.Core` | Biblioteca con todo el compilador (`IconScript.ATG` → `Scanner.cs`/`Parser.cs` generados) |
| `src/IconScript.Cli` | Ejecutable `iconc` |
| `src/IconScript.Api` | API HTTP (`POST /compile`, `GET /examples`) |
| `web/` | Editor web (Vite + React + TypeScript + CodeMirror 6) |
| `examples/` | Programas `.icon` de ejemplo y de error |
| `tests/IconScript.Tests` | Tests xUnit (léxico, sintaxis, semántica, IR, geometría, snapshots SVG) |
| `tools/` | Coco/R compilado desde fuentes y frames modificados |
| `docs/` | `diseno.md`, `gramatica-ebnf.md`, `bitacora-ia.md` |

Los tests snapshot comparan el SVG de cada ejemplo con `tests/IconScript.Tests/Snapshots/`.
Después de un cambio intencional se regeneran así (y se revisa el diff):

```bash
UPDATE_SNAPSHOTS=1 dotnet test
```
