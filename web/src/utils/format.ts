/** 时间格式化：ISO 字符串转 yyyy-MM-dd HH:mm:ss，空值返回占位符 */
export function formatDateTime(_row: unknown, _column: unknown, cellValue?: string | null): string {
  if (!cellValue) return '-'
  const date = new Date(cellValue)
  if (Number.isNaN(date.getTime())) return cellValue
  const pad = (n: number) => String(n).padStart(2, '0')
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ` +
    `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
  )
}


/** 标量时间格式化（非 el-table 场景直接调用） */
export function formatTime(value?: string | null): string {
  return formatDateTime(null, null, value)
}

/** 操作日志动作中文名（写入为控制器动作名，展示层翻译；未收录动作回退原文） */
const ACTION_LABELS: Record<string, string> = {
  Login: '登录',
  Logout: '退出登录',
  Refresh: '刷新令牌',
  Create: '新增',
  Update: '修改',
  Delete: '删除',
  Export: '导出',
  Import: '导入',
  ImportTemplate: '下载导入模板',
  ResetPassword: '重置密码',
  ResetPasswordByCode: '自助重置密码',
  SendResetCode: '发送重置验证码',
  ChangePassword: '修改密码',
  UpdateProfile: '修改资料',
  Avatar: '更换头像',
  AssignRoles: '分配角色',
  AssignMenus: '分配菜单',
  MarkRead: '标记已读',
  MarkAllRead: '全部已读',
  Trigger: '触发作业',
  Pause: '暂停作业',
  Resume: '恢复作业',
  UpdateCron: '修改调度',
  Kick: '强制下线',
  KickBatch: '批量强制下线',
  Sessions: '在线会话查询',
  Cleanup: '清理日志',
  Publish: '发布',
  Backup: '执行备份',
  Page: '分页查询',
  List: '列表查询',
  Detail: '查询详情',
  My: '查询我的',
  Tree: '查询树'
}

/** 操作日志动作格式化（el-table :formatter 签名；未收录动作原样显示） */
export function formatAction(_row: unknown, _column: unknown, value?: string | null): string {
  if (!value) return '-'
  return ACTION_LABELS[value] ?? value
}
