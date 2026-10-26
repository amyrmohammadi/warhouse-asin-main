import { nextTick } from 'vue'

/**
 * ورودی‌ها داخل کامپوننت‌های فرزند هستند و template ref مستقیم ندارند؛
 * پس یک قرارداد واحد داریم: data-entry-row="<key>" data-entry-field="<name>"
 * روی خود input یا روی ریشه کامپوننت انتخابگر.
 */
export function entryFieldAttrs(rowKey: number, field: string) {
  return { 'data-entry-row': rowKey, 'data-entry-field': field }
}

export function focusEntryField(rowKey: number, field: string): void {
  void nextTick(() => {
    const holder = document.querySelector<HTMLElement>(
      `[data-entry-row="${rowKey}"][data-entry-field="${field}"]`,
    )
    const input = holder?.matches('input') ? (holder as HTMLInputElement) : holder?.querySelector('input')
    input?.focus()
    input?.select()
  })
}
