import request from '../request'
import type { PageQuery, PageResult } from '@/types/api'

/** 单据状态：0草稿 1审批中 2已通过 3已拒绝 4已撤回 */
export const BIZ_DOC_STATUS: Record<number, { label: string; type: 'info' | 'primary' | 'success' | 'danger' | 'warning' }> = {
  0: { label: '草稿', type: 'info' },
  1: { label: '审批中', type: 'primary' },
  2: { label: '已通过', type: 'success' },
  3: { label: '已拒绝', type: 'danger' },
  4: { label: '已撤回', type: 'warning' }
}

// ---------------- 报销单（业务样板一） ----------------

export interface ExpenseDoc {
  id: string
  title: string
  amount: number
  reason?: string
  status: number
  createTime: string
}

export interface ExpenseSave {
  title: string
  amount: number
  reason?: string
}

export const getExpensePage = (params: Partial<PageQuery & { keyword?: string }>) =>
  request.get<never, PageResult<ExpenseDoc>>('/biz/expense/page', { params })

export const getExpenseDetail = (id: string | number) =>
  request.get<never, ExpenseDoc>(`/biz/expense/${id}`)

export const createExpense = (data: ExpenseSave) => request.post<never, string>('/biz/expense', data)

export const updateExpense = (id: string | number, data: ExpenseSave) =>
  request.put<never, void>(`/biz/expense/${id}`, data)

export const deleteExpense = (id: string | number) => request.delete<never, void>(`/biz/expense/${id}`)

export const submitExpense = (id: string | number) =>
  request.post<never, void>(`/biz/expense/${id}/submit`)

// ---------------- 采购申请单（业务样板二） ----------------

export interface PurchaseDoc {
  id: string
  title: string
  itemName: string
  amount: number
  reason?: string
  status: number
  createTime: string
}

export interface PurchaseSave {
  title: string
  itemName: string
  amount: number
  reason?: string
}

export const getPurchasePage = (params: Partial<PageQuery & { keyword?: string }>) =>
  request.get<never, PageResult<PurchaseDoc>>('/biz/purchase/page', { params })

export const getPurchaseDetail = (id: string | number) =>
  request.get<never, PurchaseDoc>(`/biz/purchase/${id}`)

export const createPurchase = (data: PurchaseSave) => request.post<never, string>('/biz/purchase', data)

export const updatePurchase = (id: string | number, data: PurchaseSave) =>
  request.put<never, void>(`/biz/purchase/${id}`, data)

export const deletePurchase = (id: string | number) => request.delete<never, void>(`/biz/purchase/${id}`)

export const submitPurchase = (id: string | number) =>
  request.post<never, void>(`/biz/purchase/${id}/submit`)
