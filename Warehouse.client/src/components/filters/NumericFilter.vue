<script setup lang="ts">
import { computed } from 'vue'

import type {
  NumericComparisonFilterDefinition,
  NumericFilterValue,
  NumericOperator,
} from '@/types/dataTableFilters'

const props = defineProps<{
  definition: NumericComparisonFilterDefinition
  modelValue: NumericFilterValue | null
  error?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: NumericFilterValue | null]
}>()

const operator = computed(() => props.modelValue?.operator ?? 'eq')
const step = computed(() =>
  props.definition.numericType === 'integer'
    ? 1
    : props.definition.step ?? 10 ** -(props.definition.precision ?? 2),
)

function updateOperator(event: Event): void {
  const nextOperator = (event.target as HTMLSelectElement).value as NumericOperator
  const next: NumericFilterValue = {
    operator: nextOperator,
    value: props.modelValue?.value ?? null,
  }

  if (nextOperator === 'between') next.valueTo = props.modelValue?.valueTo ?? null
  emit('update:modelValue', next)
}

function updateNumber(field: 'value' | 'valueTo', event: Event): void {
  const rawValue = (event.target as HTMLInputElement).value
  const numericValue = rawValue === '' ? null : Number(rawValue)
  const next: NumericFilterValue = {
    operator: operator.value,
    value: props.modelValue?.value ?? null,
  }

  if (operator.value === 'between') next.valueTo = props.modelValue?.valueTo ?? null
  next[field] = numericValue
  emit('update:modelValue', next)
}
</script>

<template>
  <fieldset class="numeric-filter">
    <div class="numeric-filter__heading">
      <legend>{{ definition.label }}</legend>
      <button
        v-if="modelValue?.value !== null && modelValue?.value !== undefined"
        class="numeric-filter__clear"
        type="button"
        :aria-label="`پاک کردن ${definition.label}`"
        @click="emit('update:modelValue', null)"
      >
        <i class="las la-times" aria-hidden="true"></i>
      </button>
    </div>

    <div class="numeric-filter__controls">
      <select
        class="custom-select custom-select-sm numeric-filter__operator"
        :value="operator"
        :aria-label="`عملگر ${definition.label}`"
        @change="updateOperator"
      >
        <option value="eq">=</option>
        <option value="gt">بزرگ‌تر</option>
        <option value="gte">بزرگ‌تر یا مساوی</option>
        <option value="lt">کوچک‌تر</option>
        <option value="lte">کوچک‌تر یا مساوی</option>
        <option value="between">بازه</option>
      </select>

      <input
        class="form-control form-control-sm"
        type="number"
        inputmode="decimal"
        :value="modelValue?.value ?? ''"
        :min="definition.min"
        :max="definition.max"
        :step="step"
        :aria-label="`مقدار ${definition.label}`"
        @input="updateNumber('value', $event)"
      />

      <input
        v-if="operator === 'between'"
        class="form-control form-control-sm"
        type="number"
        inputmode="decimal"
        :value="modelValue?.valueTo ?? ''"
        :min="definition.min"
        :max="definition.max"
        :step="step"
        :aria-label="`انتهای بازه ${definition.label}`"
        @input="updateNumber('valueTo', $event)"
      />
    </div>
    <small v-if="error" class="numeric-filter__error">{{ error }}</small>
  </fieldset>
</template>

<style scoped>
.numeric-filter { border: 0; margin: 0; min-width: 0; padding: 0; }
.numeric-filter__heading { align-items: center; display: flex; justify-content: space-between; margin-bottom: 0.3rem; }
.numeric-filter legend { color: var(--text-primary); font-size: 0.8125rem; font-weight: 600; margin: 0; width: auto; }
.numeric-filter__controls
{
  direction: rtl;
  display: grid;
  gap: 0;
  grid-template-columns: auto minmax(0, 1fr);
}
.numeric-filter__controls:has(input + input) { grid-template-columns: auto repeat(2, minmax(0, 1fr)); }
.numeric-filter__operator {
  border-radius:0 0.25rem  0.25rem 0;
  border-left: 0;
  color: var(--text-secondary) !important;
  font-size: 0.6875rem !important;
  padding-bottom: 0.2rem;
  padding-left: 0.35rem;
  padding-top: 0.2rem;
 
}
  .numeric-filter__controls .form-control,
  .numeric-filter__controls .custom-select {
    padding: 8px;
    background-color: var(--surface) !important;
    border-color: var(--border-color);
    color: var(--text-primary);
    font-size: 0.8125rem;
    height: 2.25rem;
    min-height: 2.25rem;
  }
.numeric-filter__controls .form-control { border-radius: 0; direction: ltr; }
.numeric-filter__controls .form-control:last-child { border-radius:  0.25rem 0 0 0.25rem ; }
.numeric-filter__clear { background: transparent; border: 0; color: var(--text-secondary); line-height: 1; padding: 0.15rem; }
.numeric-filter__clear:hover, .numeric-filter__clear:focus-visible { color: var(--error); }
.numeric-filter__error { color: var(--error); display: block; margin-top: 0.35rem; }
@media (max-width: 575px) {
  .numeric-filter__controls, .numeric-filter__controls:has(input + input) { grid-template-columns: 1fr; }
  .numeric-filter__operator {
    border-radius: 0.25rem 0.25rem 0 0; border-right: 1px solid var(--border-color);
    border-bottom: 0; width: 100%;
  }
  .numeric-filter__controls .form-control,
  .numeric-filter__controls .form-control:last-child { border-radius: 0; }
  .numeric-filter__controls .form-control:last-child { border-radius: 0 0 0.25rem 0.25rem; }
}
</style>
