import { reactive } from 'vue'

/** خطاهای اعتبارسنجی فرم؛ کلید سطرها `row.<key>.<field>` است. */
export function useFieldErrors() {
  const errors = reactive<Record<string, string>>({})

  const rowKey = (rowId: number, field: string): string => `row.${rowId}.${field}`

  const get = (key: string): string => errors[key] ?? ''
  const set = (key: string, message: string): void => { errors[key] = message }
  const clear = (key: string): void => { delete errors[key] }
  function clearAll(): void {
    for (const key of Object.keys(errors)) delete errors[key]
  }
  const hasAny = (): boolean => Object.keys(errors).length > 0

  const getRow = (rowId: number, field: string): string => get(rowKey(rowId, field))
  const setRow = (rowId: number, field: string, message: string): void => set(rowKey(rowId, field), message)
  function clearRow(rowId: number, field: string): void {
    clear(rowKey(rowId, field))
    clear('rows')
  }

  return { errors, get, set, clear, clearAll, hasAny, getRow, setRow, clearRow, rowKey }
}
