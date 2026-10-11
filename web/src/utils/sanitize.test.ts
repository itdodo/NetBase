import { describe, it, expect } from 'vitest'
import { sanitizeHtml } from '@/utils/sanitize'

describe('sanitizeHtml（防存储型 XSS）', () => {
  it('剔除 script 标签', () => {
    expect(sanitizeHtml('<p>a</p><script>alert(1)</script>')).not.toContain('<script>')
  })

  it('剔除事件处理器属性', () => {
    const out = sanitizeHtml('<img src="x.png" onerror="alert(1)">')
    expect(out).not.toContain('onerror')
    expect(out).toContain('src')
  })

  it('剔除 javascript: 协议链接', () => {
    const out = sanitizeHtml('<a href="javascript:alert(1)">x</a>')
    expect(out).not.toContain('javascript:')
  })

  it('白名单内标签与样式属性保留（wangEditor 常用形态）', () => {
    const html = '<p style="text-align:center"><strong>加粗</strong></p><table><tr><td colspan="2">cell</td></tr></table>'
    const out = sanitizeHtml(html)
    expect(out).toContain('<strong>')
    expect(out).toContain('colspan')
  })

  it('data-* 属性剔除（ALLOW_DATA_ATTR=false）', () => {
    expect(sanitizeHtml('<p data-x="1">t</p>')).not.toContain('data-x')
  })
})
