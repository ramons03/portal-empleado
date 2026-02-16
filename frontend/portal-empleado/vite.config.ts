import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    watch: {
      usePolling: true,
    },
    proxy: {
      '/api': {
        target: 'http://localhost:5011',
        changeOrigin: false,
        secure: false,
      },
      '/signin-google': {
        target: 'http://localhost:5011',
        changeOrigin: false,
        secure: false,
      },
    },
  },
})
