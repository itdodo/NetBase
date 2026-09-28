import request from '../request'
import type { PageQuery, PageResult } from '@/types/api'

/** ContractMgr */
export interface Contract {
  id: string

  /** 合同名称 */
  contractName: string



  /** 合同金额 */
  amount: number

  /** 签订日期 */
  signDate?: string

  /** 备注 */
  remark: string



  /** 归属部门 */
  deptId: string



  /** 归属用户 */
  ownerUserId: string



  /** 单据状态 */
  status: string



  version: number
}

/** ContractMgr 保存入参 */
export interface ContractSave {

  contractName: string

  amount: number

  signDate?: string

  remark: string

  deptId: string

  ownerUserId: string

  status: string

}

export interface ContractQuery extends PageQuery {

  contractName?: string

}

export const getContractPage = (params: Partial<ContractQuery>) =>
  request.get<never, PageResult<Contract>>('/biz/contract/page', { params })

export const getContractDetail = (id: string) =>
  request.get<never, Contract>(`/biz/contract/${id}`)

export const createContract = (data: ContractSave) =>
  request.post<never, string>('/biz/contract', data)

export const updateContract = (id: string, data: ContractSave) =>
  request.put<never, void>(`/biz/contract/${id}`, data)

export const deleteContract = (id: string) =>
  request.delete<never, void>(`/biz/contract/${id}`)


