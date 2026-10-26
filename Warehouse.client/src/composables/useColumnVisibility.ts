import type { Api, ColumnsConfig } from 'datatables.net'
import { onBeforeUnmount, onMounted, reactive, ref, type Ref } from 'vue'

export interface ToggleableColumn { key: string; label: string; index: number }

interface Options<M extends string> {
  storageKey: string
  mode: Ref<M>
  columns: Record<M, ColumnsConfig[]>
  getApi: () => Api | undefined
  /** ستون‌هایی که کاربر نمی‌تواند پنهان کند (بر اساس `name`). */
  lockedNames?: string[]
}

/**
 * نمایش/پنهان‌کردن ستون‌ها. نکات نسبت به نسخهٔ قبل:
 *  - لیست ستون‌ها و ایندکس‌ها از خود `columns` (name و title) ساخته می‌شود، نه آرایهٔ جدا با ایندکس دستی
 *  - «پنهان‌ها» ذخیره می‌شود، پس ستون جدیدی که بعداً اضافه شود پیش‌فرض نمایان است
 *  - در حالت serverSide از draw() استفاده نمی‌شود (درخواست اضافه به سرور می‌زد)
 */
export function useColumnVisibility<M extends string>(options: Options<M>) {
  const locked = new Set(options.lockedNames ?? ['actions', 'rowNumber'])
  const modes = Object.keys(options.columns) as M[]

  const toggleable = {} as Record<M, ToggleableColumn[]>
  for (const mode of modes) {
    toggleable[mode] = options.columns[mode]
      .map((column, index) => ({ key: String(column.name ?? ''), label: String(column.title ?? ''), index }))
      .filter(column => column.key && column.label && !locked.has(column.key))
  }

  function readHidden(): Record<M, string[]> {
    const empty = Object.fromEntries(modes.map(m => [m, [] as string[]])) as Record<M, string[]>
    try {
      const parsed = JSON.parse(window.localStorage.getItem(options.storageKey) ?? 'null') as Partial<Record<M, unknown>> | null
      if (!parsed) return empty
      for (const mode of modes) {
        const saved = parsed[mode]
        if (Array.isArray(saved)) empty[mode] = saved.filter((k): k is string => typeof k === 'string')
      }
    } catch { /* storage در دسترس نیست؛ برای همین نشست پیش‌فرض‌ها کافی است */ }
    return empty
  }

  const hidden = reactive(readHidden()) as Record<M, string[]>

  /** ستون‌ها با `visible` اعمال‌شده؛ برای ساخت options جدول استفاده کنید. */
  const resolvedColumns = {} as Record<M, ColumnsConfig[]>
  for (const mode of modes) {
    resolvedColumns[mode] = options.columns[mode].map((column, index) => {
      const item = toggleable[mode].find(c => c.index === index)
      return item ? { ...column, visible: !hidden[mode].includes(item.key) } : column
    })
  }

  const menuOpen = ref(false)

  const isVisible = (key: string): boolean => !hidden[options.mode.value].includes(key)

  function toggle(column: ToggleableColumn): void {
    const mode = options.mode.value
    const show = hidden[mode].includes(column.key)
    const visibleCount = toggleable[mode].filter(c => !hidden[mode].includes(c.key)).length
    if (!show && visibleCount <= 1) return

    hidden[mode] = show ? hidden[mode].filter(k => k !== column.key) : [...hidden[mode], column.key]
    try {
      window.localStorage.setItem(options.storageKey, JSON.stringify(hidden))
    } catch { /* ignore */ }

    const api = options.getApi()
    if (!api) return
    api.column(column.index).visible(show, false)
    api.columns.adjust()
    ;(api as unknown as { responsive?: { recalc(): void } }).responsive?.recalc()
  }

  const close = (): void => { menuOpen.value = false }
  onMounted(() => document.addEventListener('click', close))
  onBeforeUnmount(() => document.removeEventListener('click', close))

  return {
    resolvedColumns,
    toggleable,
    menuOpen,
    isVisible,
    toggle,
    toggleMenu: () => { menuOpen.value = !menuOpen.value },
    closeMenu: close,
  }
}
