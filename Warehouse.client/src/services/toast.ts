import { readonly, ref } from 'vue'

export type ToastType = 'success' | 'error' | 'warning' | 'info'

export interface ToastMessage {
  id: number
  type: ToastType
  title: string
  message: string
  duration: number
}

interface ToastOptions {
  type: ToastType
  message: string
  title?: string
  duration?: number
}

const defaultTitles: Record<ToastType, string> = {
  success: 'عملیات موفق',
  error: 'عملیات ناموفق',
  warning: 'هشدار',
  info: 'اطلاع‌رسانی',
}

const messages = ref<ToastMessage[]>([])
const timers = new Map<number, ReturnType<typeof setTimeout>>()
let nextId = 1

export const toastMessages = readonly(messages)

export function dismissToast(id: number): void {
  const timer = timers.get(id)
  if (timer) clearTimeout(timer)
  timers.delete(id)
  messages.value = messages.value.filter((message) => message.id !== id)
}

export function showToast(options: ToastOptions): number {
  const normalizedMessage = options.message.trim()
  const duplicate = messages.value.find(
    (message) => message.type === options.type && message.message === normalizedMessage,
  )
  if (duplicate) dismissToast(duplicate.id)

  const toast: ToastMessage = {
    id: nextId++,
    type: options.type,
    title: options.title ?? defaultTitles[options.type],
    message: normalizedMessage,
    duration: options.duration ?? 4500,
  }
  messages.value = [...messages.value, toast].slice(-4)
  timers.set(toast.id, setTimeout(() => dismissToast(toast.id), toast.duration))
  return toast.id
}

export const toast = {
  success(message: string, title?: string): number {
    return showToast({ type: 'success', message, title })
  },
  error(message: string, title?: string): number {
    return showToast({ type: 'error', message, title, duration: 6000 })
  },
  warning(message: string, title?: string): number {
    return showToast({ type: 'warning', message, title })
  },
  info(message: string, title?: string): number {
    return showToast({ type: 'info', message, title })
  },
}
