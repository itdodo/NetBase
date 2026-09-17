// 端到端验证：SignalR 连接 → 同账号二次登录互踢 → 被挤端实时收到 force-logout 推送
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr'

const BASE = 'http://localhost:5173'

async function login() {
  const res = await fetch(BASE + '/api/v1/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userName: 'admin', password: 'Net123456' })
  })
  const json = await res.json()
  if (json.code !== 200) throw new Error('登录失败: ' + json.message)
  return json.data.accessToken
}

const firstToken = await login()
console.log('1. 第一次登录成功（被挤方）')

const conn = new HubConnectionBuilder()
  .withUrl(`${BASE}/hubs/notify?access_token=${encodeURIComponent(firstToken)}`, {
    transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling
  })
  .configureLogging(LogLevel.Error)
  .build()

let received = null
const receivedPromise = new Promise((resolve) => {
  conn.on('force-logout', (payload) => {
    received = payload
    resolve()
  })
})
conn.onclose((err) => console.log('连接关闭:', err?.message ?? '正常'))

await conn.start()
console.log('2. SignalR 已连接:', conn.state)

const secondToken = await login()
console.log('3. 第二次登录成功（触发互踢）')

const timeout = new Promise((_, reject) => setTimeout(() => reject(new Error('10 秒内未收到 force-logout 推送')), 10000))
await Promise.race([receivedPromise, timeout])

console.log('4. 实时收到 force-logout 推送 ✓  原因:', received?.reason ?? '(无)')
console.log('5. 旧 token 仍可用？')

const probe = await fetch(BASE + '/api/v1/auth/profile', {
  headers: { Authorization: `Bearer ${firstToken}` }
})
console.log('   旧 token 状态码:', probe.status, '(期望 401)')

await conn.stop()
console.log('验证完成：实时推送链路 OK')
process.exit(0)
