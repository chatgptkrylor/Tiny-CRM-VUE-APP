import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5174',
        changeOrigin: false,
      },
    },
  },
  build: {
    outDir: '../TinyCrm.Api/wwwroot',
    emptyOutDir: true,
  },
})
