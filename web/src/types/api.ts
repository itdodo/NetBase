/** 统一返回结果（对应后端 NetBase.Common.Results.ApiResult） */
export interface ApiResult<T = unknown> {
  code: number
  message: string
  data: T
  timestamp: number
}

/** 分页请求参数 */
export interface PageQuery {
  pageIndex: number
  pageSize: number
  [key: string]: unknown
}

/** 分页返回结果 */
export interface PageResult<T> {
  items: T[]
  total: number
  pageIndex: number
  pageSize: number
  totalPages: number
}

/** 角色 */
export interface RoleSimple {
  id: number
  roleName: string
  roleCode: string
}

export interface UserQuery extends PageQuery {
  keyword?: string
  status?: number
}

export interface User {
  id: number
  userName: string
  nickName?: string
  phone?: string
  email?: string
  status: number
  lastLoginTime?: string
  createTime: string
  roles: RoleSimple[]
}

export interface UserCreate {
  userName: string
  nickName?: string
  phone?: string
  email?: string
  password?: string
  status: number
  roleIds: number[]
}

export interface UserUpdate {
  nickName?: string
  phone?: string
  email?: string
  status: number
  roleIds?: number[]
}

export interface Role {
  id: number
  roleName: string
  roleCode: string
  status: number
  sort: number
  createTime: string
}

export interface RoleQuery extends PageQuery {
  keyword?: string
  status?: number
}

export interface RoleSave {
  roleName: string
  roleCode: string
  status: number
  sort: number
}

/** 菜单类型：1-目录 2-菜单 3-按钮 */
export const MENU_TYPE = { DIRECTORY: 1, MENU: 2, BUTTON: 3 } as const

export interface MenuTree {
  id: number
  parentId: number
  menuName: string
  menuType: number
  path?: string
  component?: string
  permission?: string
  icon?: string
  sort: number
  visible: boolean
  status: number
  createTime: string
  children: MenuTree[]
}

export interface MenuSave {
  parentId: number
  menuName: string
  menuType: number
  path?: string
  component?: string
  permission?: string
  icon?: string
  sort: number
  visible: boolean
  status: number
}
