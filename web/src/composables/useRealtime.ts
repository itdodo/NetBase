/**
 * SignalR 实时通知连接管理（单例）。
 * - JWT 认证：accessToken 随连接 URL 传递（后端 OnMessageReceived 支持）
 * - 自动重连：withAutomaticReconnect 指数退避，无限次
 * - 事件分发：onNotice / onForceLogout 注册回调
 */
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { getToken } from '@/utils/auth'

export interface RealtimeNotice {
  msgType: number
  title: string
  content: string
  senderName?: string
  bizType?: string
  bizId?: string
}

let connection: HubConnection | null = null
let started = false

const noticeHandlers = new Set<(notice: RealtimeNotice) => void>()
const forceLogoutHandlers = new Set<(reason?: string) => void>()

export function onNotice(handler: (notice: RealtimeNotice) => void): void {
  noticeHandlers.add(handler)
}

export function onForceLogout(handler: (reason?: string) => void): void {
  forceLogoutHandlers.add(handler)
}

export function getRealtimeConnection(): HubConnection {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl(`/hubs/notify?access_token=${encodeURIComponent(getToken() ?? '')}`, {
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('notice', (notice: RealtimeNotice) => {
      noticeHandlers.forEach((h) => h(notice))
    })

    connection.on('force-logout', (payload: { reason?: string }) => {
      forceLogoutHandlers.forEach((h) => h(payload?.reason))
    })

    connection.onclose(() => {
      started = false
    })
  }
  return connection
}

/** 登录后调用：建立实时连接（幂等） */
export async function startRealtime(): Promise<void> {
  const conn = getRealtimeConnection()
  if (started || conn.state === 'Connected') return
  try {
    await conn.start()
    started = true
  } catch {
    // 失败不阻断页面；后续路由切换时可重试
  }
}

/** 断开实时连接（登出时调用） */
export async function stopRealtime(): Promise<void> {
  if (connection && started) {
    try {
      await connection.stop()
    } catch {
      // 忽略停止异常
    }
  }
  started = false
}
