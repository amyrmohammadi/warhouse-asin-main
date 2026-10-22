<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'

import {
  getGoodsIssueDestinationById,
  GOODS_ISSUE_DESTINATION_SEARCH_MAX_RESULTS,
  GOODS_ISSUE_DESTINATION_SEARCH_MIN_LENGTH,
  searchGoodsIssueDestinations,
  type GoodsIssueDestinationOption,
} from '@/services/goodsIssueDestinations'

defineOptions({ name: 'GoodsIssueDestinationSelect' })

const props = withDefaults(defineProps<{
  modelValue: string | null
  text?: string
  label?: string
  placeholder?: string
  ariaLabel?: string
  error?: string
  errorClass?: string
  disabled?: boolean
  required?: boolean
  size?: 'sm' | 'md'
  allowCreate?: boolean
  minSearchLength?: number
  maxResults?: number
  debounceMs?: number
}>(), {
  text: '',
  label: '',
  placeholder: 'تایپ یا انتخاب مقصد',
  error: '',
  errorClass: '',
  disabled: false,
  required: false,
  size: 'md',
  allowCreate: false,
  minSearchLength: GOODS_ISSUE_DESTINATION_SEARCH_MIN_LENGTH,
  maxResults: GOODS_ISSUE_DESTINATION_SEARCH_MAX_RESULTS,
  debounceMs: 250,
})

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
  'update:text': [value: string]
  select: [destination: GoodsIssueDestinationOption]
  clear: []
  enter: []
}>()

const componentId = useId()
const inputId = `goods-issue-destination-select-${componentId}`
const listboxId = `${inputId}-listbox`
const rootElement = ref<HTMLElement | null>(null)
const localText = ref(props.text ?? '')
const options = ref<GoodsIssueDestinationOption[]>([])
const selectedOption = ref<GoodsIssueDestinationOption | null>(null)
const menuOpen = ref(false)
const loading = ref(false)
const loadFailed = ref(false)
const activeIndex = ref(-1)
let searchTimer: ReturnType<typeof setTimeout> | null = null
let searchController: AbortController | null = null
let lookupController: AbortController | null = null

const trimmedText = computed(() => localText.value.trim())
const searchReady = computed(() => trimmedText.value.length >= props.minSearchLength)
const canCreate = computed(() => props.allowCreate && searchReady.value && !loading.value
  && !options.value.some(option => normalize(option.title) === normalize(trimmedText.value)))
const currentTitle = computed(() => {
  if (!props.modelValue) return ''
  if (selectedOption.value?.id === props.modelValue) return selectedOption.value.title
  return (props.text ?? '').trim()
})

watch(() => props.text, value => {
  const next = value ?? ''
  if (next !== localText.value) localText.value = next
})

watch(() => props.modelValue, id => {
  if (!id) {
    selectedOption.value = null
    return
  }
  if (selectedOption.value?.id === id || (props.text ?? '').trim()) return
  void resolveDestination(id)
}, { immediate: true })

function normalize(value: string): string {
  return value.trim().replace(/\s+/gu, ' ').toLocaleUpperCase('fa-IR')
}

function cancelPendingSearch(): void {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = null
  searchController?.abort()
  searchController = null
}

function openMenu(): void {
  if (props.disabled) return
  menuOpen.value = true
  if (searchReady.value) scheduleSearch(trimmedText.value)
}

function closeMenu(): void {
  menuOpen.value = false
  activeIndex.value = -1
}

function scheduleSearch(term: string): void {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    searchTimer = null
    void runSearch(term)
  }, props.debounceMs)
}

async function runSearch(term: string): Promise<void> {
  searchController?.abort()
  const controller = new AbortController()
  searchController = controller
  loading.value = true
  loadFailed.value = false
  try {
    const results = await searchGoodsIssueDestinations({
      search: term,
      limit: props.maxResults,
      signal: controller.signal,
    })
    if (!controller.signal.aborted) options.value = results
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    options.value = []
    loadFailed.value = true
  } finally {
    if (searchController === controller) {
      searchController = null
      loading.value = false
    }
  }
}

async function resolveDestination(id: string): Promise<void> {
  lookupController?.abort()
  const controller = new AbortController()
  lookupController = controller
  try {
    const destination = await getGoodsIssueDestinationById(id, controller.signal)
    if (controller.signal.aborted) return
    selectedOption.value = destination
    localText.value = destination.title
    emit('update:text', destination.title)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
  } finally {
    if (lookupController === controller) lookupController = null
  }
}

function handleInput(event: Event): void {
  const value = (event.target as HTMLInputElement).value
  localText.value = value
  emit('update:text', value)
  if (props.modelValue && normalize(value) !== normalize(currentTitle.value)) {
    selectedOption.value = null
    emit('update:modelValue', null)
    emit('clear')
  }
  menuOpen.value = true
  activeIndex.value = -1
  if (!searchReady.value) {
    cancelPendingSearch()
    options.value = []
    loadFailed.value = false
    loading.value = false
    return
  }
  scheduleSearch(trimmedText.value)
}

function chooseOption(option: GoodsIssueDestinationOption): void {
  selectedOption.value = option
  localText.value = option.title
  options.value = []
  activeIndex.value = -1
  menuOpen.value = false
  emit('update:text', option.title)
  emit('update:modelValue', option.id)
  emit('select', option)
}

function moveActive(step: number): void {
  if (!menuOpen.value) {
    openMenu()
    return
  }
  const count = options.value.length
  if (!count) return
  const next = activeIndex.value + step
  activeIndex.value = next < 0 ? count - 1 : next >= count ? 0 : next
}

function resolveExactMatch(): void {
  const exact = options.value.find(option => normalize(option.title) === normalize(trimmedText.value))
  if (exact) chooseOption(exact)
}

function handleEnter(): void {
  if (activeIndex.value >= 0 && activeIndex.value < options.value.length) {
    chooseOption(options.value[activeIndex.value])
    return
  }
  resolveExactMatch()
  closeMenu()
  emit('enter')
}

function handleBlur(): void {
  resolveExactMatch()
  window.setTimeout(closeMenu, 120)
}

function handleDocumentMouseDown(event: MouseEvent): void {
  if (!rootElement.value?.contains(event.target as Node)) closeMenu()
}

onMounted(() => document.addEventListener('mousedown', handleDocumentMouseDown))
onBeforeUnmount(() => {
  document.removeEventListener('mousedown', handleDocumentMouseDown)
  cancelPendingSearch()
  lookupController?.abort()
})
</script>

<template>
  <div ref="rootElement" class="destination-select" :class="{ 'destination-select--open': menuOpen }">
    <label v-if="label" class="destination-select__label" :for="inputId">
      {{ label }} <span v-if="required" class="text-danger">*</span>
    </label>
    <div class="destination-select__control">
      <input :id="inputId"
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
      <i class="las la-angle-down destination-select__arrow" aria-hidden="true"></i>
      <div v-if="menuOpen" :id="listboxId" class="destination-select__menu" role="listbox">
        <div v-if="loading" class="destination-select__status">در حال جستجو...</div>
        <div v-else-if="loadFailed" class="destination-select__status">دریافت فهرست مقصدها انجام نشد.</div>
        <div v-else-if="!searchReady" class="destination-select__status">
          برای جستجو حداقل {{ minSearchLength }} کاراکتر وارد کنید.
        </div>
        <div v-else-if="!options.length && !canCreate" class="destination-select__status">مقصدی پیدا نشد.</div>
        <button v-for="(option, index) in options"
                :key="option.id"
                class="destination-select__option"
                :class="{ 'destination-select__option--active': index === activeIndex }"
                type="button"
                role="option"
                :aria-selected="option.id === modelValue"
                @mousedown.prevent="chooseOption(option)"
                @mouseenter="activeIndex = index">
          <span class="destination-select__option-title">{{ option.title }}</span>
          <span v-if="option.description" class="destination-select__option-description">{{ option.description }}</span>
        </button>
        <div v-if="canCreate" class="destination-select__create">
          <i class="las la-plus-circle" aria-hidden="true"></i>
          «{{ trimmedText }}» به‌عنوان مقصد جدید ثبت می‌شود.
        </div>
      </div>
    </div>
    <small v-if="error" class="invalid-feedback d-block" :class="errorClass">{{ error }}</small>
  </div>
</template>

<style scoped>
.destination-select { position: relative; width: 100%; }
.destination-select__label { color: var(--text-primary); display: block; font-size: .8125rem; font-weight: 600; margin-bottom: .3rem; }
.destination-select__control { position: relative; }
.destination-select__control .form-control {
  background-color: var(--destination-select-input-background, var(--surface));
  border-color: var(--destination-select-input-border-color, var(--border-color));
  border-radius: var(--destination-select-input-radius, .5rem);
  border-style: solid;
  border-width: var(--destination-select-input-border-width, 1px);
  color: var(--destination-select-input-color, var(--text-primary));
  padding-left: 2rem;
}
.destination-select__arrow { color: var(--text-secondary); left: .7rem; pointer-events: none; position: absolute; top: 50%; transform: translateY(-50%); }
.destination-select__menu {
  background: var(--destination-select-menu-background, var(--surface));
  border: 1px solid var(--destination-select-menu-border-color, var(--border-color));
  border-radius: var(--destination-select-menu-radius, .65rem);
  box-shadow: var(--destination-select-menu-shadow, 0 .75rem 1.75rem rgba(15, 23, 42, .18));
  color: var(--destination-select-menu-color, var(--text-primary));
  max-height: 15rem;
  min-width: var(--destination-select-menu-min-width, 180px);
  overflow-y: auto;
  padding: .3rem;
  position: absolute;
  right: 0;
  top: calc(100% + .35rem);
  width: var(--destination-select-menu-width, 100%);
  z-index: 100070;
}
.destination-select__option { background: transparent; border: 0; border-radius: .45rem; color: var(--text-primary); display: flex; flex-direction: column; padding: .5rem .6rem; text-align: right; width: 100%; }
.destination-select__option:hover, .destination-select__option:focus, .destination-select__option--active { background: var(--primary-light); color: var(--primary-dark); outline: 0; }
.destination-select__option-title { font-size: .8rem; font-weight: 600; }
.destination-select__option-description { color: var(--text-secondary); font-size: .7rem; margin-top: .15rem; }
.destination-select__status, .destination-select__create { color: var(--text-secondary); font-size: .75rem; padding: .65rem; }
.destination-select__create { color: var(--success); }
:global(body[data-app-theme='dark'] .destination-select__menu) { background: #1e293b; border-color: #334155; color: #f1f5f9; }
:global(body[data-app-theme='dark'] .destination-select__option) { color: #f1f5f9; }
:global(body[data-app-theme='dark'] .destination-select__option:hover),
:global(body[data-app-theme='dark'] .destination-select__option:focus),
:global(body[data-app-theme='dark'] .destination-select__option--active) { background: #334155; color: #f1f5f9; }
</style>
