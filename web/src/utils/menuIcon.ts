import * as ElementIcons from '@element-plus/icons-vue'
import type { Component } from 'vue'

/**
 * 菜单 icon 字段动态匹配 Element Plus 图标（支持 kebab/小写，如 setting → Setting），
 * 菜单管理里可直接填写任意 Element Plus 图标名，无需改前端代码。
 */
export function resolveMenuIcon(name?: string | null): Component | undefined {
  if (!name) return undefined
  const pascal = name
    .replace(/(^|[-_])([a-z])/g, (_, __, c: string) => c.toUpperCase())
    .replace(/^./, (c) => c.toUpperCase())
  return (ElementIcons as Record<string, Component>)[pascal]
}

/** 菜单图标可选项（Pascal 命名，菜单管理表单下拉展示用；支持手工输入其他合法图标名） */
export const MENU_ICON_OPTIONS = [
  // 人员/组织
  'User', 'Avatar', 'UserFilled', 'OfficeBuilding',
  // 系统/配置
  'Menu', 'Collection', 'Setting', 'Tools', 'Key', 'Lock',
  // 通知/消息
  'Bell', 'Message', 'ChatDotRound',
  // 流程/文档
  'Share', 'Connection', 'Document', 'DocumentAdd', 'DocumentChecked', 'Tickets', 'Files', 'FolderOpened', 'Notebook',
  // 财务/业务
  'Money', 'Wallet', 'ShoppingCart', 'Goods', 'Box', 'CreditCard',
  // 监控/统计
  'Monitor', 'Cpu', 'Timer', 'AlarmClock', 'Clock', 'Calendar',
  'DataAnalysis', 'TrendCharts', 'PieChart', 'Odometer', 'List', 'Operation',
  // 标记
  'Checked', 'Finished', 'Star', 'Flag', 'Link', 'View'
]
