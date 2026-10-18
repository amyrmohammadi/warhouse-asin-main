<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue'

type RtlScrollType = 'default' | 'negative' | 'reverse'

const root = ref<HTMLElement | null>(null)
const desktopMediaQuery = window.matchMedia('(min-width: 992px)')

let tableScrollBody: HTMLElement | null = null
let compactScrollbar: HTMLDivElement | null = null
let compactScrollbarContent: HTMLDivElement | null = null
let tableResizeObserver: ResizeObserver | null = null
let tableMutationObserver: MutationObserver | null = null
let scrollSyncInProgress = false
let rtlScrollType: RtlScrollType | null = null
let connectionFrame = 0

function getRtlScrollType(): RtlScrollType {
  if (rtlScrollType) return rtlScrollType

  const outer = document.createElement('div')
  const inner = document.createElement('div')
  outer.dir = 'rtl'
  outer.style.cssText = 'height:1px;overflow:scroll;position:absolute;top:-1000px;width:4px;'
  inner.style.width = '8px'
  outer.append(inner)
  document.body.append(outer)

  if (outer.scrollLeft > 0) {
    rtlScrollType = 'default'
  } else {
    outer.scrollLeft = 1
    rtlScrollType = outer.scrollLeft === 0 ? 'negative' : 'reverse'
  }

  outer.remove()
  return rtlScrollType
}

function normalizedScrollLeft(element: HTMLElement): number {
  if (window.getComputedStyle(element).direction !== 'rtl') return element.scrollLeft

  const maximum = element.scrollWidth - element.clientWidth
  switch (getRtlScrollType()) {
    case 'negative': return maximum + element.scrollLeft
    case 'reverse': return maximum - element.scrollLeft
    default: return element.scrollLeft
  }
}

function setNormalizedScrollLeft(element: HTMLElement, value: number): void {
  if (window.getComputedStyle(element).direction !== 'rtl') {
    element.scrollLeft = value
    return
  }

  const maximum = element.scrollWidth - element.clientWidth
  switch (getRtlScrollType()) {
    case 'negative':
      element.scrollLeft = value - maximum
      break
    case 'reverse':
      element.scrollLeft = maximum - value
      break
    default:
      element.scrollLeft = value
  }
}

function updateScrollbar(): void {
  if (!tableScrollBody || !compactScrollbar || !compactScrollbarContent) return

  const tableMaximum = Math.max(0, tableScrollBody.scrollWidth - tableScrollBody.clientWidth)
  compactScrollbar.hidden = tableMaximum === 0 || tableScrollBody.clientWidth === 0
  compactScrollbar.setAttribute('aria-valuemax', String(tableMaximum))
  compactScrollbar.setAttribute('aria-valuenow', String(Math.round(normalizedScrollLeft(tableScrollBody))))
  if (compactScrollbar.hidden) return

  const scrollbarViewport = compactScrollbar.clientWidth || 300
  const contentWidth = scrollbarViewport * (tableScrollBody.scrollWidth / tableScrollBody.clientWidth)
  compactScrollbarContent.style.width = `${Math.max(scrollbarViewport + 1, contentWidth)}px`

  const scrollbarMaximum = compactScrollbar.scrollWidth - compactScrollbar.clientWidth
  compactScrollbar.scrollLeft = scrollbarMaximum * (normalizedScrollLeft(tableScrollBody) / tableMaximum)
}

function syncScrollbarFromTable(): void {
  if (scrollSyncInProgress || !tableScrollBody || !compactScrollbar) return

  const tableMaximum = tableScrollBody.scrollWidth - tableScrollBody.clientWidth
  const scrollbarMaximum = compactScrollbar.scrollWidth - compactScrollbar.clientWidth
  if (tableMaximum <= 0 || scrollbarMaximum <= 0) return

  scrollSyncInProgress = true
  compactScrollbar.scrollLeft = scrollbarMaximum * (normalizedScrollLeft(tableScrollBody) / tableMaximum)
  compactScrollbar.setAttribute('aria-valuenow', String(Math.round(normalizedScrollLeft(tableScrollBody))))
  scrollSyncInProgress = false
}

function syncTableFromScrollbar(): void {
  if (scrollSyncInProgress || !tableScrollBody || !compactScrollbar) return

  const tableMaximum = tableScrollBody.scrollWidth - tableScrollBody.clientWidth
  const scrollbarMaximum = compactScrollbar.scrollWidth - compactScrollbar.clientWidth
  if (tableMaximum <= 0 || scrollbarMaximum <= 0) return

  scrollSyncInProgress = true
  const nextPosition = tableMaximum * (compactScrollbar.scrollLeft / scrollbarMaximum)
  setNormalizedScrollLeft(tableScrollBody, nextPosition)
  compactScrollbar.setAttribute('aria-valuenow', String(Math.round(nextPosition)))
  scrollSyncInProgress = false
}

function disconnectScrollbar(): void {
  tableResizeObserver?.disconnect()
  tableResizeObserver = null
  tableScrollBody?.removeEventListener('scroll', syncScrollbarFromTable)
  compactScrollbar?.removeEventListener('scroll', syncTableFromScrollbar)
  compactScrollbar?.remove()
  tableScrollBody = null
  compactScrollbar = null
  compactScrollbarContent = null
}

function connectScrollbar(): void {
  if (!desktopMediaQuery.matches) {
    disconnectScrollbar()
    return
  }

  const nextScrollBody = root.value?.querySelector<HTMLElement>('.dt-layout-table > div') ?? null
  const scrollWrapper = root.value?.querySelector<HTMLElement>('.dt-layout-table') ?? null
  if (!nextScrollBody || !scrollWrapper) return
  if (nextScrollBody === tableScrollBody && compactScrollbar?.isConnected) {
    updateScrollbar()
    return
  }

  disconnectScrollbar()
  tableScrollBody = nextScrollBody
  compactScrollbar = document.createElement('div')
  compactScrollbar.className = 'data-table-horizontal-scrollbar'
  compactScrollbar.setAttribute('aria-label', 'اسکرول افقی جدول')
  compactScrollbar.setAttribute('aria-valuemin', '0')
  compactScrollbar.setAttribute('role', 'scrollbar')
  compactScrollbarContent = document.createElement('div')
  compactScrollbarContent.className = 'data-table-horizontal-scrollbar__content'
  compactScrollbar.append(compactScrollbarContent)
  scrollWrapper.insertAdjacentElement('afterend', compactScrollbar)

  tableScrollBody.addEventListener('scroll', syncScrollbarFromTable, { passive: true })
  compactScrollbar.addEventListener('scroll', syncTableFromScrollbar, { passive: true })
  tableResizeObserver = new ResizeObserver(updateScrollbar)
  tableResizeObserver.observe(tableScrollBody)
  const table = tableScrollBody.querySelector('table')
  if (table) tableResizeObserver.observe(table)
  updateScrollbar()
}

function scheduleConnection(): void {
  window.cancelAnimationFrame(connectionFrame)
  connectionFrame = window.requestAnimationFrame(connectScrollbar)
}

function handleWheel(event: WheelEvent): void {
  if (!desktopMediaQuery.matches || !event.ctrlKey || Math.abs(event.deltaY) < Math.abs(event.deltaX)) return
  if (!tableScrollBody || tableScrollBody.scrollWidth <= tableScrollBody.clientWidth) return

  event.preventDefault()
  const maximum = tableScrollBody.scrollWidth - tableScrollBody.clientWidth
  const direction = window.getComputedStyle(tableScrollBody).direction === 'rtl' ? -1 : 1
  const nextPosition = Math.min(
    maximum,
    Math.max(0, normalizedScrollLeft(tableScrollBody) + (event.deltaY * direction)),
  )
  setNormalizedScrollLeft(tableScrollBody, nextPosition)
}

onMounted(async () => {
  await nextTick()
  tableMutationObserver = new MutationObserver(scheduleConnection)
  if (root.value) tableMutationObserver.observe(root.value, { childList: true, subtree: true })
  desktopMediaQuery.addEventListener('change', scheduleConnection)
  window.addEventListener('resize', scheduleConnection, { passive: true })
  scheduleConnection()
})

onBeforeUnmount(() => {
  window.cancelAnimationFrame(connectionFrame)
  tableMutationObserver?.disconnect()
  desktopMediaQuery.removeEventListener('change', scheduleConnection)
  window.removeEventListener('resize', scheduleConnection)
  disconnectScrollbar()
})
</script>

<template>
  <div ref="root" class="data-table-scroll-container" @wheel="handleWheel">
    <slot />
  </div>
</template>
