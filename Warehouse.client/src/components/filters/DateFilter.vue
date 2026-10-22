<script setup lang="ts">
import { computed } from 'vue'
import DatePicker from 'vue3-persian-datetime-picker'

import type {
  DateComparisonFilterDefinition,
  DateFilterOperator,
  DateFilterValue,
} from '@/types/dataTableFilters'

const props = defineProps<{
  definition: DateComparisonFilterDefinition
  modelValue: DateFilterValue | null
  error?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: DateFilterValue | null]
}>()

const operator = computed(() => props.modelValue?.operator ?? 'eq')
const value = computed({
  get: () => props.modelValue?.value ?? '',
  set: (nextValue: string) => updateDate('value', nextValue),
})
const valueTo = computed({
  get: () => props.modelValue?.valueTo ?? '',
  set: (nextValue: string) => updateDate('valueTo', nextValue),
})

function updateOperator(event: Event): void {
  const nextOperator = (event.target as HTMLSelectElement).value as DateFilterOperator
  const next: DateFilterValue = {
    operator: nextOperator,
    value: props.modelValue?.value ?? '',
  }
  if (nextOperator === 'between') next.valueTo = props.modelValue?.valueTo ?? ''
  emit('update:modelValue', next)
}

function updateDate(field: 'value' | 'valueTo', nextValue: string): void {
  const next: DateFilterValue = {
    operator: operator.value,
    value: props.modelValue?.value ?? '',
  }
  if (operator.value === 'between') next.valueTo = props.modelValue?.valueTo ?? ''
  next[field] = nextValue
  emit('update:modelValue', next)
}
</script>

<template>
  <fieldset class="date-filter" >
    <div class="date-filter__heading">
      <legend>{{ definition.label }}</legend>
      <button
        v-if="modelValue?.value"
        class="date-filter__clear"
        type="button"
        :aria-label="`پاک کردن ${definition.label}`"
        @click="emit('update:modelValue', null)"
      >
        <i class="las la-times" aria-hidden="true"></i>
      </button>
    </div>

    <div class="date-filter__controls" dir="rtl" :class="{ 'date-filter__controls--range': operator === 'between' }">
      <select
        class="custom-select custom-select-sm date-filter__operator"
        :value="operator"
        :aria-label="`عملگر ${definition.label}`"
        @change="updateOperator"
      >
        <option value="before">قبل از</option>
        <option value="after">بعد از</option>
        <option value="eq">=</option>
        <option value="between">بازه</option>
      </select>

      <DatePicker
        v-model="value"
        type="date"
        format="YYYY-MM-DD"
        display-format="jYYYY/jMM/jDD"
        locale="fa"
      
        :editable="true"
        :clearable="true"
        :auto-submit="true"
        color="#2563eb"
      />

      <DatePicker
        v-if="operator === 'between'"
        v-model="valueTo"
        type="date"
        format="YYYY-MM-DD"
        display-format="jYYYY/jMM/jDD"
        locale="fa"
     
        :editable="true"
        :clearable="true"
        :auto-submit="true"
        color="#2563eb"
      />
    </div>
    <small v-if="error" class="date-filter__error">{{ error }}</small>
  </fieldset>
</template>

<style scoped>
.date-filter { border: 0; margin: 0; min-width: 0; padding: 0; }
.date-filter__heading { align-items: center; display: flex; justify-content: space-between; margin-bottom: 0.3rem; }
.date-filter legend { color: var(--text-primary); font-size: 0.8125rem; font-weight: 600; margin: 0; width: auto; }
.date-filter__controls
{
    direction: rtl;
    display: grid;
    gap: 0;
    grid-template-columns: auto minmax(0, 1fr);
}
.date-filter__controls--range { grid-template-columns: auto repeat(2, minmax(0, 1fr)); }
.date-filter__operator {
  border-radius: 0 0.25rem  0.25rem 0;
  border-left: 0;
  color: var(--text-secondary) !important;
  font-size: 0.6875rem !important;
  padding-bottom: 0.2rem;
  padding-left: 0.05rem;
  padding-top: 0.2rem; 
}
.date-filter__controls .custom-select {
    padding:8px;
  background-color: var(--surface) !important; border-color: var(--border-color);
  color: var(--text-primary); font-size: 0.8125rem;
  height: 2.25rem; min-height: 2.25rem;
}
.date-filter__controls :deep(input) {
  background-color: var(--surface) !important; border: 1px solid var(--border-color);
  border-radius:  0.25rem 0 0 0.25rem ; color: var(--text-primary); direction: rtl; font-size: 0.8125rem;
  height: 2.25rem; min-height: 2.25rem; padding: 0.375rem 0.75rem; width: 100%;
}
.date-filter__clear { background: transparent; border: 0; color: var(--text-secondary); line-height: 1; padding: 0.15rem; }
.date-filter__clear:hover, .date-filter__clear:focus-visible { color: var(--error); }
.date-filter__error { color: var(--error); display: block; margin-top: 0.35rem; }
@media (max-width: 767px) {
  .date-filter__controls, .date-filter__controls--range { grid-template-columns: 1fr; }
  .date-filter__operator {
    border-radius: 0.25rem 0.25rem 0 0; border-right: 1px solid var(--border-color);
    border-bottom: 0; width: 100%;
  }
  .date-filter__controls :deep(input) { border-radius: 0; }
  .date-filter__controls > :last-child :deep(input) { border-radius: 0 0 0.25rem 0.25rem; }
}
</style>
