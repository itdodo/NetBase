import DOMPurify from 'dompurify'

/**
 * 富文本净化：公告等 HTML 内容渲染/入库前过滤，防存储型 XSS。
 * 白名单保留 wangEditor 常用标签与样式属性，剔除 script/事件处理器等。
 */
export function sanitizeHtml(html: string): string {
  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [
      'p', 'br', 'strong', 'b', 'em', 'i', 'u', 's', 'h1', 'h2', 'h3', 'h4', 'h5',
      'ul', 'ol', 'li', 'blockquote', 'pre', 'code',
      'table', 'thead', 'tbody', 'tr', 'th', 'td',
      'img', 'a', 'span', 'div', 'hr', 'sub', 'sup'
    ],
    ALLOWED_ATTR: ['href', 'src', 'alt', 'title', 'target', 'style', 'class', 'colspan', 'rowspan'],
    ALLOW_DATA_ATTR: false
  })
}
