import { onBeforeUnmount } from 'vue'

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

/**
 * یک «کانال» درخواست: شروع درخواست جدید، قبلی را لغو می‌کند
 * و با خروج از کامپوننت همه‌چیز لغو می‌شود.
 */
export function useRequestChannel() {
  let current: AbortController | null = null

  function begin(): AbortController {
    current?.abort()
    current = new AbortController()
    return current
  }
  const isCurrent = (c: AbortController): boolean => current === c
  function end(c: AbortController): void {
    if (current === c) current = null
  }
  function abort(): void {
    current?.abort()
    current = null
  }

  onBeforeUnmount(abort)
  return { begin, isCurrent, end, abort }
}
