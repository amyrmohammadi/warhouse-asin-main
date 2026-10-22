<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'

import {
  getSupplierById,
  searchSuppliers,
  SUPPLIER_SEARCH_MAX_RESULTS,
  SUPPLIER_SEARCH_MIN_LENGTH,
  type SupplierOption,
} from '@/services/suppliers'

defineOptions({ name: 'SupplierSelect' })

const props = withDefaults(
  defineProps<{
    /** شناسه تأمین‌کننده انتخاب‌شده */
    modelValue: string | null
    /** متن نمایشی داخل کادر (برای فرم‌های ادیت مقدار اولیه دارد) */
    text?: string
    label?: string
    placeholder?: string
    ariaLabel?: string
    error?: string
    /** کلاس CSS اضافی برای پیام خطا */
    errorClass?: string
    disabled?: boolean
    required?: boolean
    size?: 'sm' | 'md'
    /** اجازه ثبت تأمین‌کننده جدید در صورت نبود نتیجه */
    allowCreate?: boolean
    minSearchLength?: number
    maxResults?: number
    debounceMs?: number
  }>(),
  {
    text: '',
    label: '',
    placeholder: 'تایپ یا انتخاب تأمین‌کننده',
    error: '',
    errorClass: '',
    disabled: false,
    required: false,
    size: 'md',
    allowCreate: false,
    minSearchLength: SUPPLIER_SEARCH_MIN_LENGTH,
    maxResults: SUPPLIER_SEARCH_MAX_RESULTS,
    debounceMs: 250,
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
  'update:text': [value: string]
  'select': [supplier: SupplierOption]
  'create': [text: string]
  'clear': []
  /** وقتی کلید Enter زده شود ولی گزینه‌ای انتخاب نشده باشد */
  'enter': []
}>()

const componentId = useId()
const inputId = `supplier-select-${componentId}`
const listboxId = `${inputId}-listbox`

const rootElement = ref<HTMLElement | null>(null)
const localText = ref(props.text ?? '')
const options = ref<SupplierOption[]>([])
const selectedOption = ref<SupplierOption | null>(null)
const menuOpen = ref(false)
const loading = ref(false)
const loadFailed = ref(false)
const activeIndex = ref(-1)

let searchTimer: ReturnType<typeof setTimeout> | null = null
let searchController: AbortController | null = null
let lookupController: AbortController | null = null

const trimmedText = computed(() => localText.value.trim())
const searchReady = computed(() => trimmedText.value.length >= props.minSearchLength)
const canCreate = computed(() =>
  props.allowCreate &&
  trimmedText.value.length >= props.minSearchLength &&
  !options.value.some(option => option.title.trim() === trimmedText.value),
)

/** عنوانی که در حال حاضر به شناسه انتخاب‌شده مربوط است. */
const currentTitle = computed(() => {
  if (!props.modelValue) return ''
  if (selectedOption.value?.id === props.modelValue) return selectedOption.value.title
  return (props.text ?? '').trim()
})

watch(
  () => props.text,
  (value) => {
    const next = value ?? ''
    if (next !== localText.value) localText.value = next
  },
)

watch(
  () => props.modelValue,
  (id) => {
    if (!id) {
      selectedOption.value = null
      return
    }

    if (selectedOption.value?.id === id) return

    // فرم ادیت عنوان اولیه را همراه شناسه می‌فرستد؛ نیازی به درخواست سرور نیست.
    if ((props.text ?? '').trim()) return

    void resolveTitle(id)
  },
  { immediate: true },
)

function cancelPendingSearch(): void {
  if (searchTimer) {
    clearTimeout(searchTimer)
    searchTimer = null
  }

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
    const results = await searchSuppliers({
      search: term,
      limit: props.maxResults,
      signal: controller.signal,
    })
    if (controller.signal.aborted) return

    options.value = results
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

async function resolveTitle(id: string): Promise<void> {
  lookupController?.abort()
  const controller = new AbortController()
  lookupController = controller

  try {
    const supplier = await getSupplierById(id, controller.signal)
    if (controller.signal.aborted) return

    selectedOption.value = supplier
    localText.value = supplier.title
    emit('update:text', supplier.title)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    // عنوان قابل بازیابی نبود؛ شناسه دست‌نخورده باقی می‌ماند.
  } finally {
    if (lookupController === controller) lookupController = null
  }
}

function handleInput(event: Event): void {
  const value = (event.target as HTMLInputElement).value
  localText.value = value
  emit('update:text', value)

  if (props.modelValue && value.trim() !== currentTitle.value) {
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

function chooseOption(option: SupplierOption): void {
  selectedOption.value = option
  localText.value = option.title
  options.value = []
  activeIndex.value = -1
  menuOpen.value = false

  emit('update:text', option.title)
  emit('update:modelValue', option.id)
  emit('select', option)
}

function createSupplier(): void {
  const value = trimmedText.value
  if (!value) return

  selectedOption.value = null
  localText.value = value
  options.value = []
  activeIndex.value = -1
  menuOpen.value = false

  emit('update:modelValue', null)
  emit('update:text', value)
  emit('create', value)
}

function moveActive(step: number): void {
  if (!menuOpen.value) {
    openMenu()
    return
  }

  const count = options.value.length + (canCreate.value ? 1 : 0)
  if (!count) return

  const next = activeIndex.value + step
  activeIndex.value = next < 0 ? count - 1 : next >= count ? 0 : next
}

function handleEnter(): void {
  if (activeIndex.value >= 0 && activeIndex.value < options.value.length) {
    chooseOption(options.value[activeIndex.value])
    return
  }

  if (canCreate.value) {
    createSupplier()
    return
  }

  emit('enter')
}

function handleBlur(): void {
  window.setTimeout(closeMenu, 120)
}

function handleDocumentMouseDown(event: MouseEvent): void {
  if (!rootElement.value?.contains(event.target as Node)) closeMenu()
}

onMounted(() => {
  document.addEventListener('mousedown', handleDocumentMouseDown)
})

onBeforeUnmount(() => {
  document.removeEventListener('mousedown', handleDocumentMouseDown)
  cancelPendingSearch()
  lookupController?.abort()
})
</script>

<template>
  <div ref="rootElement" class="supplier-select" :class="{ 'supplier-select--open': menuOpen }">
    <label v-if="label" class="supplier-select__label" :for="inputId">
      {{ label }}
      <span v-if="required" class="text-danger">*</span>
    </label>

    <div class="supplier-select__control">
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

      <i class="las la-angle-down supplier-select__arrow" aria-hidden="true"></i>

      <div v-if="menuOpen" :id="listboxId" class="supplier-select__menu" role="listbox">
        <div v-if="loading" class="supplier-select__status">در حال جستجو...</div>
        <div v-else-if="loadFailed" class="supplier-select__status">
          دریافت فهرست تأمین‌کنندگان انجام نشد.
        </div>
        <div v-else-if="!searchReady" class="supplier-select__status">
          برای جستجو حداقل {{ minSearchLength }} کاراکتر وارد کنید.
        </div>
        <div v-else-if="!options.length && !canCreate" class="supplier-select__status">
          تأمین‌کننده‌ای پیدا نشد.
        </div>

        <button v-for="(option, index) in options"
                :key="option.id"
                class="supplier-select__option"
                :class="{ 'supplier-select__option--active': index === activeIndex }"
                type="button"
                role="option"
                :aria-selected="option.id === modelValue"
                @mousedown.prevent="chooseOption(option)"
                @mouseenter="activeIndex = index">
          <span class="supplier-select__option-title">{{ option.title }}</span>
          <span v-if="option.description" class="supplier-select__option-description">
            {{ option.description }}
          </span>
        </button>

        <button v-if="canCreate"
                class="supplier-select__option supplier-select__option--create"
                type="button"
                role="option"
                @mousedown.prevent="createSupplier">
          <i class="las la-plus-circle" aria-hidden="true"></i>
          <span>افزودن «{{ trimmedText }}» به‌عنوان تأمین‌کننده جدید</span>
        </button>
      </div>
    </div>

    <small v-if="error" class="invalid-feedback d-block" :class="errorClass">{{ error }}</small>
  </div>
</template>

<style scoped>
  /*
   * همهٔ رنگ‌ها و اندازه‌ها از متغیرهای CSS خوانده می‌شوند تا با تم روشن و تاریک
   * هماهنگ بمانند. برای تغییر ظاهر از بیرون کامپوننت کافی است همین متغیرها را
   * روی هر والدی (یا ریشهٔ خود کامپوننت) بازنویسی کنید، چون مقدار پیش‌فرض فقط
   * زمانی استفاده می‌شود که متغیر تعریف نشده باشد:
   *   --supplier-select-input-background
   *   --supplier-select-input-border-color
   *   --supplier-select-input-border-width
   *   --supplier-select-input-radius
   *   --supplier-select-input-color
   *   --supplier-select-menu-background
   *   --supplier-select-menu-border-color
   *   --supplier-select-menu-radius
   *   --supplier-select-menu-min-width
   *   --supplier-select-menu-shadow
   *   --supplier-select-menu-color
   *   --supplier-select-option-color
   *   --supplier-select-option-hover-background
   *   --supplier-select-option-hover-color
   */
  .supplier-select {
    position: relative;
    width: 100%;
  }

  .supplier-select__label {
    color: var(--text-primary);
    display: block;
    font-size: .8125rem;
    font-weight: 600;
    margin-bottom: .3rem;
  }

  .supplier-select__control {
    position: relative;
  }

  .supplier-select__control .form-control {
    background-color: var(--supplier-select-input-background, var(--surface));
    border-color: var(--supplier-select-input-border-color, var(--border-color));
    border-radius: var(--supplier-select-input-radius, .5rem);
    border-style: solid;
    border-width: var(--supplier-select-input-border-width, 1px);
    color: var(--supplier-select-input-color, var(--text-primary));
    padding-left: 2rem;
  }

  .supplier-select__arrow {
    color: var(--text-secondary);
    left: .7rem;
    pointer-events: none;
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
  }

  .supplier-select__menu {
    background: var(--supplier-select-menu-background, var(--surface));
    border: 1px solid var(--supplier-select-menu-border-color, var(--border-color));
    border-radius: var(--supplier-select-menu-radius, .65rem);
    box-shadow: var(--supplier-select-menu-shadow, 0 .75rem 1.75rem rgba(15, 23, 42, .18));
    color: var(--supplier-select-menu-color, var(--text-primary));
    max-height: 15rem;
    min-width: var(--supplier-select-menu-min-width, 120px);
    overflow-y: auto;
    padding: .3rem;
    position: absolute;
    right: 0;
    top: calc(100% + .35rem);
    width: 100%;
    z-index: 100070;
  }

  .supplier-select__option {
    align-items: stretch;
    background: transparent;
    border: 0;
    border-radius: .45rem;
    color: var(--supplier-select-option-color, var(--text-primary));
    cursor: pointer;
    display: flex;
    flex-direction: column;
    font-size: 11px;
    gap: .25rem;
    padding: .35rem .45rem;
    text-align: right;
    width: 100%;
  }

    .supplier-select__option + .supplier-select__option {
      margin-top: .15rem;
    }

    .supplier-select__option:hover,
    .supplier-select__option:focus,
    .supplier-select__option--active {
      background: var(--supplier-select-option-hover-background, var(--primary-light));
      color: var(--supplier-select-option-hover-color, var(--primary-dark));
      outline: 0;
    }

  .supplier-select__option-title {
    display: block;
    font-weight: 600;
    min-width: 0;
    overflow: hidden;
    text-align: right;
    text-overflow: ellipsis;
    white-space: nowrap;
    width: 100%;
  }

  .supplier-select__option-description {
    color: var(--text-secondary);
    display: block;
    font-size: .72rem;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    width: 100%;
  }

  .supplier-select__option--create {
    align-items: center;
    flex-direction: row;
    gap: .35rem;
    font-size:.7rem;
    color:var(--primary);
    justify-content: flex-start;
  }

  .supplier-select__status {
    align-items: center;
    color: var(--text-secondary);
    display: flex;
    font-size: .7rem;
    gap: .4rem;
    padding: .7rem .55rem;
  }

  /* تم تاریک: پاپ‌آپ و حالت هاور باید با پس‌زمینهٔ تیره هماهنگ شوند. */
  :global(body[data-app-theme='dark'] .supplier-select__control .form-control) {
    background-color: var(--supplier-select-input-background, #1e293b);
    border-color: var(--supplier-select-input-border-color, #334155);
    color: var(--supplier-select-input-color, #f1f5f9);
  }

  :global(body[data-app-theme='dark'] .supplier-select__menu) {
    background: var(--supplier-select-menu-background, #1e293b);
    border-color: var(--supplier-select-menu-border-color, #334155);
    box-shadow: var(--supplier-select-menu-shadow, 0 .75rem 1.75rem rgba(0, 0, 0, .55));
    color: var(--supplier-select-menu-color, #f1f5f9);
  }

  :global(body[data-app-theme='dark'] .supplier-select__option) {
    color: var(--supplier-select-option-color, #f1f5f9);
  }

  :global(body[data-app-theme='dark'] .supplier-select__option:hover),
  :global(body[data-app-theme='dark'] .supplier-select__option:focus),
  :global(body[data-app-theme='dark'] .supplier-select__option--active) {
    background: var(--supplier-select-option-hover-background, #334155);
    color: var(--supplier-select-option-hover-color, #f1f5f9);
  }
</style>
