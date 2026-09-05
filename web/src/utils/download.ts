import request from '@/api/request'

/** 下载文件（GET，blob），filename 含扩展名 */
export async function download(
  url: string,
  params: Record<string, unknown> | undefined,
  filename: string,
): Promise<void> {
  const blob = (await request.get<never, Blob>(url, { params, responseType: 'blob' })) as Blob
  const blobUrl = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = blobUrl
  link.download = filename
  link.click()
  URL.revokeObjectURL(blobUrl)
}
