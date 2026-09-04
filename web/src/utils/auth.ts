const TOKEN_KEY = 'netbase:token'
const USER_KEY = 'netbase:user'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
}

export function getUserName(): string {
  return localStorage.getItem(USER_KEY) || 'admin'
}

export function setUserName(name: string): void {
  localStorage.setItem(USER_KEY, name)
}
