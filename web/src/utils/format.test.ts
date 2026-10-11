import { describe, it, expect } from 'vitest'
import { formatDateTime, formatTime, formatAction, ACTION_LABELS } from '@/utils/format'

describe('formatDateTime', () => {
  it('空值返回占位符', () => {
    expect(formatDateTime(null, null, null)).toBe('-')
    expect(formatDateTime(null, null, '')).toBe('-')
  })

  it('ISO 字符串转 yyyy-MM-dd HH:mm:ss（本地时区）', () => {
    const d = new Date(2026, 9, 8, 14, 30, 5) // 本地时区构造
    expect(formatTime(d.toISOString())).toBe('2026-10-08 14:30:05')
  })

  it('非法字符串原样返回', () => {
    expect(formatTime('not-a-date')).toBe('not-a-date')
  })
})

describe('formatAction（操作日志动作中文化）', () => {
  it('已知动作映射中文', () => {
    expect(formatAction(null, null, 'Login')).toBe('登录')
    expect(formatAction(null, null, 'AssignMenus')).toBe('分配菜单')
    expect(formatAction(null, null, 'KickBatch')).toBe('批量强制下线')
  })

  it('未知动作回退原文（前向兼容新控制器动作）', () => {
    expect(formatAction(null, null, 'SomeFutureAction')).toBe('SomeFutureAction')
    expect(formatAction(null, null, null)).toBe('-')
  })

  it('词表与筛选下拉同源（约束：翻译与筛选永不漂移）', () => {
    // 关键回归：动作列翻译与操作日志下拉筛选共用 ACTION_LABELS
    expect(Object.keys(ACTION_LABELS).length).toBeGreaterThan(20)
    expect(ACTION_LABELS['Login']).toBeDefined()
  })
})
