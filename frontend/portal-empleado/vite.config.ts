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
        target: 'https://localhost:7079',
        changeOrigin: false,
        secure: false,
      },
      '/signin-google': {
        target: 'https://localhost:7079',
        changeOrigin: false,
        secure: false,
      },
    },
  },
})
