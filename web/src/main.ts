import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import 'element-plus/dist/index.css'
import App from './App.vue'
import router from './router'
import { setupRouterGuard } from './router/guard'
import { setupPermissionDirective } from './directives/permission'

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(ElementPlus, { locale: zhCn })

setupRouterGuard(router)
setupPermissionDirective(app)

// 全局错误兜底：未捕获异常统一记录（预留上报接口），避免静默丢失
app.config.errorHandler = (err, _instance, info) => {
  console.error(`[全局错误] ${info}:`, err)
}

app.mount('#app')
