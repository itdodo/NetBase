import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { setupRouterGuard } from './router/guard'
import { setupPermissionDirective } from './directives/permission'

// 函数式组件（显式 import，样式需手动引入）
import 'element-plus/es/components/message/style/css'
import 'element-plus/es/components/message-box/style/css'
import 'element-plus/es/components/notification/style/css'

const app = createApp(App)

app.use(createPinia())
app.use(router)

setupRouterGuard(router)
setupPermissionDirective(app)

// 全局错误兜底：未捕获异常统一记录（预留上报接口），避免静默丢失
app.config.errorHandler = (err, _instance, info) => {
  console.error(`[全局错误] ${info}:`, err)
}

app.mount('#app')
