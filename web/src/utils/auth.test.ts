import { describe, it, expect, beforeEach } from 'vitest'
import { getToken, setToken, clearToken, getUserName, setUserName } from '@/utils/auth'

describe('auth 存取（localStorage 键约定）', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('token 存取与清理', () => {
    setToken('abc')
    expect(getToken()).toBe('abc')
    clearToken()
    expect(getToken()).toBeNull()
  })

  it('clearToken 同时清 token 与 refresh-token', () => {
    setToken('a')
    localStorage.setItem('netbase:refresh-token', 'r')
    clearToken()
    expect(localStorage.getItem('netbase:refresh-token')).toBeNull()
  })

  it('getUserName 空值回退 admin（历史行为约定）', () => {
    expect(getUserName()).toBe('admin')
    setUserName('lisi')
    expect(getUserName()).toBe('lisi')
  })
})
