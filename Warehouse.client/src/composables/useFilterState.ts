import { computed, reactive, ref, watch } from 'vue'

import type {
  DataTableFilterValue,
  DataTableFilterValues,
  DateFilterValue,
  NumericFilterValue,
  TextFilterValue,
} from '@/types/dataTableFilters'

const TEXT_OPS = ['contains', 'eq'] as const
const DATE_OPS = ['before', 'after', 'eq', 'between'] as const
const NUMERIC_OPS = ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between'] as const

export type SetFilterValue = (value: DataTableFilterValue) => void
interface Selection { id: string | null; text: string }

/**
 * تمام state فیلترها (جستجوی سریع، فیلترهای اعمال‌شده و برچسب گزینه‌های انتخاب‌شده)
 * در یک جا. افزودن فیلتر جدید دیگر به دست‌بردن در ۴ تابع نیاز ندارد.
 */
export function useFilterState(onChange: () => void) {
  const search = ref('')
  const applied = ref<DataTableFilterValues>({})
  const selections = reactive<Record<string, Selection>>({})

  const searchQuery = computed(() => search.value.trim() || undefined)

  // وقتی یک فیلتر برداشته شد، برچسب انتخابش هم پاک شود
  watch(applied, filters => {
    for (const key of Object.keys(selections)) if (!filters[key]) delete selections[key]
  }, { deep: true })

  function operatorValue<T>(key: string, allowed: readonly string[]): T | undefined {
    const value = applied.value[key]
    return typeof value === 'object' && value !== null && 'operator' in value
      && allowed.includes(String(value.operator))
      ? (value as T) : undefined
  }

  const text = (key: string) => operatorValue<TextFilterValue>(key, TEXT_OPS)
  const date = (key: string) => operatorValue<DateFilterValue>(key, DATE_OPS)
  const numeric = (key: string) => operatorValue<NumericFilterValue>(key, NUMERIC_OPS)
  function id(key: string): string | undefined {
    const value = applied.value[key]
    return typeof value === 'string' && value.trim() ? value.trim() : undefined
  }

  /** گزینهٔ تک‌عضوی برای فیلترهای `select` که گزینه‌هایشان از سرور جستجو می‌شود. */
  function selectedOption(key: string) {
    const value = id(key)
    const label = selections[key]?.text
    return value && label ? [{ value, label }] : []
  }

  function selection(key: string): Selection {
    return selections[key] ?? { id: null, text: '' }
  }
  function setSelection(key: string, next: Partial<Selection>): void {
    selections[key] = { ...selection(key), ...next }
  }

  function onSearch(value: string): void {
    search.value = value
    onChange()
  }
  function onApply(filters: DataTableFilterValues): void {
    applied.value = filters
    onChange()
  }
  function onRemove(filters: DataTableFilterValues): void {
    applied.value = filters
    onChange()
  }
  function onClear(): void {
    search.value = ''
    applied.value = {}
    for (const key of Object.keys(selections)) delete selections[key]
    onChange()
  }

  return {
    search, searchQuery, applied,
    text, date, numeric, id,
    selectedOption, selection, setSelection,
    onSearch, onApply, onRemove, onClear,
  }
}
