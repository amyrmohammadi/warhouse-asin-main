<script setup lang="ts">
import { computed, ref } from 'vue'

import { useFloatingMenu } from './useFloatingMenu'
import type { UnitOfMeasure } from '@/services/unitsOfMeasure'

defineOptions({ inheritAttrs: false }) // data-entry-* و بقیهٔ attrها روی خود input می‌نشینند

const props = withDefaults(defineProps<{
  modelValue: string | null
  text: string
  units: UnitOfMeasure[]
  readonly?: boolean
  disabled?: boolean
  error?: string
  size?: 'sm' | 'md'
  /** برای سلول جدول: بدون کادر و پس‌زمینه */
  borderless?: boolean
  placeholder?: string
  ariaLabel?: string
}>(), { size: 'md', placeholder: '' })

const emit = defineEmits<{
  'update:modelValue': [id: string | null]
  'update:text': [text: string]
  select: [unit: UnitOfMeasure]
  enter: []
}>()

const open = ref(false)
const active = ref(-1)
const root = ref<HTMLElement | null>(null)
const menuEl = ref<HTMLElement | null>(null)
const { style: menuStyle } = useFloatingMenu(root, open, { menu: menuEl, minWidth: 192 })

const normalize = (value: string): string =>
  value.replace(/\u200C/gu, ' ').trim().replace(/\s+/gu, ' ').toLocaleUpperCase('fa-IR')

const options = computed(() => {
  const query = normalize(props.text)
  const list = query ? props.units.filter(unit => normalize(unit.title).includes(query)) : props.units
  return list.slice(0, 8)
})

function resolve(value: string): void {
  if (props.readonly) return
  const match = props.units.find(unit => normalize(unit.title) === normalize(value))
  emit('update:modelValue', match?.id ?? null)
  if (match && match.title !== value) emit('update:text', match.title)
}

function onInput(event: Event): void {
  const value = (event.target as HTMLInputElement).value
  emit('update:text', value)
  open.value = true
  active.value = -1
  resolve(value)
}

function choose(unit: UnitOfMeasure): void {
  emit('update:modelValue', unit.id)
  emit('update:text', unit.title)
  emit('select', unit)
  open.value = false
}

function onBlur(): void {
  resolve(props.text)
  open.value = false
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    if (props.readonly) return
    event.preventDefault()
    open.value = true
    const count = options.value.length
    if (!count) return
    active.value = (active.value + (event.key === 'ArrowDown' ? 1 : -1) + count) % count
  } else if (event.key === 'Enter') {
    event.preventDefault()
    const picked = open.value ? options.value[active.value] : undefined
    if (picked) choose(picked)
    else { resolve(props.text); open.value = false }
    emit('enter')
  } else if (event.key === 'Escape' && open.value) {
    event.stopPropagation()
    open.value = false
  }
}
</script>

<template>
  <div ref="root" class="unit-select" :class="{ 'unit-select--borderless': borderless }">
    <input v-bind="$attrs"
           :value="text"
           class="form-control"
           :class="[size === 'sm' && 'form-control-sm', { 'is-invalid': error }]"
           type="text"
           role="combobox"
           aria-autocomplete="list"
           :aria-expanded="open"
           :aria-label="ariaLabel"
           autocomplete="off"
           maxlength="200"
           :placeholder="placeholder"
           :readonly="readonly"
           :disabled="disabled"
           @focus="open = !readonly"
           @input="onInput"
           @blur="onBlur"
           @keydown="onKeydown" />
    <i class="las la-angle-down unit-select__icon" aria-hidden="true"></i>
    <Teleport to="body">
      <div v-if="open && !readonly" ref="menuEl" class="unit-select__menu" :style="menuStyle" role="listbox">
        <button v-for="(unit, index) in options" :key="unit.id" class="unit-select__option"
                :class="{ 'unit-select__option--active': index === active }" type="button" role="option"
                :aria-selected="index === active" @mousedown.prevent="choose(unit)">
          {{ unit.title }}
        </button>
        <div v-if="!options.length" class="unit-select__empty">فقط واحدهای موجود قابل انتخاب‌اند.</div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.unit-select { height: 100%; position: relative; }
.unit-select .form-control { padding-left: 1.9rem; }
.unit-select__icon {
  color: var(--text-secondary); 
  left: .7rem; 
  pointer-events: none;
   position: absolute; 
   top: 50%;
    transform: translateY(-50%);
}

.unit-select--borderless { min-height: 1.85rem; }
.unit-select--borderless .form-control {
  background-color: transparent !important; 
  border-color: transparent;
   border-radius: 0;
    box-sizing: border-box;
  font-size: .68rem; 
  height: 100%; 
  min-height: 1.85rem; 
  opacity: 1;
   padding: .25rem .35rem .25rem 1.65rem; 
   width: 100%;
}
.unit-select--borderless .form-control:focus 
{ 
  border-color: var(--primary); 
  box-shadow: 0 0 0 .12rem rgba(37, 99, 235, .12);
 }
.unit-select--borderless .form-control.is-invalid
 {
   border-color: #dc3545;
    box-shadow: inset 0 0 0 1px rgba(220, 53, 69, .18);
   }

.unit-select__menu {
  background: var(--surface);
   border: 1px solid var(--border-color);
   border-radius: .65rem;
  box-shadow: 0 .75rem 1.75rem rgba(15, 23, 42, .18);
   overflow-y: auto; 
   padding: .3rem;
    z-index: 100071;
}
.unit-select__option {
  background: transparent; border: 0; 
  border-radius: .45rem; 
  color: var(--text-primary);
   cursor: pointer; 
   display: block;
  font-size: 11px; 
  font-weight: 600;
   padding: .35rem .4rem; 
   text-align: right; width: 100%;
}
.unit-select__option:hover, .unit-select__option--active { background: var(--primary-light); color: var(--primary-dark); outline: 0; }
.unit-select__empty { color: var(--text-secondary); font-size: .8rem; padding: .7rem .55rem; }
</style>
