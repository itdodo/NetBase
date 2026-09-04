import request from './request'
import type { PageResult, Role, RoleQuery, RoleSave, RoleSimple } from '@/types/api'

/** 分页查询角色 */
export const getRolePage = (params: Partial<RoleQuery>) =>
  request.get<never, PageResult<Role>>('/sys/role/page', { params })

/** 查询全部启用角色（下拉框用） */
export const getRoleList = () => request.get<never, RoleSimple[]>('/sys/role/list')

/** 查询角色详情 */
export const getRoleDetail = (id: number) => request.get<never, Role>(`/sys/role/${id}`)

/** 创建角色 */
export const createRole = (data: RoleSave) => request.post<never, number>('/sys/role', data)

/** 更新角色 */
export const updateRole = (id: number, data: RoleSave) =>
  request.put<never, void>(`/sys/role/${id}`, data)

/** 删除角色（内置 admin 角色会被后端拒绝） */
export const deleteRole = (id: number) => request.delete<never, void>(`/sys/role/${id}`)

/** 查询角色已分配的菜单ID */
export const getRoleMenuIds = (id: number) => request.get<never, number[]>(`/sys/role/${id}/menu-ids`)

/** 为角色分配菜单（全量重设） */
export const assignRoleMenus = (id: number, menuIds: number[]) =>
  request.put<never, void>(`/sys/role/${id}/menus`, { menuIds })
