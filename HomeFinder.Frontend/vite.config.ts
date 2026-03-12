import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  base: '/spa/',
  build: {
    outDir: '../HomeFinder/wwwroot/spa',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'https://localhost:7020',
    },
  },
})
