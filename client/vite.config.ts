import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5499,
    proxy: {
      '/api': 'http://127.0.0.1:5488',
      '/health': 'http://127.0.0.1:5488',
      '/ready': 'http://127.0.0.1:5488',
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
})
