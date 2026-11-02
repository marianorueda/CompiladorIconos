import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// La API corre en http://localhost:5080 (CORS habilitado para el puerto 5173).
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
  build: { chunkSizeWarningLimit: 1000 },
})
