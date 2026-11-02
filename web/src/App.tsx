import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { compile, examples as fetchExamples, type CompileResult, type Example } from './api'
import { Editor, type EditorHandle } from './Editor'
import { Help } from './Help'
import { PNG_SIZES, appPackage, download, pngSet, svgToPng } from './exporter'

const INITIAL = `// Escribí tu ícono acá. Probá los ejemplos del menú.
TEMPLATE badge
BACKGROUND #0F172A
COLOR #FACC15
WIDTH 24
REPEAT 5 [ FD 300 RT 144 ]
`

type Tab = 'preview' | 'code' | 'help'
type Mask = 'none' | 'ios' | 'circle' | 'squircle'

const KIND: Record<string, string> = {
  Lexico: 'léxico', Sintactico: 'sintáctico', Semantico: 'semántico', Ejecucion: 'ejecución',
}

const FAKE_APPS = ['#F43F5E', '#F59E0B', '#10B981', '#3B82F6', '#8B5CF6', '#EC4899', '#14B8A6',
  '#64748B', '#EAB308', '#6366F1', '#22C55E']
const FAKE_NAMES = ['Fotos', 'Mapas', 'Clima', 'Notas', 'Música', 'Cámara', 'Reloj',
  'Ajustes', 'Correo', 'Agenda', 'Salud']

export function App() {
  const editor = useRef<EditorHandle>(null)
  const [source, setSource] = useState(INITIAL)
  const [result, setResult] = useState<CompileResult | null>(null)
  const [lastSvg, setLastSvg] = useState<{ svg: string; template: string } | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('preview')
  const [mask, setMask] = useState<Mask>('none')
  const [home, setHome] = useState(false)
  const [list, setList] = useState<Example[]>([])
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    fetchExamples().then(setList).catch(() => setList([]))
  }, [])

  // Compilación en vivo con debounce de 300 ms; se cancela la petición anterior.
  useEffect(() => {
    const ctrl = new AbortController()
    const t = setTimeout(() => {
      compile(source, ctrl.signal)
        .then(r => {
          setResult(r)
          setApiError(null)
          if (r.svg) setLastSvg({ svg: r.svg, template: r.template })
        })
        .catch(e => { if (e.name !== 'AbortError') setApiError(String(e.message ?? e)) })
    }, 300)
    return () => { clearTimeout(t); ctrl.abort() }
  }, [source])

  const svgUrl = useMemo(
    () => (lastSvg ? 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(lastSvg.svg) : null),
    [lastSvg])

  const loadExample = useCallback((name: string) => {
    const ex = list.find(e => e.name === name)
    if (ex) editor.current?.setValue(ex.source)
  }, [list])

  async function run(job: () => Promise<void>) {
    setBusy(true)
    try { await job() } catch (e) { alert(`No se pudo exportar: ${e}`) } finally { setBusy(false) }
  }

  const errors = result?.errors ?? []
  const stale = result !== null && !result.success

  const icon = svgUrl && (
    <div className={`icon mask-${mask}`}>
      <img src={svgUrl} alt="vista previa del ícono" />
    </div>
  )

  return (
    <div className="app">
      <header className="bar">
        <span className="logo">IconScript</span>
        <select value="" onChange={e => loadExample(e.target.value)} disabled={!list.length}>
          <option value="">Ejemplos…</option>
          {list.map(e => <option key={e.name} value={e.name}>{e.name}</option>)}
        </select>
        <span className="grow" />
        <button disabled={!lastSvg} onClick={() =>
          download(new Blob([lastSvg!.svg], { type: 'image/svg+xml' }), 'icono.svg')}>Descargar SVG</button>
        <select value="" disabled={!lastSvg || busy} onChange={e => {
          const v = e.target.value
          if (!v || !lastSvg) return
          run(async () => {
            if (v === 'all') download(await pngSet(lastSvg.svg), 'icono-png.zip')
            else download(await svgToPng(lastSvg.svg, +v), `icono-${v}.png`)
          })
        }}>
          <option value="">Exportar PNG…</option>
          {PNG_SIZES.map(s => <option key={s} value={s}>{s}×{s}</option>)}
          <option value="all">Todos (ZIP)</option>
        </select>
        <button disabled={!lastSvg || busy} onClick={() =>
          run(async () => download(await appPackage(lastSvg!.svg, lastSvg!.template), 'app-icons.zip'))}>
          Exportar paquete de app
        </button>
      </header>

      <main className="panes">
        <section className="left">
          <Editor ref={editor} initial={INITIAL} errors={errors} onChange={setSource} />
        </section>

        <section className="right">
          <nav className="tabs">
            <button className={tab === 'preview' ? 'on' : ''} onClick={() => setTab('preview')}>Vista previa</button>
            <button className={tab === 'code' ? 'on' : ''} onClick={() => setTab('code')}>Código intermedio</button>
            <button className={tab === 'help' ? 'on' : ''} onClick={() => setTab('help')}>Ayuda</button>
          </nav>

          {tab === 'preview' && (
            <div className="preview">
              <div className="options">
                <label>Máscara{' '}
                  <select value={mask} onChange={e => setMask(e.target.value as Mask)}>
                    <option value="none">Plantilla</option>
                    <option value="ios">iOS</option>
                    <option value="circle">Android círculo</option>
                    <option value="squircle">Android squircle</option>
                  </select>
                </label>
                <label><input type="checkbox" checked={home} onChange={e => setHome(e.target.checked)} /> Pantalla de inicio</label>
              </div>
              <div className={`stage ${stale ? 'stale' : ''}`}>
                {!icon && <p className="muted">Escribí un programa para ver el ícono.</p>}
                {icon && !home && <div className="big">{icon}</div>}
                {icon && home && (
                  <div className="phone">
                    {FAKE_APPS.map((c, i) => (
                      <div className="app-slot" key={i} style={{ order: i < 5 ? i : i + 1 }}>
                        <div className={`icon mask-${mask === 'none' ? 'ios' : mask}`}><div className="fake" style={{ background: c }} /></div>
                        <span>{FAKE_NAMES[i]}</span>
                      </div>
                    ))}
                    <div className="app-slot" style={{ order: 5 }}>{icon}<span>Mi app</span></div>
                  </div>
                )}
              </div>
              {stale && <p className="muted small">Mostrando el último ícono válido.</p>}
            </div>
          )}

          {tab === 'code' && (
            <pre className="ir">{result?.intermediateCode ?? 'Sin código intermedio (hay errores de compilación).'}</pre>
          )}

          {tab === 'help' && <Help />}
        </section>
      </main>

      <footer className="errors">
        {apiError && <div className="err api">No se pudo conectar con la API ({apiError}). ¿Está corriendo <code>dotnet run --project src/IconScript.Api</code>?</div>}
        {!apiError && errors.length === 0 && result && <div className="ok">✓ Compilación correcta · plantilla {result.template}</div>}
        {errors.map((e, i) => (
          <button key={i} className="err" onClick={() => editor.current?.goTo(e.line, e.col)}>
            <span className={`kind k-${e.kind}`}>{KIND[e.kind] ?? e.kind}</span>
            línea {e.line}, col {e.col}: {e.message}
          </button>
        ))}
      </footer>
    </div>
  )
}
