<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import DateFilter from '@/components/filters/DateFilter.vue'
import NumericFilter from '@/components/filters/NumericFilter.vue'
import TextFilter from '@/components/filters/TextFilter.vue'
import type {
  DataTableFilterDefinition,
  DataTableFilterValue,
  DataTableFilterValues,
  DateFilterValue,
  DateRangeFilterValue,
  FilterOption,
  NumericFilterValue,
  TextFilterValue,
} from '@/types/dataTableFilters'

const props = withDefaults(
  defineProps<{
    definitions: DataTableFilterDefinition[]
    modelValue: DataTableFilterValues
    search: string
    searchPlaceholder?: string
    loading?: boolean
    debounceMs?: number
  }>(),
  { searchPlaceholder: 'جستجو...', loading: false, debounceMs: 400 },
)

const emit = defineEmits<{
  'update:modelValue': [value: DataTableFilterValues]
  'update:search': [value: string]
  apply: [value: DataTableFilterValues]
  clear: []
  remove: [value: DataTableFilterValues]
}>()

const panelOpen = ref(false)
const searchValue = ref(props.search)
const draftFilters = ref<DataTableFilterValues>(cloneValues(props.modelValue))
const errors = ref<Record<string, string>>({})
const resolvedOptions = ref<Record<string, FilterOption[]>>({})
const loadingOptions = ref<Record<string, boolean>>({})
let searchTimer: ReturnType<typeof setTimeout> | null = null

const appliedChips = computed(() =>
  props.definitions
    .filter((definition) => hasValue(props.modelValue[definition.key]))
    .map((definition) => ({
      key: definition.key,
      label: definition.label,
      value: formatValue(definition, props.modelValue[definition.key]),
    })),
)
const activeFilterCount = computed(() => appliedChips.value.length)

watch(
  () => props.search,
  (value) => {
    if (value !== searchValue.value) searchValue.value = value
  },
)
watch(
  () => props.modelValue,
  (value) => {
    if (!panelOpen.value) draftFilters.value = cloneValues(value)
  },
  { deep: true },
)

/**
 * گزینه‌های فیلترهای انتخابی تنها زمانی بارگذاری می‌شوند که کاربر پنل فیلترها را باز کند؛
 * تا آن زمان فهرست کامل داده‌ها از سرور دریافت نمی‌شود.
 */
async function loadPendingOptions(): Promise<void> {
  await Promise.all(
    props.definitions.map(async (definition) => {
      if (definition.type !== 'select' && definition.type !== 'multiselect') return
      if (!definition.loadOptions) return
      if (resolvedOptions.value[definition.key]) return

      loadingOptions.value[definition.key] = true
      try {
        resolvedOptions.value[definition.key] = await definition.loadOptions()
      } catch {
        resolvedOptions.value[definition.key] = []
      } finally {
        loadingOptions.value[definition.key] = false
      }
    }),
  )
}

onBeforeUnmount(() => {
  if (searchTimer) clearTimeout(searchTimer)
})

function togglePanel(): void {
  panelOpen.value = !panelOpen.value
  if (panelOpen.value) {
    draftFilters.value = cloneValues(props.modelValue)
    errors.value = {}
    void loadPendingOptions()
  }
}

function updateSearch(event: Event): void {
  searchValue.value = (event.target as HTMLInputElement).value
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => emit('update:search', searchValue.value.trim()), props.debounceMs)
}

function clearSearch(): void {
  if (searchTimer) clearTimeout(searchTimer)
  searchValue.value = ''
  emit('update:search', '')
}

function applyFilters(): void {
  const normalized = normalizeAndValidate()
  if (!normalized) return
  emit('update:modelValue', normalized)
  emit('apply', normalized)
}

function clearAll(): void {
  if (searchTimer) clearTimeout(searchTimer)
  searchValue.value = ''
  draftFilters.value = {}
  errors.value = {}
  panelOpen.value = false
  emit('update:search', '')
  emit('update:modelValue', {})
  emit('clear')
}

function removeFilter(key: string): void {
  const next = cloneValues(props.modelValue)
  delete next[key]
  draftFilters.value = cloneValues(next)
  emit('update:modelValue', next)
  emit('remove', next)
}

function setDraftValue(key: string, value: DataTableFilterValue): void {
  draftFilters.value = { ...draftFilters.value, [key]: value }
  delete errors.value[key]
}

function updateSimpleValue(key: string, event: Event): void {
  setDraftValue(key, (event.target as HTMLInputElement | HTMLSelectElement).value)
}

function updateBooleanValue(key: string, event: Event): void {
  const value = (event.target as HTMLSelectElement).value
  setDraftValue(key, value === '' ? null : value === 'true')
}

function updateMultiSelect(key: string, event: Event): void {
  const select = event.target as HTMLSelectElement
  setDraftValue(key, Array.from(select.selectedOptions, (option) => option.value))
}

function updateDateRange(key: string, field: 'from' | 'to', event: Event): void {
  setDraftValue(key, {
    ...asDateRange(draftFilters.value[key]),
    [field]: (event.target as HTMLInputElement).value,
  })
}

function normalizeAndValidate(): DataTableFilterValues | null {
  const normalized: DataTableFilterValues = {}
  const nextErrors: Record<string, string> = {}

  for (const definition of props.definitions) {
    const rawValue = draftFilters.value[definition.key]
    if (!hasValue(rawValue)) continue

    if (definition.type === 'text') {
      const value = String(rawValue).trim()
      if (value) normalized[definition.key] = value
      continue
    }

    if (definition.type === 'text-comparison') {
      const value = asTextComparison(rawValue)
      const text = value?.value.trim() ?? ''
      if (!value || !text) {
        nextErrors[definition.key] = 'وارد کردن مقدار الزامی است.'
        continue
      }
      normalized[definition.key] = { operator: value.operator, value: text }
      continue
    }

    if (definition.type === 'date-comparison') {
      const value = asDateComparison(rawValue)
      if (!value?.value) {
        nextErrors[definition.key] = 'انتخاب تاریخ الزامی است.'
        continue
      }
      if (value.operator === 'between') {
        if (!value.valueTo) {
          nextErrors[definition.key] = 'انتخاب انتهای بازه الزامی است.'
          continue
        }
        if (value.value > value.valueTo) {
          nextErrors[definition.key] = 'ابتدای بازه نباید بعد از انتهای آن باشد.'
          continue
        }
        normalized[definition.key] = { ...value }
      } else {
        normalized[definition.key] = { operator: value.operator, value: value.value }
      }
      continue
    }

    if (definition.type === 'numeric-comparison') {
      const value = asNumeric(rawValue)
      if (!value || value.value === null || !Number.isFinite(value.value)) {
        nextErrors[definition.key] = 'وارد کردن مقدار الزامی است.'
        continue
      }
      if (definition.numericType === 'integer' && !Number.isInteger(value.value)) {
        nextErrors[definition.key] = 'فقط عدد صحیح مجاز است.'
        continue
      }
      if (definition.min !== undefined && value.value < definition.min) {
        nextErrors[definition.key] = `مقدار نباید کمتر از ${definition.min} باشد.`
        continue
      }
      if (value.operator === 'between') {
        if (value.valueTo === null || value.valueTo === undefined || !Number.isFinite(value.valueTo)) {
          nextErrors[definition.key] = 'وارد کردن انتهای بازه الزامی است.'
          continue
        }
        if (definition.numericType === 'integer' && !Number.isInteger(value.valueTo)) {
          nextErrors[definition.key] = 'فقط عدد صحیح مجاز است.'
          continue
        }
        if (definition.min !== undefined && value.valueTo < definition.min) {
          nextErrors[definition.key] = `مقدار نباید کمتر از ${definition.min} باشد.`
          continue
        }
        if (value.value > value.valueTo) {
          nextErrors[definition.key] = 'ابتدای بازه نباید از انتهای آن بزرگ‌تر باشد.'
          continue
        }
        normalized[definition.key] = { ...value }
      } else {
        normalized[definition.key] = { operator: value.operator, value: value.value }
      }
      continue
    }

    if (definition.type === 'date-range') {
      const value = asDateRange(rawValue)
      if (value.from && value.to && value.from > value.to) {
        nextErrors[definition.key] = 'تاریخ شروع نباید بعد از تاریخ پایان باشد.'
        continue
      }
    }
    normalized[definition.key] = rawValue
  }

  errors.value = nextErrors
  return Object.keys(nextErrors).length > 0 ? null : normalized
}

function optionsFor(definition: DataTableFilterDefinition): FilterOption[] {
  if (definition.type !== 'select' && definition.type !== 'multiselect') return []
  return resolvedOptions.value[definition.key] ?? definition.options ?? []
}

function cloneValues(values: DataTableFilterValues): DataTableFilterValues {
  return Object.fromEntries(
    Object.entries(values).map(([key, value]) => [
      key,
      Array.isArray(value)
        ? [...value]
        : value && typeof value === 'object'
          ? { ...value }
          : value,
    ]),
  )
}

function hasValue(value: DataTableFilterValue | undefined): boolean {
  if (value === null || value === undefined || value === '') return false
  if (Array.isArray(value)) return value.length > 0
  if (isTextComparison(value) || isDateComparison(value)) return Boolean(value.value)
  if (isNumeric(value)) return value.value !== null
  if (isDateRange(value)) return Boolean(value.from || value.to)
  return true
}

function asNumeric(value: DataTableFilterValue | undefined): NumericFilterValue | null {
  return isNumeric(value) ? value : null
}

function asTextComparison(value: DataTableFilterValue | undefined): TextFilterValue | null {
  return isTextComparison(value) ? value : null
}

function asDateComparison(value: DataTableFilterValue | undefined): DateFilterValue | null {
  return isDateComparison(value) ? value : null
}

function asDateRange(value: DataTableFilterValue | undefined): DateRangeFilterValue {
  return isDateRange(value) ? value : { from: '', to: '' }
}

function asStringArray(value: DataTableFilterValue | undefined): string[] {
  return Array.isArray(value) ? value : []
}

function isNumeric(value: unknown): value is NumericFilterValue {
  return Boolean(
    value
      && typeof value === 'object'
      && 'operator' in value
      && ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between'].includes(String(value.operator))
      && 'value' in value
      && (typeof value.value === 'number' || value.value === null)
  )
}

function isTextComparison(value: unknown): value is TextFilterValue {
  return Boolean(
    value
      && typeof value === 'object'
      && 'operator' in value
      && ['contains', 'eq'].includes(String(value.operator))
      && 'value' in value
      && typeof value.value === 'string',
  )
}

function isDateComparison(value: unknown): value is DateFilterValue {
  return Boolean(
    value
      && typeof value === 'object'
      && 'operator' in value
      && ['before', 'after', 'eq', 'between'].includes(String(value.operator))
      && 'value' in value
      && typeof value.value === 'string',
  )
}

function isDateRange(value: unknown): value is DateRangeFilterValue {
  return Boolean(value && typeof value === 'object' && 'from' in value && 'to' in value)
}

function formatValue(
  definition: DataTableFilterDefinition,
  value: DataTableFilterValue | undefined,
): string {
  if (definition.type === 'numeric-comparison') {
    const numeric = asNumeric(value)
    if (!numeric) return ''
    const labels: Record<string, string> = {
      eq: '=', neq: '≠', gt: '>', gte: '≥', lt: '<', lte: '≤', between: 'تا',
    }
    return numeric.operator === 'between'
      ? `${numeric.value} تا ${numeric.valueTo}`
      : `${labels[numeric.operator]} ${numeric.value}`
  }
  if (definition.type === 'text-comparison') {
    const text = asTextComparison(value)
    if (!text) return ''
    return `${text.operator === 'eq' ? 'مساوی با' : 'شامل'} ${text.value}`
  }
  if (definition.type === 'date-comparison') {
    const date = asDateComparison(value)
    if (!date) return ''
    const labels = { before: 'قبل از', after: 'بعد از', eq: '=', between: 'بازه' }
    return date.operator === 'between'
      ? `${date.value} تا ${date.valueTo}`
      : `${labels[date.operator]} ${date.value}`
  }
  if (definition.type === 'select' && typeof value === 'string') {
    return optionsFor(definition).find(option => option.value === value)?.label ?? value
  }
  if (Array.isArray(value)) return value.join('، ')
  if (isDateRange(value)) return [value.from, value.to].filter(Boolean).join(' تا ')
  if (typeof value === 'boolean') return value ? 'بله' : 'خیر'
  return String(value ?? '')
}
</script>

<template>
  <section class="data-table-filters" aria-label="فیلترهای جدول">
    <div class="data-table-filters__toolbar">
      <div class="data-table-filters__search">
        <i class="las la-search" aria-hidden="true"></i>
        <input
          class="form-control form-control-sm"
          type="search"
          :value="searchValue"
          :placeholder="searchPlaceholder"
          aria-label="جستجوی سریع"
          @input="updateSearch"
        />
        <button v-if="searchValue" type="button" aria-label="پاک کردن جستجو" @click="clearSearch">
          <i class="las la-times" aria-hidden="true"></i>
        </button>
      </div>

      <button
        class="btn btn-outline-primary btn-sm data-table-filters__toggle"
        type="button"
        :aria-expanded="panelOpen"
        @click="togglePanel"
      >
        <i class="las la-filter" aria-hidden="true"></i>
        <span>فیلترها</span>
        <span v-if="activeFilterCount" class="badge badge-primary">{{ activeFilterCount }}</span>
      </button>
    </div>

    <Transition name="filter-panel">
      <div v-if="panelOpen" class="data-table-filters__panel">
        <div class="data-table-filters__grid">
          <div v-for="definition in definitions" :key="definition.key" class="data-table-filters__field">
            <slot
              :name="`filter-${definition.key}`"
              :definition="definition"
              :value="draftFilters[definition.key]"
              :error="errors[definition.key]"
              :set-value="(value: DataTableFilterValue) => setDraftValue(definition.key, value)"
            >
            <TextFilter
              v-if="definition.type === 'text-comparison'"
              :definition="definition"
              :model-value="asTextComparison(draftFilters[definition.key])"
              :error="errors[definition.key]"
              @update:model-value="setDraftValue(definition.key, $event)"
            />

            <DateFilter
              v-else-if="definition.type === 'date-comparison'"
              :definition="definition"
              :model-value="asDateComparison(draftFilters[definition.key])"
              :error="errors[definition.key]"
              @update:model-value="setDraftValue(definition.key, $event)"
            />

            <NumericFilter
              v-else-if="definition.type === 'numeric-comparison'"
              :definition="definition"
              :model-value="asNumeric(draftFilters[definition.key])"
              :error="errors[definition.key]"
              @update:model-value="setDraftValue(definition.key, $event)"
            />

            <template v-else>
              <label :for="`filter-${definition.key}`">{{ definition.label }}</label>
              <input
                v-if="definition.type === 'text'"
                :id="`filter-${definition.key}`"
                class="form-control form-control-sm"
                type="text"
                :value="draftFilters[definition.key] ?? ''"
                :placeholder="definition.placeholder"
                :maxlength="definition.maxlength"
                @input="updateSimpleValue(definition.key, $event)"
              />
              <select
                v-else-if="definition.type === 'select'"
                :id="`filter-${definition.key}`"
                class="custom-select custom-select-sm"
                :value="draftFilters[definition.key] ?? ''"
                :disabled="loadingOptions[definition.key]"
                @change="updateSimpleValue(definition.key, $event)"
              >
                <option value="">{{ definition.placeholder ?? 'همه' }}</option>
                <option v-for="option in optionsFor(definition)" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </select>
              <select
                v-else-if="definition.type === 'multiselect'"
                :id="`filter-${definition.key}`"
                class="custom-select custom-select-sm"
                multiple
                :value="asStringArray(draftFilters[definition.key])"
                :disabled="loadingOptions[definition.key]"
                @change="updateMultiSelect(definition.key, $event)"
              >
                <option v-for="option in optionsFor(definition)" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </select>
              <input
                v-else-if="definition.type === 'date'"
                :id="`filter-${definition.key}`"
                class="form-control form-control-sm"
                type="date"
                :value="draftFilters[definition.key] ?? ''"
                @input="updateSimpleValue(definition.key, $event)"
              />
              <div v-else-if="definition.type === 'date-range'" class="data-table-filters__range">
                <input
                  :id="`filter-${definition.key}`"
                  class="form-control form-control-sm"
                  type="date"
                  :value="asDateRange(draftFilters[definition.key]).from"
                  aria-label="تاریخ شروع"
                  @input="updateDateRange(definition.key, 'from', $event)"
                />
                <input
                  class="form-control form-control-sm"
                  type="date"
                  :value="asDateRange(draftFilters[definition.key]).to"
                  aria-label="تاریخ پایان"
                  @input="updateDateRange(definition.key, 'to', $event)"
                />
              </div>
              <select
                v-else-if="definition.type === 'boolean'"
                :id="`filter-${definition.key}`"
                class="custom-select custom-select-sm"
                :value="draftFilters[definition.key] == null ? '' : String(draftFilters[definition.key])"
                @change="updateBooleanValue(definition.key, $event)"
              >
                <option value="">همه</option>
                <option value="true">{{ definition.trueLabel ?? 'بله' }}</option>
                <option value="false">{{ definition.falseLabel ?? 'خیر' }}</option>
              </select>
              <small v-if="errors[definition.key]" class="data-table-filters__error">
                {{ errors[definition.key] }}
              </small>
            </template>
            </slot>
          </div>
        </div>

        <div class="data-table-filters__actions">
          <button class="btn btn-outline-dark btn-sm" type="button" :disabled="loading" @click="clearAll">
            پاک کردن فیلترها
          </button>
          <button class="btn btn-success btn-sm" type="button" :disabled="loading" @click="applyFilters">
            <span v-if="loading" class="spinner-border spinner-border-sm ml-2"></span>
            <i v-else class="las la-filter ml-1" aria-hidden="true"></i>
            اعمال فیلتر
          </button>
        </div>
      </div>
    </Transition>

    <div v-if="appliedChips.length" class="data-table-filters__chips" aria-label="فیلترهای فعال">
      <button
        v-for="chip in appliedChips"
        :key="chip.key"
        class="data-table-filters__chip"
        type="button"
        :aria-label="`حذف فیلتر ${chip.label}`"
        @click="removeFilter(chip.key)"
      >
        <span>{{ chip.label }}: {{ chip.value }}</span>
        <i class="las la-times" aria-hidden="true"></i>
      </button>
    </div>
  </section>
</template>

<style scoped>
.data-table-filters { margin-bottom: 1rem; }
.data-table-filters__toolbar { align-items: center; display: flex; gap: 0.75rem; }
.data-table-filters__search { flex: 0 1 24rem; max-width: 24rem; position: relative; width: 100%; }
.data-table-filters__search > i {
  color: var(--text-secondary); font-size: 1.1rem; pointer-events: none;
  position: absolute; right: 0.85rem; top: 50%; transform: translateY(-50%);
}
.data-table-filters__search .form-control {
  font-size: 0.8125rem; height: 2.25rem; min-height: 2.25rem;
  padding-left: 2.25rem; padding-right: 2.25rem;
}
.data-table-filters__search button {
  background: transparent; border: 0; color: var(--text-secondary); left: 0.65rem;
  padding: 0.25rem; position: absolute; top: 50%; transform: translateY(-50%);
}
.data-table-filters__toggle {
  align-items: center; display: inline-flex; font-size: 0.8125rem; gap: 0.4rem;
  height: 2.25rem; min-height: 2.25rem; white-space: nowrap;
}
.data-table-filters__toggle .badge {
  align-items: center; border-radius: 50%; display: inline-flex; height: 1.3rem;
  justify-content: center; min-width: 1.3rem;
}
  .data-table-filters__panel {
    background: var(--background);
    border: 1px solid var(--border-color);
    border-radius: 0.85rem;
    margin-top: 0.85rem;
    overflow: hidden;
    padding: 0.75rem;
  }
.data-table-filters__grid { display: grid; gap: 0.75rem; grid-template-columns: repeat(4, minmax(0, 1fr)); }
.data-table-filters__field > label {
  color: var(--text-primary); display: block; font-size: 0.8125rem;
  font-weight: 600; margin-bottom: 0.3rem;
}
.data-table-filters__field .form-control,
.data-table-filters__field .custom-select {
  background-color: var(--surface) !important; border-color: var(--border-color);
  color: var(--text-primary); font-size: 0.8125rem;
  height: 2.25rem; min-height: 2.25rem;
}
.data-table-filters__range { display: grid; gap: 0.5rem; grid-template-columns: repeat(2, minmax(0, 1fr)); }
.data-table-filters__error { color: var(--error); display: block; margin-top: 0.35rem; }
.data-table-filters__actions { display: flex; gap: 0.5rem; justify-content: flex-end; margin-top: 0.75rem; }
.data-table-filters__chips { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 0.75rem; }
.data-table-filters__chip {
  align-items: center; background: var(--primary-light); border: 0; border-radius: 999px;
  color: var(--primary-dark); display: inline-flex; font-size: 0.75rem; gap: 0.35rem;
  padding: 0.35rem 0.65rem;
}
.filter-panel-enter-active, .filter-panel-leave-active { transition: opacity 0.18s ease, transform 0.18s ease; }
.filter-panel-enter-from, .filter-panel-leave-to { opacity: 0; transform: translateY(-0.4rem); }

@media (max-width: 1199px) {
  .data-table-filters__grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}

@media (max-width: 767px) {
  .data-table-filters__toolbar { align-items: stretch; }
  .data-table-filters__search { min-width: 0; }
  .data-table-filters__toggle span:not(.badge) { display: none; }
  .data-table-filters__toggle {
    flex: 0 0 2.25rem; justify-content: center; padding: 0; position: relative; width: 2.25rem;
  }
  .data-table-filters__toggle .badge {
    font-size: 0.625rem; height: 1rem; min-width: 1rem; position: absolute; right: -0.3rem; top: -0.3rem;
  }
  .data-table-filters__grid, .data-table-filters__range { grid-template-columns: 1fr; }
  .data-table-filters__actions .btn { flex: 1 1 50%; min-height: 3.25rem; }
}
</style>
