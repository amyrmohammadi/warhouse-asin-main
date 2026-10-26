import { onBeforeUnmount, ref, watch, type Ref } from 'vue'

interface Options {
  /** حداکثر ارتفاع منو (px) */
  maxHeight?: number
  /** حداقل عرض منو (px)؛ عرض پیش‌فرض برابر عرض anchor است */
  minWidth?: number
  gap?: number
}

/**
 * موقعیت‌دهی منوی بازشونده با `position: fixed` بر اساس مختصات anchor.
 * منو را باید با <Teleport to="body"> رندر کنید تا overflow والدها (مودال، جدول) آن را نبُرد.
 * اگر زیر anchor جا نباشد، خودکار به بالا باز می‌شود. با اسکرول و resize به‌روز می‌شود.
 */
export function useFloatingMenu(anchor: Ref<HTMLElement | null>, open: Ref<boolean>, options: Options = {}) {
  const maxHeight = options.maxHeight ?? 240
  const gap = options.gap ?? 4
  const style = ref<Record<string, string>>({})
  const placedAbove = ref(false)
  let frame: number | null = null

  function update(): void {
    const el = anchor.value
    if (!el) return
    const rect = el.getBoundingClientRect()
    const vw = window.innerWidth
    const vh = window.innerHeight

    const width = Math.min(Math.max(rect.width, options.minWidth ?? 0), vw - 16)
    const rtl = window.getComputedStyle(el).direction === 'rtl'
    const left = Math.min(Math.max(8, rtl ? rect.right - width : rect.left), vw - width - 8)

    const below = vh - rect.bottom - gap - 8
    const above = rect.top - gap - 8
    const up = below < Math.min(maxHeight, 160) && above > below
    const available = Math.max(80, up ? above : below)
    placedAbove.value = up

    style.value = {
      position: 'fixed',
      left: `${left}px`,
      width: `${width}px`,
      maxHeight: `${Math.min(maxHeight, available)}px`,
      ...(up ? { bottom: `${vh - rect.top + gap}px` } : { top: `${rect.bottom + gap}px` }),
    }
  }

  function schedule(): void {
    if (frame !== null) return
    frame = requestAnimationFrame(() => { frame = null; update() })
  }

  function listen(): void {
    window.addEventListener('resize', schedule)
    window.addEventListener('scroll', schedule, true) // capture: اسکرول هر والدی
  }
  function unlisten(): void {
    window.removeEventListener('resize', schedule)
    window.removeEventListener('scroll', schedule, true)
    if (frame !== null) { cancelAnimationFrame(frame); frame = null }
  }

  watch(open, isOpen => {
    if (isOpen) { update(); listen() } else unlisten()
  }, { immediate: true, flush: 'post' })

  onBeforeUnmount(unlisten)

  return { style, placedAbove, update }
}
