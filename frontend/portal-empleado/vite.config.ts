import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiProxyTarget = env.VITE_API_PROXY_TARGET || 'https://localhost:7079'
  const signinProxyTarget = env.VITE_SIGNIN_PROXY_TARGET || apiProxyTarget

  return {
    plugins: [react()],
    server: {
      watch: {
        usePolling: true,
      },
      proxy: {
        '/api': {
          target: apiProxyTarget,
          changeOrigin: false,
          secure: false,
        },
        '/signin-google': {
          target: signinProxyTarget,
          changeOrigin: false,
          secure: false,
        },
      },
    },
  }
})
