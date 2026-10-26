import { useRouter } from 'vue-router'

/** به‌جای instanceof روی چند کلاس خطا، فقط `status` را بررسی می‌کند. */
export function useUnauthorizedRedirect(redirectPath: string) {
  const router = useRouter()

  async function handleUnauthorized(error: unknown): Promise<boolean> {
    const status = (error as { status?: number } | null)?.status
    if (status !== 401) return false
    await router.replace({ name: 'login', query: { redirect: redirectPath } })
    return true
  }
  return { handleUnauthorized }
}
