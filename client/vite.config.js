import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The API runs as a separate process during development. Proxying /api and /hubs keeps the
// browser on a single origin, so no CORS headers are needed for the normal dev flow and
// SignalR negotiates the same way it will in production.
const apiTarget = process.env.VITE_DEV_API_URL || 'http://localhost:5145';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
      '/hubs': {
        target: apiTarget,
        changeOrigin: true,
        ws: true,
      },
    },
  },
});
