import { getSession } from '@/services/auth'

export function usePermissions() {
  const granted = new Set(getSession()?.permissions ?? [])
  const has = (permission: string): boolean => granted.has(permission)
  return { has }
}
