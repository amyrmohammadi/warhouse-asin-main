import { onBeforeUnmount, type Ref } from 'vue'

type RtlScrollType = 'default' | 'negative' | 'reverse'
let cachedRtlType: RtlScrollType | null = null

function rtlScrollType(): RtlScrollType {
  if (cachedRtlType) return cachedRtlType
  const outer = document.createElement('div')
  const inner = document.createElement('div')
  outer.dir = 'rtl'
  outer.style.cssText = 'height:1px;overflow:scroll;position:absolute;top:-1000px;width:4px;'
  inner.style.width = '8px'
  outer.append(inner)
  document.body.append(outer)
  if (outer.scrollLeft > 0) cachedRtlType = 'default'
  else {
    outer.scrollLeft = 1
    cachedRtlType = outer.scrollLeft === 0 ? 'negative' : 'reverse'
  }
  outer.remove()
  return cachedRtlType
}

const isRtl = (el: HTMLElement): boolean => window.getComputedStyle(el).direction === 'rtl'
const maxScroll = (el: HTMLElement): number => el.scrollWidth - el.clientWidth

export function getNormalizedScrollLeft(el: HTMLElement): number {
  if (!isRtl(el)) return el.scrollLeft
  switch (rtlScrollType()) {
    case 'negative': return maxScroll(el) + el.scrollLeft
    case 'reverse': return maxScroll(el) - el.scrollLeft
    default: return el.scrollLeft
  }
}

export function setNormalizedScrollLeft(el: HTMLElement, value: number): void {
  if (!isRtl(el)) { el.scrollLeft = value; return }
  switch (rtlScrollType()) {
    case 'negative': el.scrollLeft = value - maxScroll(el); break
    case 'reverse': el.scrollLeft = maxScroll(el) - value; break
    default: el.scrollLeft = value
  }
}

/**
 * اسکرول‌بار افقی فشردهٔ زیر جدول + Ctrl+Wheel برای اسکرول افقی.
 * منطق همگام‌سازی همان نسخهٔ قبلی است، فقط از صفحه جدا شده.
 * نکته: اگر CSS شما `.data-table-horizontal-scrollbar` را `direction: ltr` نکرده باشد،
 * در مرورگرهای RTL «negative» همگام‌سازی نوار دقیق نیست (همان رفتار قبلی حفظ شده).
 */
export function useCompactScrollbar(container: Ref<HTMLElement | null>) {
  let body: HTMLElement | null = null
  let bar: HTMLDivElement | null = null
  let barContent: HTMLDivElement | null = null
  let observer: ResizeObserver | null = null
  let syncing = false
  let frame: number | null = null

  function update(): void {
    if (!body || !bar || !barContent) return
    const tableMax = Math.max(0, maxScroll(body))
    bar.hidden = tableMax === 0
    if (tableMax === 0) return
    const viewport = bar.clientWidth || 300
    barContent.style.width = `${Math.max(viewport + 1, viewport * (body.scrollWidth / body.clientWidth))}px`
    bar.scrollLeft = maxScroll(bar) * (getNormalizedScrollLeft(body) / tableMax)
  }

  function syncBarFromTable(): void {
    if (syncing || !body || !bar) return
    const tableMax = maxScroll(body)
    const barMax = maxScroll(bar)
    if (tableMax <= 0 || barMax <= 0) return
    syncing = true
    bar.scrollLeft = barMax * (getNormalizedScrollLeft(body) / tableMax)
    syncing = false
  }

  function syncTableFromBar(): void {
    if (syncing || !body || !bar) return
    const tableMax = maxScroll(body)
    const barMax = maxScroll(bar)
    if (tableMax <= 0 || barMax <= 0) return
    syncing = true
    setNormalizedScrollLeft(body, tableMax * (bar.scrollLeft / barMax))
    syncing = false
  }

  function disconnect(): void {
    observer?.disconnect()
    observer = null
    body?.removeEventListener('scroll', syncBarFromTable)
    bar?.removeEventListener('scroll', syncTableFromBar)
    bar?.remove()
    body = bar = barContent = null
  }

  function connect(): void {
    const nextBody = container.value?.querySelector<HTMLElement>('.dt-layout-table > div') ?? null
    const wrapper = container.value?.querySelector<HTMLElement>('.dt-layout-table') ?? null
    if (!nextBody || !wrapper) return
    if (nextBody === body && bar?.isConnected) { update(); return }

    disconnect()
    body = nextBody
    bar = document.createElement('div')
    bar.className = 'data-table-horizontal-scrollbar'
    bar.setAttribute('role', 'scrollbar')
    bar.setAttribute('aria-label', 'اسکرول افقی جدول')
    barContent = document.createElement('div')
    barContent.className = 'data-table-horizontal-scrollbar__content'
    bar.append(barContent)
    wrapper.insertAdjacentElement('afterend', bar)

    body.addEventListener('scroll', syncBarFromTable, { passive: true })
    bar.addEventListener('scroll', syncTableFromBar, { passive: true })
    observer = new ResizeObserver(update)
    observer.observe(body)
    update()
  }

  /** بعد از هر draw صدا بزنید. */
  function schedule(): void {
    if (frame !== null) cancelAnimationFrame(frame)
    frame = requestAnimationFrame(() => { frame = null; connect() })
  }

  function onWheel(event: WheelEvent): void {
    if (!event.ctrlKey || Math.abs(event.deltaY) < Math.abs(event.deltaX)) return
    const scrollBody = container.value?.querySelector<HTMLElement>('.dt-layout-table > div')
    if (!scrollBody || maxScroll(scrollBody) <= 0) return
    event.preventDefault()
    const direction = isRtl(scrollBody) ? -1 : 1
    const next = Math.min(maxScroll(scrollBody), Math.max(0, getNormalizedScrollLeft(scrollBody) + event.deltaY * direction))
    setNormalizedScrollLeft(scrollBody, next)
  }

  onBeforeUnmount(() => {
    if (frame !== null) cancelAnimationFrame(frame)
    disconnect()
  })

  return { schedule, disconnect, onWheel }
}
