import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'

export default defineConfig({
  plugins: [
    vue(),
    // Element Plus 按需引入：组件/样式自动导入，主包体积大幅下降
    AutoImport({ resolvers: [ElementPlusResolver()], dts: 'src/auto-imports.d.ts' }),
    Components({ resolvers: [ElementPlusResolver()], dts: 'src/components.d.ts' })
  ],
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
      },
      // SignalR 实时通道：ws:true 转发 WebSocket 握手与长轮询
      '/hubs': {
        target: 'http://localhost:5026',
        changeOrigin: true,
        ws: true
      }
    }
  }
})
