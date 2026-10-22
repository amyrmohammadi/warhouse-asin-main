<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'

export interface PersonSelectOption {
  id: string
  fullName: string
}

defineOptions({ name: 'PersonSelect' })

const props = withDefaults(
  defineProps<{
    modelValue: string | null
    text?: string
    options: PersonSelectOption[]
    label?: string
    placeholder?: string
    ariaLabel?: string
    notFoundText?: string
    error?: string
    errorClass?: string
    disabled?: boolean
    required?: boolean
    size?: 'sm' | 'md'
    allowCreate?: boolean
    maxResults?: number
  }>(),
  {
    text: '',
    label: '',
    placeholder: 'تایپ یا انتخاب تحویل‌دهنده',
    notFoundText: 'تحویل‌دهنده‌ای پیدا نشد.',
    error: '',
    errorClass: '',
    disabled: false,
    required: false,
    size: 'md',
    allowCreate: false,
    maxResults: 10,
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
  'update:text': [value: string]
  select: [person: PersonSelectOption]
  clear: []
  enter: []
}>()

const componentId = useId()
const inputId = `person-select-${componentId}`
const listboxId = `${inputId}-listbox`
const localText = ref(props.text ?? '')
const menuOpen = ref(false)
const activeIndex = ref(-1)

const normalizedText = computed(() => normalize(localText.value))
const filteredOptions = computed(() => {
  const query = normalizedText.value
  const matches = query
    ? props.options.filter(person => normalize(person.fullName).includes(query))
    : props.options
  return matches.slice(0, props.maxResults)
})
const canCreate = computed(() =>
  props.allowCreate
  && Boolean(localText.value.trim())
  && !props.options.some(person => normalize(person.fullName) === normalizedText.value),
)

watch(
  () => props.text,
  value => {
    const next = value ?? ''
    if (next !== localText.value) localText.value = next
  },
)

watch(
  () => props.modelValue,
  id => {
    if (!id || (props.text ?? '').trim()) return
    const selected = props.options.find(person => person.id === id)
    if (!selected) return
    localText.value = selected.fullName
    emit('update:text', selected.fullName)
  },
  { immediate: true },
)

function normalize(value: string): string {
  return value.trim().toLocaleLowerCase('fa-IR')
}

function openMenu(): void {
  if (!props.disabled) menuOpen.value = true
}

function closeMenu(): void {
  menuOpen.value = false
  activeIndex.value = -1
}

function chooseOption(person: PersonSelectOption): void {
  localText.value = person.fullName
  menuOpen.value = false
  activeIndex.value = -1
  emit('update:text', person.fullName)
  emit('update:modelValue', person.id)
  emit('select', person)
}

function resolveExactMatch(): void {
  const match = props.options.find(person => normalize(person.fullName) === normalizedText.value)
  if (match) {
    chooseOption(match)
    return
  }
  if (props.modelValue) {
    emit('update:modelValue', null)
    emit('clear')
  }
}

function handleInput(event: Event): void {
  const value = (event.target as HTMLInputElement).value
  localText.value = value
  activeIndex.value = -1
  menuOpen.value = true
  emit('update:text', value)

  const match = props.options.find(person => normalize(person.fullName) === normalize(value))
  if (match) {
    emit('update:modelValue', match.id)
  } else if (props.modelValue) {
    emit('update:modelValue', null)
    emit('clear')
  }
}

function moveActive(step: number): void {
  if (!menuOpen.value) {
    openMenu()
    return
  }
  const count = filteredOptions.value.length
  if (!count) return
  const next = activeIndex.value + step
  activeIndex.value = next < 0 ? count - 1 : next >= count ? 0 : next
}

function handleEnter(): void {
  if (activeIndex.value >= 0 && activeIndex.value < filteredOptions.value.length) {
    chooseOption(filteredOptions.value[activeIndex.value])
    return
  }
  resolveExactMatch()
  emit('enter')
}

function handleBlur(): void {
  resolveExactMatch()
  window.setTimeout(closeMenu, 120)
}
</script>

<template>
  <div class="person-select" :class="{ 'person-select--open': menuOpen }">
    <label v-if="label" class="person-select__label" :for="inputId">
      {{ label }}
      <span v-if="required" class="text-danger">*</span>
    </label>

    <div class="person-select__control">
      <input
        :id="inputId"
        class="form-control"
        :class="[size === 'sm' ? 'form-control-sm' : '', { 'is-invalid': Boolean(error) }]"
        type="text"
        role="combobox"
        aria-autocomplete="list"
        :aria-expanded="menuOpen"
        :aria-controls="listboxId"
        autocomplete="off"
        :placeholder="placeholder"
        :aria-label="ariaLabel || undefined"
        :disabled="disabled"
        :value="localText"
        @input="handleInput"
        @focus="openMenu"
        @click="openMenu"
        @blur="handleBlur"
        @keydown.down.prevent="moveActive(1)"
        @keydown.up.prevent="moveActive(-1)"
        @keydown.enter.prevent="handleEnter"
        @keydown.esc.prevent="closeMenu" />

      <i class="las la-angle-down person-select__arrow" aria-hidden="true"></i>

      <div v-if="menuOpen" :id="listboxId" class="person-select__menu" role="listbox">
        <button v-for="(person, index) in filteredOptions"
                :key="person.id"
                class="person-select__option"
                :class="{ 'person-select__option--active': index === activeIndex }"
                type="button"
                role="option"
                :aria-selected="person.id === modelValue"
                @mousedown.prevent="chooseOption(person)"
                @mouseenter="activeIndex = index">
          {{ person.fullName }}
        </button>
        <div v-if="!filteredOptions.length" class="person-select__status">{{ notFoundText }}</div>
        <div v-if="canCreate" class="person-select__create">
          <i class="las la-plus-circle" aria-hidden="true"></i>
          «{{ localText.trim() }}» به‌عنوان تحویل‌دهنده جدید ثبت می‌شود.
        </div>
      </div>
    </div>

    <small v-if="error" class="invalid-feedback d-block" :class="errorClass">{{ error }}</small>
  </div>
</template>

<style scoped>
.person-select { position: relative; width: 100%; }
.person-select__label {
  color: var(--text-primary); display: block; font-size: .8125rem;
  font-weight: 600; margin-bottom: .3rem;
}
.person-select__control { position: relative; }
.person-select__control .form-control {
  background-color: var(--person-select-input-background, var(--surface));
  border-color: var(--person-select-input-border-color, var(--border-color));
  border-radius: var(--person-select-input-radius, .5rem);
  color: var(--person-select-input-color, var(--text-primary));
  padding-left: 2rem;
}
.person-select__arrow {
  color: var(--text-secondary); left: .7rem; pointer-events: none;
  position: absolute; top: 50%; transform: translateY(-50%);
}
.person-select__menu {
  background: var(--surface); border: 1px solid var(--border-color); border-radius: .65rem;
  box-shadow: 0 .75rem 2rem rgba(15, 23, 42, .14); color: var(--text-primary);
  left: 0; max-height: 16rem; min-width: 100%; overflow-y: auto; padding: .35rem;
  position: absolute; right: 0; top: calc(100% + .35rem); z-index: 100072;
}
.person-select__option {
  background: transparent; border: 0; border-radius: .45rem; color: var(--text-primary);
  display: block; font-size: .8125rem; padding: .55rem .65rem; text-align: right; width: 100%;
}
.person-select__option:hover, .person-select__option--active {
  background: var(--primary-light); color: var(--primary-dark);
}
.person-select__status, .person-select__create {
  color: var(--text-secondary); font-size: .75rem; padding: .65rem;
}
.person-select__create { color: var(--success); }
</style>
