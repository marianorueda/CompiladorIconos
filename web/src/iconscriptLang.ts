// Resaltado de sintaxis de IconScript para CodeMirror 6.
// Es un tokenizador simple (StreamLanguage) que reproduce los tokens del .ATG:
// number, color, varDecl ("x), varRef (:x), palabras clave y comentarios // y /* */ anidados.
import { StreamLanguage, type StreamParser } from '@codemirror/language'

export const KEYWORDS = [
  'TEMPLATE', 'FD', 'FORWARD', 'BK', 'BACK', 'RT', 'RIGHT', 'LT', 'LEFT', 'PU', 'PENUP',
  'PD', 'PENDOWN', 'ARC', 'MOVETO', 'REPEAT', 'MIRROR', 'COLOR', 'WIDTH', 'PALETTE',
  'SHADOW', 'BACKGROUND', 'GRADIENT', 'FILL', 'CIRCLE', 'RECT', 'MAKE', 'NOAUTOFIT',
]
const keywordSet = new Set(KEYWORDS)

interface State {
  commentDepth: number // profundidad de comentarios /* */ anidados
}

const parser: StreamParser<State> = {
  name: 'iconscript',
  startState: () => ({ commentDepth: 0 }),
  token(stream, state) {
    if (state.commentDepth > 0) {
      while (!stream.eol()) {
        if (stream.match('/*')) state.commentDepth++
        else if (stream.match('*/')) { if (--state.commentDepth === 0) break }
        else stream.next()
      }
      return 'comment'
    }
    if (stream.eatSpace()) return null
    if (stream.match('//')) { stream.skipToEnd(); return 'comment' }
    if (stream.match('/*')) { state.commentDepth = 1; return 'comment' }
    if (stream.match(/^#[0-9a-fA-F]{6}/)) return 'string'
    if (stream.match(/^\d+(\.\d+)?/)) return 'number'
    if (stream.match(/^"[A-Za-z][A-Za-z0-9_]*/)) return 'variableName.definition'
    if (stream.match(/^:[A-Za-z][A-Za-z0-9_]*/)) return 'variableName'
    if (stream.match(/^[A-Za-z][A-Za-z0-9_]*/)) {
      return keywordSet.has(stream.current().toUpperCase()) ? 'keyword' : 'atom'
    }
    if (stream.match(/^[[\]]/)) return 'bracket'
    if (stream.match(/^[-+*/()]/)) return 'operator'
    stream.next()
    return 'invalid'
  },
  languageData: { commentTokens: { line: '//', block: { open: '/*', close: '*/' } } },
}

export const iconscript = StreamLanguage.define(parser)
