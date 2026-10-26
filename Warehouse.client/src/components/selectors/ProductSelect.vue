<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'

import { useFloatingMenu } from './useFloatingMenu'
import {
  getProductById,
  PRODUCT_SEARCH_MAX_RESULTS,
  PRODUCT_SEARCH_MIN_LENGTH,
  searchProducts,
  type Product,
} from '@/services/products'

defineOptions({ name: 'ProductSelect' })

const props = withDefaults(
  defineProps<{
    /** شناسه کالای انتخاب‌شده */
    modelValue: string | null
    /** متن نمایشی داخل کادر (عنوان یا کد، بسته به searchBy) */
    text?: string
    /** جستجو بر اساس عنوان یا کد کالا */
    searchBy?: 'title' | 'code'
    label?: string
    placeholder?: string
    ariaLabel?: string
    /** پیام نمایش‌داده‌شده وقتی نتیجه‌ای یافت نشد */
    notFoundText?: string
    error?: string
    /** کلاس CSS اضافی برای پیام خطا */
    errorClass?: string
    disabled?: boolean
    required?: boolean
    size?: 'sm' | 'md'
    minSearchLength?: number
    maxResults?: number
    debounceMs?: number
  }>(),
  {
    text: '',
    searchBy: 'title',
    label: '',
    placeholder: 'تایپ یا انتخاب کالا',
    ariaLabel: '',
    notFoundText: 'کالایی پیدا نشد.',
    error: '',
    errorClass: '',
    disabled: false,
    required: false,
    size: 'md',
    minSearchLength: PRODUCT_SEARCH_MIN_LENGTH,
    maxResults: PRODUCT_SEARCH_MAX_RESULTS,
    debounceMs: 250,
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
  'update:text': [value: string]
  'select': [product: Product]
  'clear': []
  /** وقتی کلید Enter زده شود ولی گزینه‌ای انتخاب نشده باشد */
  'enter': []
}>()

const componentId = useId()
const inputId = `product-select-${componentId}`
const listboxId = `${inputId}-listbox`

const rootElement = ref<HTMLElement | null>(null)
const controlElement = ref<HTMLElement | null>(null)
const menuElement = ref<HTMLElement | null>(null)
const localText = ref(props.text ?? '')
const options = ref<Product[]>([])
const selectedProduct = ref<Product | null>(null)
const menuOpen = ref(false)
const loading = ref(false)
const loadFailed = ref(false)
const activeIndex = ref(-1)

let searchTimer: ReturnType<typeof setTimeout> | null = null
let searchController: AbortController | null = null
let lookupController: AbortController | null = null

const trimmedText = computed(() => localText.value.trim())
const searchReady = computed(() => trimmedText.value.length >= props.minSearchLength)

/**
 * منو به <body> منتقل می‌شود (Teleport) تا overflow مودال و جدول آن را نبُرد؛
 * پس دیگر فرزند `.product-select` نیست و متغیرهای CSS ارثی را از دست می‌دهد.
 * متغیرهای ظاهری `--product-select-menu-*` و `--product-select-option-*` را به منو منتقل می‌کنیم.
 */
const FORWARDED_MENU_VARS = [
  '--product-select-menu-background',
  '--product-select-menu-border-color',
  '--product-select-menu-radius',
  '--product-select-menu-shadow',
  '--product-select-menu-color',
  '--product-select-option-color',
  '--product-select-option-hover-background',
  '--product-select-option-hover-color',
]

// عرض منو همیشه برابر عرض ورودی است
const { style: floatingStyle } = useFloatingMenu(controlElement, menuOpen, { menu: menuElement, minWidth: 220 })

const forwardedVars = ref<Record<string, string>>({})
watch(menuOpen, open => {
  if (!open || !controlElement.value) return
  const computed = window.getComputedStyle(controlElement.value)
  const next: Record<string, string> = {}
  for (const name of FORWARDED_MENU_VARS) {
    const value = computed.getPropertyValue(name).trim()
    if (value) next[name] = value
  }
  forwardedVars.value = next
}, { flush: 'pre' })

/** مقدار متنی که در حال حاضر به کالای انتخاب‌شده مربوط است. */
const currentTitle = computed(() => {
  if (!props.modelValue) return ''
  if (selectedProduct.value?.id === props.modelValue) {
    return props.searchBy === 'code'
      ? selectedProduct.value.code
      : selectedProduct.value.title
  }
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
      selectedProduct.value = null
      return
    }

    if (selectedProduct.value?.id === id) return

    // فرم ادیت متن اولیه را همراه شناسه می‌فرستد؛ نیازی به درخواست سرور نیست.
    if ((props.text ?? '').trim()) return

    void resolveProduct(id)
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

/** Esc فقط منو را می‌بندد؛ مودال والد با همین Esc بسته نشود. */
function handleEscape(event: KeyboardEvent): void {
  if (!menuOpen.value) return
  event.preventDefault()
  event.stopPropagation()
  closeMenu()
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
    const results = await searchProducts({
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

async function resolveProduct(id: string): Promise<void> {
  lookupController?.abort()
  const controller = new AbortController()
  lookupController = controller

  try {
    const product = await getProductById(id, controller.signal)
    if (controller.signal.aborted) return

    selectedProduct.value = product
    localText.value = props.searchBy === 'code' ? product.code : product.title
    emit('update:text', localText.value)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    // اطلاعات کالا قابل بازیابی نبود؛ شناسه دست‌نخورده باقی می‌ماند.
  } finally {
    if (lookupController === controller) lookupController = null
  }
}

function handleInput(event: Event): void {
  const value = (event.target as HTMLInputElement).value
  localText.value = value
  emit('update:text', value)

  if (props.modelValue && value.trim() !== currentTitle.value) {
    selectedProduct.value = null
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

function chooseOption(product: Product): void {
  selectedProduct.value = product
  localText.value = props.searchBy === 'code' ? product.code : product.title
  options.value = []
  activeIndex.value = -1
  menuOpen.value = false

  emit('update:text', localText.value)
  emit('update:modelValue', product.id)
  emit('select', product)
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

function handleEnter(): void {
  if (activeIndex.value >= 0 && activeIndex.value < options.value.length) {
    chooseOption(options.value[activeIndex.value])
    return
  }

  emit('enter')
}

function handleBlur(): void {
  window.setTimeout(closeMenu, 120)
}

/** کلیک بیرون از ورودی و بیرون از منو (که حالا خارج از root است) منو را می‌بندد. */
function handleDocumentMouseDown(event: MouseEvent): void {
  const target = event.target as Node
  if (rootElement.value?.contains(target) || menuElement.value?.contains(target)) return
  closeMenu()
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
  <div ref="rootElement" class="product-select" :class="{ 'product-select--open': menuOpen }">
    <label v-if="label" class="product-select__label" :for="inputId">
      {{ label }}
      <span v-if="required" class="text-danger">*</span>
    </label>

    <div ref="controlElement" class="product-select__control">
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
        @keydown.esc="handleEscape" />

      <i class="las la-angle-down product-select__arrow" aria-hidden="true"></i>

      <Teleport to="body">
        <div v-if="menuOpen"
             :id="listboxId"
             ref="menuElement"
             class="product-select__menu"
             :style="[floatingStyle, forwardedVars]"
             role="listbox">
          <div v-if="loading" class="product-select__status">در حال جستجو...</div>
          <div v-else-if="loadFailed" class="product-select__status">
            دریافت فهرست کالاها انجام نشد.
          </div>
          <div v-else-if="!searchReady" class="product-select__status">
            برای جستجو حداقل {{ minSearchLength }} کاراکتر وارد کنید.
          </div>
          <div v-else-if="!options.length" class="product-select__status">
            {{ notFoundText }}
          </div>

          <button v-for="(product, index) in options"
                  :key="product.id"
                  class="product-select__option"
                  :class="{ 'product-select__option--active': index === activeIndex }"
                  type="button"
                  role="option"
                  :aria-selected="product.id === modelValue"
                  :title="product.title"
                  @mousedown.prevent="chooseOption(product)"
                  @mouseenter="activeIndex = index">
            <span class="product-select__option-title">{{ product.title }}</span>
            <span class="product-select__option-meta">
              <span class="product-select__option-code" dir="ltr">{{ product.code }}</span>
              <span class="product-select__option-unit">{{ product.unitOfMeasureTitle }}</span>
            </span>
          </button>
        </div>
      </Teleport>
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
   *   --product-select-input-background
   *   --product-select-input-border-color
   *   --product-select-input-border-width
   *   --product-select-input-radius
   *   --product-select-input-color
   *   --product-select-menu-background
   *   --product-select-menu-border-color
   *   --product-select-menu-radius
   *   --product-select-menu-shadow
   *   --product-select-menu-color
   *   --product-select-option-color
   *   --product-select-option-hover-background
   *   --product-select-option-hover-color
   *
   * منو به <body> منتقل می‌شود؛ مکان آن را useFloatingMenu تعیین می‌کند و عرضش همیشه
   * برابر عرض ورودی است (متغیرهای --product-select-menu-width/min-width دیگر استفاده نمی‌شوند).
   * متغیرهای بالا هنگام باز شدن از root خوانده و روی منو کپی می‌شوند.
   */
  .product-select {
    position: relative;
    width: 100%;
  }

  .product-select__label {
    color: var(--text-primary);
    display: block;
    font-size: .8125rem;
    font-weight: 600;
    margin-bottom: .3rem;
  }

  .product-select__control {
    position: relative;
  }

  .product-select__control .form-control {
    background-color: var(--product-select-input-background, var(--surface));
    border-color: var(--product-select-input-border-color, var(--border-color));
    border-radius: var(--product-select-input-radius, .5rem);
    border-style: solid;
    border-width: var(--product-select-input-border-width, 1px);
    color: var(--product-select-input-color, var(--text-primary));
    padding-left: .2rem;
  }

  .product-select__arrow {
    color: var(--text-secondary);
    left: .7rem;
    pointer-events: none;
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
  }

  /* position / top / left / width / max-height را useFloatingMenu به‌صورت inline می‌گذارد */
  .product-select__menu {
    background: var(--product-select-menu-background, var(--surface));
    border: 1px solid var(--product-select-menu-border-color, var(--border-color));
    border-radius: var(--product-select-menu-radius, .65rem);
    box-shadow: var(--product-select-menu-shadow, 0 .75rem 1.75rem rgba(15, 23, 42, .18));
    color: var(--product-select-menu-color, var(--text-primary));
    overflow-y: auto;
    padding: .3rem;
    z-index: 100071;
  }

  .product-select__option {
    align-items: stretch;
    background: transparent;
    border: 0;
    border-radius: .45rem;
    color: var(--product-select-option-color, var(--text-primary));
    cursor: pointer;
    display: flex;
    flex-direction: column;
    font-size: 11px;
    gap: .4rem;
    padding: .25rem .25rem;
    text-align: right;
    width: 100%;
  }

    .product-select__option + .product-select__option {
      margin-top: .15rem;
    }

    .product-select__option:hover,
    .product-select__option:focus,
    .product-select__option--active {
      background: var(--product-select-option-hover-background, var(--primary-light));
      color: var(--product-select-option-hover-color, var(--primary-dark));
      outline: 0;
    }

  .product-select__option-title {
    display: block;
    font-weight: 600;
    min-width: 0;
    overflow: hidden;
    text-align: right;
    text-overflow: ellipsis;
    white-space: nowrap;
    width: 100%;
  }

  .product-select__option-meta {
    align-items: center;
    color: var(--text-secondary);
    direction: ltr;
    display: flex;
    font-size: .54rem;
    gap: .35rem;
    justify-content: flex-start;
    text-align: left;
    width: 100%;
  }

  .product-select__option-unit {
    direction: rtl;
  }

  .product-select__option-meta span {
    background: var(--body-bg);
    border: 1px solid var(--border-color);
    border-radius: 999px;
    padding: .12rem .4rem;
  }

  .product-select__option:hover .product-select__option-meta {
    color: inherit;
  }

  .product-select__status {
    align-items: center;
    color: var(--text-secondary);
    display: flex;
    font-size: .8rem;
    gap: .4rem;
    padding: .7rem .55rem;
  }

  /* تم تاریک: پاپ‌آپ و حالت هاور باید با پس‌زمینهٔ تیره هماهنگ شوند. */
  :global(body[data-app-theme='dark'] .product-select__control .form-control) {
    background-color: var(--product-select-input-background, #1e293b);
    border-color: var(--product-select-input-border-color, #334155);
    color: var(--product-select-input-color, #f1f5f9);
  }

  :global(body[data-app-theme='dark'] .product-select__menu) {
    background: var(--product-select-menu-background, #1e293b);
    border-color: var(--product-select-menu-border-color, #334155);
    box-shadow: var(--product-select-menu-shadow, 0 .75rem 1.75rem rgba(0, 0, 0, .55));
    color: var(--product-select-menu-color, #f1f5f9);
  }

  :global(body[data-app-theme='dark'] .product-select__option) {
    color: var(--product-select-option-color, #f1f5f9);
  }

  :global(body[data-app-theme='dark'] .product-select__option:hover),
  :global(body[data-app-theme='dark'] .product-select__option:focus),
  :global(body[data-app-theme='dark'] .product-select__option--active) {
    background: var(--product-select-option-hover-background, #334155);
    color: var(--product-select-option-hover-color, #f1f5f9);
  }
</style>
