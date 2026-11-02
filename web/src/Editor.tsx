// Editor CodeMirror 6 con resaltado de IconScript y subrayado de errores.
import { useEffect, useImperativeHandle, useRef, forwardRef } from 'react'
import { basicSetup } from 'codemirror'
import { EditorView, keymap } from '@codemirror/view'
import { EditorState } from '@codemirror/state'
import { indentWithTab } from '@codemirror/commands'
import { setDiagnostics, lintGutter, type Diagnostic as CmDiagnostic } from '@codemirror/lint'
import { HighlightStyle, syntaxHighlighting } from '@codemirror/language'
import { tags } from '@lezer/highlight'
import { iconscript } from './iconscriptLang'
import type { Diagnostic } from './api'

export interface EditorHandle {
  setValue(text: string): void
  goTo(line: number, col: number): void
}

interface Props {
  initial: string
  errors: Diagnostic[]
  onChange(text: string): void
}

const style = HighlightStyle.define([
  { tag: tags.keyword, color: 'var(--kw)', fontWeight: '600' },
  { tag: tags.number, color: 'var(--num)' },
  { tag: tags.string, color: 'var(--color)' },
  { tag: tags.variableName, color: 'var(--var)' },
  { tag: tags.definition(tags.variableName), color: 'var(--var)', fontStyle: 'italic' },
  { tag: tags.atom, color: 'var(--atom)' },
  { tag: tags.comment, color: 'var(--comment)', fontStyle: 'italic' },
  { tag: tags.invalid, color: 'var(--err)', textDecoration: 'underline wavy' },
])

/** Convierte línea/columna (1-based, como las devuelve el compilador) en un rango del documento. */
function range(state: EditorState, line: number, col: number) {
  const l = state.doc.line(Math.min(Math.max(line, 1), state.doc.lines))
  const from = Math.min(l.from + Math.max(col, 1) - 1, l.to)
  const rest = state.doc.sliceString(from, l.to)
  const word = /^\S+/.exec(rest)?.[0].length ?? 0
  return { from, to: word > 0 ? from + word : l.to }
}

export const Editor = forwardRef<EditorHandle, Props>(function Editor({ initial, errors, onChange }, ref) {
  const host = useRef<HTMLDivElement>(null)
  const view = useRef<EditorView>()
  const changeRef = useRef(onChange)
  changeRef.current = onChange

  useEffect(() => {
    view.current = new EditorView({
      parent: host.current!,
      state: EditorState.create({
        doc: initial,
        extensions: [
          basicSetup,
          keymap.of([indentWithTab]),
          iconscript,
          syntaxHighlighting(style),
          lintGutter(),
          EditorView.updateListener.of(u => { if (u.docChanged) changeRef.current(u.state.doc.toString()) }),
        ],
      }),
    })
    return () => view.current?.destroy()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    const v = view.current
    if (!v) return
    const diags: CmDiagnostic[] = errors.map(e => ({
      ...range(v.state, e.line, e.col),
      severity: 'error',
      message: e.message,
      source: e.kind,
    }))
    v.dispatch(setDiagnostics(v.state, diags))
  }, [errors])

  useImperativeHandle(ref, () => ({
    setValue(text) {
      const v = view.current!
      v.dispatch({ changes: { from: 0, to: v.state.doc.length, insert: text } })
    },
    goTo(line, col) {
      const v = view.current!
      const { from } = range(v.state, line, col)
      v.dispatch({ selection: { anchor: from }, scrollIntoView: true })
      v.focus()
    },
  }))

  return <div className="editor" ref={host} />
})
