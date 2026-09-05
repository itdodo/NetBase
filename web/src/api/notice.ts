import request from './request'
import type { PageQuery, PageResult } from '@/types/api'

export interface NoticeInfo {
  id: number
  title: string
  noticeType: number
  content: string
  status: number
  createBy?: string
  createTime: string
}

export interface NoticeSave {
  title: string
  noticeType: number
  content: string
  status: number
}

/** 公告分页（管理端） */
export const getNoticePage = (params: Partial<PageQuery & { keyword?: string; noticeType?: number }>) =>
  request.get<never, PageResult<NoticeInfo>>('/sys/notice/page', { params })

/** 公告详情 */
export const getNoticeDetail = (id: number) => request.get<never, NoticeInfo>(`/sys/notice/${id}`)

/** 登录用户取最新公告（铃铛） */
export const getLatestNotices = () => request.get<never, NoticeInfo[]>('/sys/notice/latest')

/** 创建/更新/删除公告 */
export const createNotice = (data: NoticeSave) => request.post<never, number>('/sys/notice', data)
export const updateNotice = (id: number, data: NoticeSave) => request.put<never, void>(`/sys/notice/${id}`, data)
export const deleteNotice = (id: number) => request.delete<never, void>(`/sys/notice/${id}`)
