import request from './request'
import type { PageQuery, PageResult } from '@/types/api'

/** 岗位（审批权限的载体） */
export interface Position {
  id: string
  positionCode: string
  positionName: string
  sort: number
  status: number
  remark?: string
  createTime: string
}

export interface PositionSave {
  positionCode: string
  positionName: string
  sort: number
  status: number
  remark?: string
}

/** 全部启用岗位（下拉框/审批人选择用） */
export const getPositionList = () => request.get<never, Position[]>('/sys/position/list')

export const getPositionPage = (params: Partial<PageQuery & { keyword?: string }>) =>
  request.get<never, PageResult<Position>>('/sys/position/page', { params })

export const createPosition = (data: PositionSave) => request.post<never, string>('/sys/position', data)

export const updatePosition = (id: string | number, data: PositionSave) =>
  request.put<never, void>(`/sys/position/${id}`, data)

export const deletePosition = (id: string | number) => request.delete<never, void>(`/sys/position/${id}`)
