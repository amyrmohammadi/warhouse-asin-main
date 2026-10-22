<script setup lang="ts">
import { computed } from 'vue'

import type {
  TextComparisonFilterDefinition,
  TextFilterOperator,
  TextFilterValue,
} from '@/types/dataTableFilters'

const props = defineProps<{
  definition: TextComparisonFilterDefinition
  modelValue: TextFilterValue | null
  error?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: TextFilterValue | null]
}>()

const operator = computed(() => props.modelValue?.operator ?? 'contains')

function updateOperator(event: Event): void {
  emit('update:modelValue', {
    operator: (event.target as HTMLSelectElement).value as TextFilterOperator,
    value: props.modelValue?.value ?? '',
  })
}

function updateValue(event: Event): void {
  emit('update:modelValue', {
    operator: operator.value,
    value: (event.target as HTMLInputElement).value,
  })
}
</script>

<template>
  <fieldset class="text-filter">
    <div class="text-filter__heading">
      <legend>{{ definition.label }}</legend>
      <button
        v-if="modelValue?.value"
        class="text-filter__clear"
        type="button"
        :aria-label="`پاک کردن ${definition.label}`"
        @click="emit('update:modelValue', null)"
      >
        <i class="las la-times" aria-hidden="true"></i>
      </button>
    </div>

    <div class="text-filter__controls">
      <select
        class="custom-select custom-select-sm text-filter__operator"
        :value="operator"
        :aria-label="`عملگر ${definition.label}`"
        @change="updateOperator"
      >
        <option value="contains">شامل</option>
        <option value="eq">مساوی با</option>
      </select>

      <input
        class="form-control form-control-sm"
        type="text"
        :value="modelValue?.value ?? ''"
        
        :maxlength="definition.maxlength"
        :aria-label="`مقدار ${definition.label}`"
        @input="updateValue"
      />
    </div>
    <small v-if="error" class="text-filter__error">{{ error }}</small>
  </fieldset>
</template>

<style scoped>
.text-filter { border: 0; margin: 0; min-width: 0; padding: 0; }
.text-filter__heading { align-items: center; display: flex; justify-content: space-between; margin-bottom: 0.3rem; }
.text-filter legend { color: var(--text-primary); font-size: 0.8125rem; font-weight: 600; margin: 0; width: auto; }
.text-filter__controls { direction: rtl; display: grid; gap: 0; grid-template-columns: 6.25rem minmax(0, 1fr); }
.text-filter__operator {
  border-radius:0 0.25rem  0.25rem 0; border-left: 0;
  color: var(--text-secondary) !important; font-size: 0.6875rem !important;
  padding-bottom: 0.2rem; padding-left: 0.35rem; padding-top: 0.2rem; 
}
.text-filter__controls .form-control,
.text-filter__controls .custom-select {
  background-color: var(--surface) !important; border-color: var(--border-color);
  color: var(--text-primary); font-size: 0.8125rem;
  height: 2.25rem; min-height: 2.25rem;
}
.text-filter__controls .form-control {
  border-radius:  0.25rem 0 0 0.25rem ; direction: rtl;
}
.text-filter__clear { background: transparent; border: 0; color: var(--text-secondary); line-height: 1; padding: 0.15rem; }
.text-filter__clear:hover, .text-filter__clear:focus-visible { color: var(--error); }
.text-filter__error { color: var(--error); display: block; margin-top: 0.35rem; }
@media (max-width: 575px) {
  .text-filter__controls { grid-template-columns: 1fr; }
  .text-filter__operator {
    border-radius: 0.25rem 0.25rem 0 0; border-right: 1px solid var(--border-color);
    border-bottom: 0; width: 100%;
  }
  .text-filter__controls .form-control { border-radius: 0 0 0.25rem 0.25rem; }
}
</style>
