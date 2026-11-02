// Pestaña "Ayuda": referencia rápida del lenguaje.
const rows: [string, string][] = [
  ['TEMPLATE app | file | badge', 'Plantilla del ícono (una vez, al inicio). Por defecto: app.'],
  ['FD n · BK n', 'Avanza / retrocede n unidades (FORWARD, BACK).'],
  ['RT g · LT g', 'Gira g grados a la derecha / izquierda (RIGHT, LEFT).'],
  ['PU · PD', 'Levanta / baja el lápiz (PENUP, PENDOWN).'],
  ['ARC r g', 'Arco de radio r y g grados; g > 0 gira a la derecha.'],
  ['MOVETO x y', 'Va a la posición (x, y) relativa al centro, sin trazar.'],
  ['REPEAT n [ … ]', 'Repite el bloque n veces.'],
  ['MIRROR [ … ]', 'Ejecuta el bloque y luego su reflejo (simetría).'],
  ['COLOR c · WIDTH n', 'Color y grosor del trazo.'],
  ['PALETTE nombre', 'ocean, sunset, forest, candy, mono → :primary :secondary :accent :light :dark'],
  ['BACKGROUND pintura', 'Fondo: un color o GRADIENT c1 c2.'],
  ['FILL pintura [ … ]', 'Rellena el camino que recorre la tortuga dentro del bloque.'],
  ['CIRCLE r · RECT w h', 'Círculo / rectángulo centrado en la tortuga.'],
  ['SHADOW', 'Sombra suave bajo el dibujo.'],
  ['MAKE "x expr', 'Declara o reasigna la variable x; se usa como :x.'],
  ['NOAUTOFIT', 'Desactiva el centrado y escalado automático.'],
]

export function Help() {
  return (
    <div className="help">
      <h3>Referencia de IconScript</h3>
      <table>
        <tbody>
          {rows.map(([cmd, desc]) => (
            <tr key={cmd}><td><code>{cmd}</code></td><td>{desc}</td></tr>
          ))}
        </tbody>
      </table>
      <h4>Expresiones</h4>
      <p>
        Números (<code>100</code>, <code>2.5</code>), colores (<code>#4F46E5</code>), variables
        (<code>:lado</code>), <code>+ - * /</code>, paréntesis y menos unario. Los colores no admiten
        operaciones aritméticas.
      </p>
      <p>
        <strong>Ojo:</strong> en comandos con dos argumentos, un segundo argumento negativo va entre
        paréntesis: <code>ARC 30 (-180)</code>. Sin ellos, <code>ARC 30 -180</code> se lee como una resta.
      </p>
      <h4>Comentarios</h4>
      <p><code>// hasta el fin de línea</code> y <code>/* bloque /* anidable */ */</code>. Las palabras clave no distinguen mayúsculas.</p>
    </div>
  )
}
