import { download } from '@/utils/download'
import request from './request'
import type { PageResult, User, UserCreate, UserQuery, UserUpdate } from '@/types/api'

/** 分页查询用户 */
export const getUserPage = (params: Partial<UserQuery>) =>
  request.get<never, PageResult<User>>('/sys/user/page', { params })

/** 查询全部启用用户（下拉框用） */
export const getUserList = () => request.get<never, User[]>('/sys/user/list')

/** 查询用户详情（含角色） */
export const getUserDetail = (id: number) => request.get<never, User>(`/sys/user/${id}`)

/** 创建用户 */
export const createUser = (data: UserCreate) => request.post<never, number>('/sys/user', data)

/** 更新用户（roleIds 传入则全量重设角色） */
export const updateUser = (id: number, data: UserUpdate) =>
  request.put<never, void>(`/sys/user/${id}`, data)

/** 删除用户（内置 admin 会被后端拒绝） */
export const deleteUser = (id: number) => request.delete<never, void>(`/sys/user/${id}`)

/** 重置密码（newPassword 为空则重置为默认密码 123456） */
export const resetUserPassword = (id: number, newPassword?: string) =>
  request.put<never, void>(`/sys/user/${id}/password/reset`, { newPassword })

/** 为用户分配角色（全量重设） */
export const assignUserRoles = (id: number, roleIds: number[]) =>
  request.put<never, void>(`/sys/user/${id}/roles`, { roleIds })

/** 下载用户导入模板（xlsx） */
export const downloadImportTemplate = () =>
  download('/sys/user/import-template', undefined, `用户导入模板.xlsx`)

/** Excel 批量导入用户 */
export const uploadImportFile = (file: File): Promise<{ successCount: number; errors: string[] }> => {
  const form = new FormData()
  form.append('file', file)
  return request.post<never, { successCount: number; errors: string[] }>('/sys/user/import', form)
}
