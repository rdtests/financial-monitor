import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const backendUrl = 'http://localhost:5080'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // The dev server forwards API and SignalR calls to the backend, so the browser sees a single
    // origin (no CORS needed). ws: true lets the SignalR WebSocket upgrade pass through.
    proxy: {
      '/api': backendUrl,
      '/hubs': { target: backendUrl, ws: true },
    },
  },
})
