import { nextTick, onBeforeUnmount, ref, watch, type Ref } from 'vue'

interface Options {
  /** خود المنت منو؛ برای سنجش موقعیت واقعی و جبران آفست والدهای transform/zoom */
  menu?: Ref<HTMLElement | null>
  /** حداکثر ارتفاع منو (px) */
  maxHeight?: number
  /** حداقل عرض منو (px) یا تابعی که هنگام باز شدن آن را می‌خواند؛ عرض پیش‌فرض برابر عرض anchor است */
  minWidth?: number | (() => number)
  gap?: number
}

/**
 * موقعیت‌دهی منوی بازشونده با `position: fixed` بر اساس مختصات anchor.
 * منو را باید با <Teleport to="body"> رندر کنید تا overflow والدها (مودال، جدول) آن را نبُرد.
 *
 * - اگر زیر anchor جا نباشد، خودکار به بالا باز می‌شود.
 * - لبهٔ هم‌ترازی در RTL راست و در LTR چپ است؛ پس اگر عرض واقعی منو کمی فرق کند، منو از anchor «نمی‌لغزد».
 * - تا موقعیت نهایی سنجیده نشده منو hidden است (پرش فریم اول ندارد).
 * - بعد از رندر، موقعیت واقعی با موقعیت مطلوب مقایسه و اختلاف جبران می‌شود
 *   (اگر html/body با transform، zoom یا filter، containing block متفاوتی بسازند).
 */
export function useFloatingMenu(anchor: Ref<HTMLElement | null>, open: Ref<boolean>, options: Options = {}) {
  const maxHeight = options.maxHeight ?? 240
  const gap = options.gap ?? 4
  const style = ref<Record<string, string>>({ position: 'fixed', top: '0px', left: '0px', visibility: 'hidden' })
  const placedAbove = ref(false)
  let frame: number | null = null
  let token = 0

  async function place(): Promise<void> {
    const el = anchor.value
    if (!el) return
    const current = ++token
    const rect = el.getBoundingClientRect()
    const vw = window.innerWidth
    const vh = window.innerHeight

    const minWidth = typeof options.minWidth === 'function' ? options.minWidth() : (options.minWidth ?? 0)
    const width = Math.min(Math.max(rect.width, minWidth), vw - 16)
    const rtl = window.getComputedStyle(el).direction === 'rtl'

    const wantedLeft = Math.min(Math.max(8, rtl ? rect.right - width : rect.left), vw - width - 8)
    const below = vh - rect.bottom - gap - 8
    const above = rect.top - gap - 8
    const up = below < Math.min(maxHeight, 160) && above > below
    const available = Math.max(80, up ? above : below)
    placedAbove.value = up

    // لبهٔ عمودی مطلوب: بالای منو (حالت پایین) یا پایین منو (حالت بالا)
    const wantedEdgeY = up ? rect.top - gap : rect.bottom + gap
    const pos = { left: wantedLeft, offsetY: up ? vh - wantedEdgeY : wantedEdgeY }

    const apply = (visible: boolean): void => {
      style.value = {
        position: 'fixed',
        left: `${pos.left}px`,
        width: `${width}px`,
        maxHeight: `${Math.min(maxHeight, available)}px`,
        visibility: visible ? 'visible' : 'hidden',
        ...(up ? { bottom: `${pos.offsetY}px` } : { top: `${pos.offsetY}px` }),
      }
    }

    const alreadyVisible = style.value.visibility === 'visible'
    apply(alreadyVisible)
    await nextTick()

    // جبران آفست: حداکثر سه بار (برای zoom که اثرش ضریبی است)
    for (let i = 0; i < 3; i++) {
      const menuEl = options.menu?.value
      if (!menuEl || current !== token) break
      const actual = menuEl.getBoundingClientRect()
      const dx = (rtl ? actual.right - (wantedLeft + width) : actual.left - wantedLeft)
      const dy = up ? actual.bottom - wantedEdgeY : actual.top - wantedEdgeY
      if (Math.abs(dx) <= 1 && Math.abs(dy) <= 1) break
      pos.left -= dx
      pos.offsetY += up ? dy : -dy
      apply(alreadyVisible)
      await nextTick()
    }

    if (current === token && open.value) apply(true)
  }

  function schedule(): void {
    if (frame !== null) return
    frame = requestAnimationFrame(() => { frame = null; void place() })
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
    if (isOpen) {
      style.value = { ...style.value, visibility: 'hidden' }
      void place()
      listen()
    } else {
      token++
      unlisten()
    }
  }, { immediate: true, flush: 'post' })

  onBeforeUnmount(unlisten)

  return { style, placedAbove, update: place }
}
