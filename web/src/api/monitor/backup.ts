import request from '../request'

/** 备份文件信息 */
export interface BackupFile {
  fileName: string
  sizeBytes: number
  lastWriteTime: string
}

/** 备份文件列表（按时间倒序） */
export const getBackupFiles = () => request.get<never, BackupFile[]>('/monitor/backup/files')

/** 立即执行一次全量备份 */
export const runBackup = () => request.post<never, string>('/monitor/backup/run')

/** 下载备份文件（blob） */
export const downloadBackupFile = (fileName: string) =>
  request.get<never, Blob>(`/monitor/backup/files/${encodeURIComponent(fileName)}/download`, { responseType: 'blob' })
