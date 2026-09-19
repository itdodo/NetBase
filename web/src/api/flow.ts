import request from './request'
import type { PageQuery, PageResult } from '@/types/api'

// ---------------- 类型定义（对应后端 NetBase.Service.Sys.Flow DTO） ----------------

/** 流程定义 */
export interface FlowDefinition {
  id: string
  /** 流程编号：新流程系统自动生成（100 起自增），兼容历史手工编码 */
  flowCode: string
  category?: string
  flowName: string
  version: number
  nodeJson: string
  status: number
  remark?: string
  createTime: string
}

export interface FlowDefinitionSave {
  /** 留空则系统自动生成编号（100 起自增） */
  flowCode?: string
  category?: string
  flowName: string
  nodeJson: string
  enabled: boolean
  remark?: string
}

/** 审批任务视图（待办/已办行） */
export interface FlowTaskView {
  taskId: string
  instanceId: string
  nodeName: string
  summary: string
  flowCode: string
  businessTable: string
  businessId: string
  submitterName: string
  submitTime: string
  actTime?: string
  actResult?: string
}

/** 流程实例 */
export interface FlowInstance {
  id: string
  flowCode: string
  summary: string
  businessTable: string
  businessId: string
  /** 1审批中 2通过 3拒绝 4撤回 5作废 */
  status: number
  submitterName: string
  submitTime: string
  endTime?: string
}

/** 审批详情（时间线数据源） */
export interface FlowInstanceDetail {
  instance: FlowInstance
  nodeJson: string
  currentNodeCode?: string
  timeline: FlowTimelineItem[]
  canWithdraw: boolean
  /** 可驳回目标（本实例已走过的审批节点 + 发起人重提） */
  returnTargets: Array<{ code: string; name: string }>
}

export interface FlowTimelineItem {
  nodeCode?: string
  nodeName: string
  action: string
  operatorName: string
  comment?: string
  time: string
}

/** 实例状态字典 */
export const FLOW_INSTANCE_STATUS: Record<number, { label: string; type: 'primary' | 'success' | 'danger' | 'info' | 'warning' }> = {
  1: { label: '审批中', type: 'primary' },
  2: { label: '已通过', type: 'success' },
  3: { label: '已拒绝', type: 'danger' },
  4: { label: '已撤回', type: 'info' },
  5: { label: '已作废', type: 'warning' }
}

// ---------------- 流程定义管理 ----------------

export const getFlowDefPage = (params: Partial<PageQuery & { keyword?: string }>) =>
  request.get<never, PageResult<FlowDefinition>>('/sys/flow/def/page', { params })

export const getFlowDefDetail = (id: string | number) =>
  request.get<never, FlowDefinition>(`/sys/flow/def/${id}`)

export const createFlowDef = (data: FlowDefinitionSave) =>
  request.post<never, string>('/sys/flow/def', data)

export const updateFlowDef = (id: string | number, data: FlowDefinitionSave) =>
  request.put<never, void>(`/sys/flow/def/${id}`, data)

export const deleteFlowDef = (id: string | number) =>
  request.delete<never, void>(`/sys/flow/def/${id}`)

export const enableFlowDef = (id: string | number) =>
  request.put<never, void>(`/sys/flow/def/${id}/enable`)

export const disableFlowDef = (id: string | number) =>
  request.put<never, void>(`/sys/flow/def/${id}/disable`)

/** 分类统计（名称 + 流程数） */
export interface FlowCategory {
  name: string
  count: number
}

export const getFlowCategories = () => request.get<never, FlowCategory[]>('/sys/flow/def/categories')

export const renameFlowCategory = (oldName: string, newName: string) =>
  request.put<never, void>('/sys/flow/def/category', { oldName, newName })

export const deleteFlowCategory = (name: string) =>
  request.delete<never, void>('/sys/flow/def/category', { params: { name } })

/** 单据绑定 */
export interface FlowBinding {
  id: number
  businessTable: string
  flowCode?: string
  remark?: string
}

export const getFlowBindings = () => request.get<never, FlowBinding[]>('/sys/flow/binding')

export const saveFlowBinding = (data: { businessTable: string; flowCode?: string; remark?: string }) =>
  request.post<never, void>('/sys/flow/binding', data)

export const deleteFlowBinding = (id: number | string) =>
  request.delete<never, void>(`/sys/flow/binding/${id}`)

// ---------------- 待办 / 已办 / 审批操作 ----------------

export const getFlowTodoPage = (params: Partial<PageQuery>) =>
  request.get<never, PageResult<FlowTaskView>>('/sys/flow/task/todo', { params })

export const getFlowDonePage = (params: Partial<PageQuery>) =>
  request.get<never, PageResult<FlowTaskView>>('/sys/flow/task/done', { params })

export const getFlowTodoCount = () => request.get<never, number>('/sys/flow/task/todo-count')

export const actFlowTask = (taskId: string | number, action: 'approve' | 'reject', comment?: string) =>
  request.post<never, void>(`/sys/flow/task/${taskId}/act`, { action, comment })

export const transferFlowTask = (taskId: string | number, userIds: number[], comment?: string) =>
  request.post<never, void>(`/sys/flow/task/${taskId}/transfer`, { userIds, comment })

export const addSignFlowTask = (taskId: string | number, userIds: number[], before: boolean, comment?: string) =>
  request.post<never, void>(`/sys/flow/task/${taskId}/addsign`, { userIds, before, comment })

// ---------------- 实例 ----------------

export const getFlowInstancePage = (params: Partial<PageQuery & { flowCode?: string; submitter?: string; status?: number }>) =>
  request.get<never, PageResult<FlowInstance>>('/sys/flow/instance/page', { params })

/** 我的申请分页（我提交的审批单） */
export const getFlowInstanceMine = (params: Partial<PageQuery & { flowCode?: string; status?: number }>) =>
  request.get<never, PageResult<FlowInstance>>('/sys/flow/instance/my', { params })

export const getFlowInstanceDetail = (id: string | number) =>
  request.get<never, FlowInstanceDetail>(`/sys/flow/instance/${id}`)

export const getFlowInstanceByBusiness = (businessTable: string, businessId: string | number) =>
  request.get<never, FlowInstance | null>('/sys/flow/instance/by-business', {
    params: { businessTable, businessId }
  })

export const getFlowCcMe = (params: Partial<PageQuery>) =>
  request.get<never, PageResult<FlowInstance>>('/sys/flow/instance/cc-me', { params })

export const withdrawFlowInstance = (id: string | number) =>
  request.post<never, void>(`/sys/flow/instance/${id}/withdraw`)

// ---------------- 提交审批（业务单据页调用） ----------------

export interface FlowSubmitBody {
  flowCode: string
  businessTable: string
  businessId: string | number
  variables?: Record<string, unknown>
  choices?: Record<string, number[]>
}

export const submitFlow = (data: FlowSubmitBody) =>
  request.post<never, string>('/sys/flow/submit', data)
