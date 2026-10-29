/** Resolves after invoking print, independently of unreliable PDF viewer afterprint events. */
export function printPdf(pdf: Blob, title: string, signal?: AbortSignal): Promise<void> {
  if (signal?.aborted) return Promise.reject(new DOMException('Printing was cancelled.', 'AbortError'))

  return new Promise<void>((resolve, reject) => {
    const objectUrl = URL.createObjectURL(pdf)
    const frame = document.createElement('iframe')
    frame.title = title
    frame.setAttribute('aria-hidden', 'true')
    frame.tabIndex = -1
    // Keep the PDF viewer renderable; display:none can prevent printing in some browsers.
    frame.style.cssText = 'position:fixed;left:0;bottom:0;width:1px;height:1px;border:0;opacity:0;pointer-events:none'
    const previousFocus = document.activeElement
    let printWindow: Window | null = null
    let printTimer: ReturnType<typeof setTimeout> | null = null
    let cleanupTimer: ReturnType<typeof setTimeout> | null = null
    let cleaned = false
    let printingStarted = false

    const cleanup = (): void => {
      if (cleaned) return
      cleaned = true
      if (printTimer !== null) clearTimeout(printTimer)
      if (cleanupTimer !== null) clearTimeout(cleanupTimer)
      signal?.removeEventListener('abort', abort)
      window.removeEventListener('pagehide', abort)
      window.removeEventListener('afterprint', afterPrint)
      try { printWindow?.removeEventListener('afterprint', afterPrint) } catch { /* Viewer already unloaded. */ }
      frame.remove()
      URL.revokeObjectURL(objectUrl)
      if (previousFocus instanceof HTMLElement && previousFocus.isConnected) previousFocus.focus()
    }
    const afterPrint = (): void => {
      if (cleaned || !printingStarted) return
      cleanup()
    }
    const fail = (error: Error): void => { cleanup(); reject(error) }
    const abort = (): void => fail(new DOMException('Printing was cancelled.', 'AbortError'))

    signal?.addEventListener('abort', abort, { once: true })
    window.addEventListener('pagehide', abort, { once: true })
    window.addEventListener('afterprint', afterPrint)
    cleanupTimer = setTimeout(() => fail(new Error('بارگذاری فایل برای چاپ بیش از زمان مجاز طول کشید.')), 60000)
    frame.addEventListener('error', () => fail(new Error('بارگذاری فایل PDF برای چاپ انجام نشد.')), { once: true })
    frame.addEventListener('load', () => {
      if (cleaned) return
      if (cleanupTimer !== null) clearTimeout(cleanupTimer)
      printTimer = setTimeout(() => {
        if (cleaned) return
        try {
          printWindow = frame.contentWindow
          if (!printWindow) throw new Error('نمایش فایل برای چاپ انجام نشد.')
          // Firefox isolates its PDF viewer: print() is allowed, but event access can throw.
          // The parent afterprint listener and timeout still handle cleanup in that case.
          try { printWindow.addEventListener('afterprint', afterPrint) } catch { /* Protected PDF viewer. */ }
          // Some PDF viewers print asynchronously and do not emit afterprint.
          cleanupTimer = setTimeout(cleanup, 5 * 60 * 1000)
          try { printWindow.focus() } catch { /* Focusing the viewer is optional. */ }
          printingStarted = true
          printWindow.print()
          resolve()
        } catch {
          fail(new Error('مرورگر نتوانست پنجره چاپ PDF را باز کند.'))
        }
      }, 300)
    }, { once: true })
    frame.src = objectUrl
    document.body.append(frame)
  })
}
