const HIGHLIGHT_NAME = 'table-search'

function normalizeText(value: string): string {
  return value
    .replace(/[۰-۹]/g, c =>
      String(c.charCodeAt(0) - 0x06F0)
    )
    .replace(/[٠-٩]/g, c =>
      String(c.charCodeAt(0) - 0x0660)
    )
    .replace(/ي/g, 'ی')
    .replace(/ك/g, 'ک')
    .toLocaleLowerCase()
}

export function clearTableHighlight(): void {
  if ('highlights' in CSS) {
    CSS.highlights.delete(HIGHLIGHT_NAME)
  }
}

export function highlightTableText(
  container: HTMLElement | null,
  search: string
): void {
  clearTableHighlight()

  if (!container || !search.trim()) return

  if (
    !('highlights' in CSS) ||
    typeof Highlight === 'undefined'
  ) {
    return
  }

  const keyword = normalizeText(search.trim())
  const ranges: Range[] = []

  const cells = container.querySelectorAll(
    'table tbody tr td'
  )

  cells.forEach(cell => {
    const walker = document.createTreeWalker(
      cell,
      NodeFilter.SHOW_TEXT,
      {
        acceptNode(node) {
          const parent = node.parentElement

          if (!parent || !node.textContent?.trim()) {
            return NodeFilter.FILTER_REJECT
          }

          if (parent.closest(
            'button, input, textarea, select, script, style'
          )) {
            return NodeFilter.FILTER_REJECT
          }

          return NodeFilter.FILTER_ACCEPT
        }
      }
    )

    while (walker.nextNode()) {
      const node = walker.currentNode
      const original = node.textContent ?? ''
      const normalized = normalizeText(original)

      let position = 0

      while (position < normalized.length) {
        const index = normalized.indexOf(
          keyword,
          position
        )

        if (index === -1) break

        const range = document.createRange()

        range.setStart(node, index)
        range.setEnd(node, index + keyword.length)

        ranges.push(range)

        position = index + keyword.length
      }
    }
  })

  if (ranges.length > 0) {
    CSS.highlights.set(
      HIGHLIGHT_NAME,
      new Highlight(...ranges)
    )
  }
}
