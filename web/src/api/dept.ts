import request from './request'

export interface DeptTree {
  id: number
  parentId: number
  deptName: string
  deptCode: string
  leader?: string
  sort: number
  status: number
  createTime: string
  children: DeptTree[]
}

export interface DeptSave {
  parentId: number
  deptName: string
  deptCode: string
  leader?: string
  /** 负责人用户ID（审批流部门主管解析用；0=未设置） */
  leaderUserId?: number
  sort: number
  status: number
}

export const getDeptTree = () => request.get<never, DeptTree[]>('/sys/dept/tree')
export const getDeptDetail = (id: number) => request.get<never, DeptTree>(`/sys/dept/${id}`)
export const createDept = (data: DeptSave) => request.post<never, string>('/sys/dept', data)
export const updateDept = (id: number, data: DeptSave) => request.put<never, void>(`/sys/dept/${id}`, data)
export const deleteDept = (id: number) => request.delete<never, void>(`/sys/dept/${id}`)
