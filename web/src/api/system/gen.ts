import request from '../request'
import type { PageQuery, PageResult } from '@/types/api'

/** 代码生成配置（表级） */
export interface GenTable {
  id: string
  tableName: string
  tableComment: string
  moduleName: string
  functionName: string
  entityName: string
  author: string
  dataScope: boolean
  flowDoc: boolean
  sourceType: string
  subTablesJson?: string
  createTime?: string
  /** 编辑回显时的子表配置（后端解析 JSON 后返回） */
  subTables?: GenSubTable[]
}

/** 代码生成字段配置 */
export interface GenColumn {
  id?: string
  genTableId?: string
  columnName: string
  propertyName: string
  columnType: string
  cSharpType: string
  displayName: string
  columnComment: string
  length: number
  isPk: boolean
  isRequired: boolean
  isList: boolean
  isQuery: boolean
  isForm: boolean
  queryType?: string
  uiType?: string
  sort: number
  subTableName?: string
}

/** 子表配置 */
export interface GenSubTable {
  tableName: string
  tableComment: string
  fkColumn: string
  entityName: string
  columns?: GenColumn[]
}

/** 保存请求（表 + 字段 + 子表） */
export interface GenSaveRequest {
  id?: string
  tableName: string
  tableComment: string
  moduleName: string
  functionName: string
  entityName: string
  author: string
  dataScope: boolean
  flowDoc: boolean
  sourceType: string
  subTables?: GenSubTable[]
  columns: GenColumn[]
}

/** 预览文件 */
export interface GenPreviewFile {
  path: string
  content: string
}

export interface GenTableBrief {
  tableName: string
  tableComment: string
}

export const getGenPage = (params: Partial<PageQuery & { keyword?: string }>) =>
  request.get<never, PageResult<GenTable>>('/sys/gen/page', { params })

export const getGenDetail = (id: string) => request.get<never, { table: GenTable; columns: GenColumn[] }>(`/sys/gen/${id}`)

export const getDbTables = () => request.get<never, GenTableBrief[]>('/sys/gen/db-tables')

export const getDbColumns = (tableName: string) =>
  request.get<never, Array<{ columnName: string; dataType: string; cSharpType: string; columnComment: string; isPk: boolean; isRequired: boolean; length: number }>>('/sys/gen/db-columns', { params: { tableName } })

export const importGenTable = (data: { tableName: string; moduleName: string; functionName: string; entityName: string }) =>
  request.post<never, string>('/sys/gen/import', data)

export const saveGenTable = (data: GenSaveRequest) => request.post<never, string>('/sys/gen', data)

export const deleteGenTable = (id: string) => request.delete<never, void>(`/sys/gen/${id}`)

export const previewGen = (id: string) => request.get<never, GenPreviewFile[]>(`/sys/gen/${id}/preview`)

/** 下载生成代码 zip（blob） */
export const downloadGen = (id: string) =>
  request.get<never, Blob>(`/sys/gen/${id}/download`, { responseType: 'blob' })
