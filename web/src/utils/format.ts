/** 时间格式化：ISO 字符串转 yyyy-MM-dd HH:mm:ss，空值返回占位符 */
export function formatDateTime(_row: unknown, _column: unknown, cellValue?: string | null): string {
  if (!cellValue) return '-'
  const date = new Date(cellValue)
  if (Number.isNaN(date.getTime())) return cellValue
  const pad = (n: number) => String(n).padStart(2, '0')
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ` +
    `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
  )
}
