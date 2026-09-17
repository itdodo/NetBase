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
  /** 排序列（实体属性名，无效值后端回退主键） */
  sortField?: string
  /** 是否降序，默认 true */
  sortDesc?: boolean
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
  deptId: number
  deptName?: string
  avatar?: string
  phone?: string
  email?: string
  status: number
  lastLoginTime?: string
  createTime: string
  roles: RoleSimple[]
  /** 并发版本（编辑时读取、保存时回传，陈旧则后端拒绝） */
  version: number
}

export interface UserCreate {
  userName: string
  deptId: number
  nickName?: string
  phone?: string
  email?: string
  password?: string
  status: number
  roleIds: number[]
}

export interface UserUpdate {
  nickName?: string
  deptId: number
  phone?: string
  email?: string
  status: number
  roleIds?: number[]
  /** 编辑时读取的并发版本（回传校验，不传则跳过） */
  version?: number
}

export interface Role {
  id: number
  roleName: string
  roleCode: string
  status: number
  sort: number
  dataScope: number
  createTime: string
  /** 并发版本（编辑时读取、保存时回传，陈旧则后端拒绝） */
  version: number
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
  dataScope: number
  deptIds?: number[]
  /** 编辑时读取的并发版本（回传校验，不传则跳过；创建时无需传） */
  version?: number
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
  /** 并发版本（编辑时读取、保存时回传，陈旧则后端拒绝） */
  version: number
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
  /** 编辑时读取的并发版本（回传校验，不传则跳过；创建时无需传） */
  version?: number
}
