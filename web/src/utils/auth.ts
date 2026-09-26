const TOKEN_KEY = 'netbase:token'
const REFRESH_TOKEN_KEY = 'netbase:refresh-token'
const USER_KEY = 'netbase:user'
const AVATAR_KEY = 'netbase:avatar'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY)
}

export function setRefreshToken(token: string): void {
  localStorage.setItem(REFRESH_TOKEN_KEY, token)
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(REFRESH_TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
}

export function getUserName(): string {
  return localStorage.getItem(USER_KEY) || 'admin'
}

export function setUserName(name: string): void {
  localStorage.setItem(USER_KEY, name)
}

export function getAvatar(): string {
  return localStorage.getItem(AVATAR_KEY) || ''
}

export function setAvatar(url: string): void {
  if (url) {
    localStorage.setItem(AVATAR_KEY, url)
  } else {
    localStorage.removeItem(AVATAR_KEY)
  }
}
