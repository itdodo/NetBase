import request from '../request'
import type { PageQuery, PageResult } from '@/types/api'

/** GenTestExpense */
export interface ExpenseTest {
  id: string

  /** 报销标题 */
  title: string



  /** 报销金额 */
  amount: number

  /** 事由说明 */
  reason: string



  /** 状态 */
  status: string



  /** 是否已删除 */
  isdeleted: boolean

  /** 并发版本 */
  version: string



}

/** GenTestExpense 保存入参 */
export interface ExpenseTestSave {

  title: string

  amount: number

  reason: string

  status: string

  createtime: string

  createby: string

  updatetime?: string

  updateby: string

  isdeleted: boolean


}

export interface ExpenseTestQuery extends PageQuery {

  title?: string

}

export const getExpenseTestPage = (params: Partial<ExpenseTestQuery>) =>
  request.get<never, PageResult<ExpenseTest>>('/biz/expensetest/page', { params })

export const getExpenseTestDetail = (id: string) =>
  request.get<never, ExpenseTest>(`/biz/expensetest/${id}`)

export const createExpenseTest = (data: ExpenseTestSave) =>
  request.post<never, string>('/biz/expensetest', data)

export const updateExpenseTest = (id: string, data: ExpenseTestSave) =>
  request.put<never, void>(`/biz/expensetest/${id}`, data)

export const deleteExpenseTest = (id: string) =>
  request.delete<never, void>(`/biz/expensetest/${id}`)


export const submitExpenseTest = (id: string, flowCode = '') =>
  request.post<never, void>(`/biz/expensetest/${id}/submit`, { flowCode })

