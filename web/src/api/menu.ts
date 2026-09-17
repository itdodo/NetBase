import request from './request'
import type { MenuSave, MenuTree } from '@/types/api'

/** 查询全量菜单树 */
export const getMenuTree = () => request.get<never, MenuTree[]>('/sys/menu/tree')

/** 当前用户可见菜单树（登录即可调用；无角色用户返回空数组） */
export const getMyMenuTree = () => request.get<never, MenuTree[]>('/sys/menu/tree/my')

/** 查询指定角色的菜单树（仅包含已授权节点及其父链） */
export const getMenuTreeByRole = (roleId: number) =>
  request.get<never, MenuTree[]>(`/sys/menu/tree/role/${roleId}`)

/** 查询菜单详情 */
export const getMenuDetail = (id: number) => request.get<never, MenuTree>(`/sys/menu/${id}`)

/** 创建菜单（目录/菜单/按钮） */
export const createMenu = (data: MenuSave) => request.post<never, string>('/sys/menu', data)

/** 更新菜单 */
export const updateMenu = (id: number, data: MenuSave) =>
  request.put<never, void>(`/sys/menu/${id}`, data)

/** 删除菜单（存在子节点或被角色引用时后端拒绝） */
export const deleteMenu = (id: number) => request.delete<never, void>(`/sys/menu/${id}`)
