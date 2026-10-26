import { computed, ref, watch, type Ref } from 'vue'

export type EntryMode = 'cards' | 'list'

interface Options<T extends { key: number }> {
  create: (key: number) => T
  hasContent: (row: T) => boolean
  minimumRows?: number
  initialMode?: EntryMode
}

/**
 * مدیریت ردیف‌های فرم ورود/خروج کالا:
 * - حالت جدولی با «همیشه یک ردیف خالی در انتها» (با یک watch، نه فراخوانی دستی در ده‌ها handler)
 * - جابه‌جایی بین حالت کارتی و جدولی
 * - ارث‌بری فیلدها از ردیف قبلی
 */
export function useEntryRows<T extends { key: number }>(options: Options<T>) {
  const minimumRows = options.minimumRows ?? 4
  let sequence = 0

  const mode = ref<EntryMode>(options.initialMode ?? 'list')
  const rows = ref([]) as Ref<T[]>

  const newRow = (): T => options.create(++sequence)

  function ensureRows(): void {
    if (mode.value !== 'list') return
    while (rows.value.length < minimumRows) rows.value.push(newRow())
    const last = rows.value.at(-1)
    if (last && options.hasContent(last)) rows.value.push(newRow())
  }

  watch([rows, mode], ensureRows, { deep: true })

  const rowsForSubmission = computed(() =>
    mode.value === 'list' ? rows.value.filter(options.hasContent) : rows.value,
  )

  function reset(items: T[] = [], nextMode: EntryMode = 'list'): void {
    mode.value = nextMode
    rows.value = items.length ? items : [newRow()]
    ensureRows()
  }

  function setMode(next: EntryMode): void {
    if (mode.value === next) return
    const populated = rows.value.filter(options.hasContent)
    mode.value = next
    rows.value = populated.length ? populated : [newRow()]
    ensureRows()
  }

  function addRow(): void {
    rows.value.push(newRow())
  }

  function removeRow(index: number): void {
    if (mode.value === 'cards' && rows.value.length === 1) rows.value[0] = newRow()
    else rows.value.splice(index, 1)
  }

  /**
   * اگر همهٔ فیلدهای `fields` در این ردیف خالی است و ردیف قبلی محتوا دارد،
   * مقدارها را از آن کپی می‌کند (مثلاً انبار/مقصد).
   * فقط وقتی صدا بزنید که کاربر واقعاً روی همین ردیف کالا انتخاب/وارد کرد.
   */
  function inheritFromPrevious<K extends keyof T>(row: T, fields: K[]): void {
    const isEmpty = (value: unknown) => value === null || value === undefined || value === ''
    if (!fields.every(field => isEmpty(row[field]))) return
    const index = rows.value.findIndex(candidate => candidate.key === row.key)
    const previous = index > 0 ? rows.value[index - 1] : undefined
    if (!previous || !options.hasContent(previous)) return
    for (const field of fields) row[field] = previous[field]
  }

  return { mode, rows, rowsForSubmission, newRow, reset, setMode, addRow, removeRow, inheritFromPrevious }
}
