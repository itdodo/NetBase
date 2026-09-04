import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  server: {
    port: 5173,
    // 开发期代理到 .NET API，生产可由 API 托管静态文件实现同源
    proxy: {
      '/api': {
        target: 'http://localhost:5026',
        changeOrigin: true
      }
    }
  }
})
