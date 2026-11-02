// Cliente de la API de IconScript (src/IconScript.Api).

export const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

export interface Diagnostic {
  line: number
  col: number
  message: string
  kind: 'Lexico' | 'Sintactico' | 'Semantico' | 'Ejecucion'
}

export interface CompileResult {
  success: boolean
  errors: Diagnostic[]
  intermediateCode: string | null
  svg: string | null
  template: string
}

export interface Example {
  name: string
  source: string
}

export async function compile(source: string, signal?: AbortSignal): Promise<CompileResult> {
  const res = await fetch(`${API_URL}/compile`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ source }),
    signal,
  })
  if (!res.ok) throw new Error(`la API respondió ${res.status}`)
  return res.json()
}

export async function examples(): Promise<Example[]> {
  const res = await fetch(`${API_URL}/examples`)
  if (!res.ok) throw new Error(`la API respondió ${res.status}`)
  return res.json()
}
