<script setup lang="ts">
import { toTypedSchema } from '@vee-validate/zod'
import type { AjaxCallback, AjaxData, ColumnsConfig, Config } from 'datatables.net'
import DataTablesCore from 'datatables.net-bs4'
import DataTable from 'datatables.net-vue3'
import { useForm } from 'vee-validate'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { z } from 'zod'

import ConfirmDialog from '@/components/ConfirmDialog.vue'
import DataTableScrollContainer from '@/components/DataTableScrollContainer.vue'
import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import { getSession } from '@/services/auth'
import { type MasterDataImportSummary, MasterDataApiError } from '@/services/masterData'
import {
  importProducts,
  createProduct,
  deleteProduct,
  getProducts,
  getProductValidationRules,
  type Product,
  type ProductValidationRules,
  updateProduct,
} from '@/services/products'
import { toast } from '@/services/toast'
import {
  getUnitsOfMeasure,
  type UnitOfMeasure,
  UnitsOfMeasureApiError,
} from '@/services/unitsOfMeasure'
import type {
  DataTableFilterDefinition,
  DataTableFilterValues,
} from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'

defineOptions({ name: 'ProductsView' })

DataTable.use(DataTablesCore)

interface FormValues {
  title: string
  code: string
  unitOfMeasureTitle: string
  description: string
}

interface ConfirmDialogInstance {
  ask: (options: {
    title: string
    text?: string
    icon?: 'warning'
    confirmButtonText?: string
    cancelButtonText?: string
    confirmButtonColor?: string
  }) => Promise<boolean>
}

const router = useRouter()
const table = ref<{
  dt: { ajax: { reload: (callback: null, resetPaging: boolean) => unknown } }
} | null>(null)
const confirmDialog = ref<ConfirmDialogInstance | null>(null)
const importInput = ref<HTMLInputElement | null>(null)
const checkOnlyProductCode = ref(false)
const importModalOpen = ref(false)
const pendingImportFile = ref<File | null>(null)
const isImporting = ref(false)
const importSummary = ref<MasterDataImportSummary | null>(null)
let importRequest: AbortController | null = null
const quickSearch = ref('')
const appliedFilters = ref<DataTableFilterValues>({})
const filtersLoading = ref(false)
const modalOpen = ref(false)
const editingItem = ref<Product | null>(null)
const validationRules = ref<ProductValidationRules | null>(null)
const unitOptions = ref<UnitOfMeasure[]>([])
const unitOptionsLoading = ref(false)
const unitMenuOpen = ref(false)
const rowsById = new Map<string, Product>()
const canManage = getSession()?.permissions.includes('MasterDataManage') ?? false
const canCreate = getSession()?.permissions.includes('ProductsCreate') ?? false

let activeListRequest: AbortController | null = null
let activeOperationRequest: AbortController | null = null
let unitOptionsRequest: AbortController | null = null
let validationRequest: Promise<boolean> | null = null
let latestDraw = 0
let closeUnitMenuTimer: number | undefined

const modalTitle = computed(() =>
  editingItem.value ? 'ویرایش کالا' : 'افزودن کالا',
)
const titleMaxLength = computed(() => validationRules.value?.title?.maxLength ?? undefined)
const codeMaxLength = computed(() => validationRules.value?.code?.maxLength ?? undefined)
const unitTitleMaxLength = computed(
  () => validationRules.value?.unitOfMeasureTitle?.maxLength ?? undefined,
)
const descriptionMaxLength = computed(
  () => validationRules.value?.description?.maxLength ?? undefined,
)

const validationSchema = computed(() =>
  toTypedSchema(
    z.object({
      title: z
        .string()
        .trim()
        .min(1, 'وارد کردن عنوان الزامی است.')
        .max(titleMaxLength.value ?? Number.MAX_SAFE_INTEGER, {
          message: titleMaxLength.value
            ? `عنوان نباید بیشتر از ${titleMaxLength.value} نویسه باشد.`
            : 'عنوان بیش از حد طولانی است.',
        }),
      code: z
        .string()
        .trim()
        .min(1, 'وارد کردن کد کالا الزامی است.')
        .max(codeMaxLength.value ?? Number.MAX_SAFE_INTEGER, {
          message: codeMaxLength.value
            ? `کد کالا نباید بیشتر از ${codeMaxLength.value} نویسه باشد.`
            : 'کد کالا بیش از حد طولانی است.',
        }),
      unitOfMeasureTitle: z
        .string()
        .trim()
        .min(1, 'انتخاب یا وارد کردن واحد اندازه‌گیری الزامی است.')
        .max(unitTitleMaxLength.value ?? Number.MAX_SAFE_INTEGER, {
          message: unitTitleMaxLength.value
            ? `عنوان واحد نباید بیشتر از ${unitTitleMaxLength.value} نویسه باشد.`
            : 'عنوان واحد بیش از حد طولانی است.',
        }),
      description: z
        .string()
        .trim()
        .max(descriptionMaxLength.value ?? Number.MAX_SAFE_INTEGER, {
          message: descriptionMaxLength.value
            ? `توضیحات نباید بیشتر از ${descriptionMaxLength.value} نویسه باشد.`
            : 'توضیحات بیش از حد طولانی است.',
        }),
    }),
  ),
)

const {
  defineField,
  errors,
  handleSubmit,
  isSubmitting,
  resetForm,
  setErrors,
} = useForm<FormValues>({
  validationSchema,
  initialValues: {
    title: '',
    code: '',
    unitOfMeasureTitle: '',
    description: '',
  },
})
const [title, titleAttrs] = defineField('title')
const [code, codeAttrs] = defineField('code')
const [unitOfMeasureTitle, unitOfMeasureTitleAttrs] = defineField('unitOfMeasureTitle')
const [description, descriptionAttrs] = defineField('description')

const filteredUnitOptions = computed(() => {
  const search = unitOfMeasureTitle.value.trim().toLocaleLowerCase('fa')
  if (!search) return unitOptions.value.slice(0, 12)
  return unitOptions.value
    .filter((unit) => unit.title.toLocaleLowerCase('fa').includes(search))
    .slice(0, 12)
})

const hasExactUnit = computed(() => {
  const value = unitOfMeasureTitle.value.trim()
  return value.length > 0 && unitOptions.value.some(
    (unit) => unit.title.localeCompare(value, 'fa', { sensitivity: 'base' }) === 0,
  )
})

const filterDefinitions = computed<DataTableFilterDefinition[]>(() => [
  {
    key: 'title',
    label: 'عنوان',
    type: 'text',
    placeholder: 'عنوان کالا را وارد کنید',
    maxlength: titleMaxLength.value,
  },
  {
    key: 'code',
    label: 'کد کالا',
    type: 'text',
    placeholder: 'کد کالا را وارد کنید',
    maxlength: codeMaxLength.value,
  },
  {
    key: 'unitOfMeasureTitle',
    label: 'واحد اندازه‌گیری',
    type: 'text',
    placeholder: 'عنوان واحد را وارد کنید',
    maxlength: unitTitleMaxLength.value,
  },
  {
    key: 'description',
    label: 'توضیحات',
    type: 'text',
    placeholder: 'بخشی از توضیحات را وارد کنید',
    maxlength: descriptionMaxLength.value,
  },
])

const columns: ColumnsConfig[] = [
  createRowNumberColumn(),
  { data: 'title', name: 'title', title: 'عنوان', className: 'all' },
  { data: 'code', name: 'code', title: 'کد کالا', className: 'min-tablet-l' },
  {
    data: 'unitOfMeasureTitle',
    name: 'unitOfMeasureTitle',
    title: 'واحد اندازه‌گیری',
    className: 'min-tablet-l',
  },
  {
    data: 'description',
    name: 'description',
    title: 'توضیحات',
    className: 'min-tablet-l',
    orderable: false,
  },
]

if (canManage) {
  columns.push({
    data: 'id',
    name: 'actions',
    title: 'عملیات',
    className: 'all text-center',
    orderable: false,
    searchable: false,
  })
}

const options: Config = {
  ajax: loadProducts,
  columns,
  serverSide: true,
  processing: true,
  searching: false,
  pageLength: 10,
  lengthMenu: [5, 10, 25, 50],
  order: [[1, 'asc']],
  autoWidth: false,
  pagingType: 'full_numbers',
  infoCallback: (_settings, start, end, _max, total) =>
    total === 0 ? '۰ از ۰' : `${start}${start === end ? '' : `–${end}`} از ${total}`,
  responsive: {
    details: {
      type: 'inline',
      renderer: DataTablesCore.Responsive.renderer.listHiddenNodes(),
    },
  },
  layout: {
    topStart: null,
    topEnd: null,
    bottomStart: {
      className: 'users-table-summary',
      features: ['pageLength', 'info'],
    },
    bottomEnd: {
      className: 'users-table-pagination',
      features: ['paging'],
    },
  },
  language: {
    aria: {
      orderable: 'مرتب‌سازی ستون',
      orderableReverse: 'مرتب‌سازی معکوس ستون',
      orderableRemove: 'حذف مرتب‌سازی ستون',
      paginate: {
        first: 'صفحه اول',
        last: 'صفحه آخر',
        next: 'صفحه بعدی',
        number: 'صفحه',
        previous: 'صفحه قبلی',
      },
    },
    emptyTable: 'کالایی برای نمایش وجود ندارد.',
    info: '_START_–_END_ از _TOTAL_',
    infoEmpty: '۰ از ۰',
    infoFiltered: '',
    lengthMenu: '_MENU_',
    loadingRecords: 'در حال دریافت کالاها...',
    processing: 'در حال پردازش...',
    zeroRecords: 'کالایی با این مشخصات پیدا نشد.',
    paginate: {
      first: '<<',
      last: '>>',
      next: '>',
      previous: '<',
    },
  },
}

async function loadProducts(request: AjaxData, callback: AjaxCallback): Promise<void> {
  activeListRequest?.abort()
  const controller = new AbortController()
  activeListRequest = controller
  filtersLoading.value = true

  const draw = request.draw ?? 0
  latestDraw = Math.max(latestDraw, draw)
  const start = request.start ?? 0
  const pageSize = request.length && request.length > 0 ? request.length : 10
  const orderedColumn = request.order?.[0]
  const columnName = orderedColumn
    ? request.columns?.[orderedColumn.column]?.name
    : undefined

  try {
    const result = await getProducts({
      paginationEnabled: true,
      pageNumber: Math.floor(start / pageSize) + 1,
      pageSize,
      search: quickSearch.value.trim() || undefined,
      title: stringFilter('title'),
      code: stringFilter('code'),
      unitOfMeasureTitle: stringFilter('unitOfMeasureTitle'),
      description: stringFilter('description'),
      sortBy: typeof columnName === 'string' ? columnName : undefined,
      sortDescending: orderedColumn?.dir === 'desc',
      signal: controller.signal,
    })

    if (draw < latestDraw) return
    rowsById.clear()
    result.items.forEach((item) => rowsById.set(item.id, item))
    callback({
      draw,
      recordsTotal: result.totalCount,
      recordsFiltered: result.filteredCount,
      data: result.items,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      callback({ draw, recordsTotal: 0, recordsFiltered: 0, data: [] })
      return
    }

    if (await redirectWhenUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'خطای ناشناخته‌ای رخ داد.')
    callback({ draw, recordsTotal: 0, recordsFiltered: 0, data: [] })
  } finally {
    if (activeListRequest === controller) filtersLoading.value = false
  }
}

function handleImportFile(event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file || isImporting.value || !canCreate) return
  if (!/\.(xlsx|xlsm)$/i.test(file.name) || file.size === 0 || file.size > 50 * 1024 * 1024) {
    toast.error('فایل xlsx یا xlsm غیرخالی با حجم حداکثر ۵۰ مگابایت انتخاب کنید.')
    return
  }
  pendingImportFile.value = file
  checkOnlyProductCode.value = false
  importModalOpen.value = true
}

function closeImportModal(): void {
  if (isImporting.value) return
  importModalOpen.value = false
  pendingImportFile.value = null
}

async function startImport(): Promise<void> {
  const file = pendingImportFile.value
  if (!file || isImporting.value || !canCreate) return
  importSummary.value = null
  const controller = new AbortController()
  importRequest = controller
  isImporting.value = true
  try {
    const result = await importProducts(file, checkOnlyProductCode.value, controller.signal)
    importSummary.value = result
    importModalOpen.value = false
    pendingImportFile.value = null
    if (result.importedCount > 0) {
      reloadProducts(true)
      unitOptions.value = []
      await ensureUnitOptions()
      toast.success(`${result.importedCount} کالا ثبت شد.`)
    }
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'ایمپورت کالا انجام نشد.')
  } finally {
    isImporting.value = false
    importRequest = null
  }
}
function stringFilter(key: string): string | undefined {
  const value = appliedFilters.value[key]
  return typeof value === 'string' && value.trim() ? value.trim() : undefined
}

function reloadProducts(resetPaging: boolean): void {
  table.value?.dt.ajax.reload(null, resetPaging)
}

function handleSearch(value: string): void {
  quickSearch.value = value
  reloadProducts(true)
}

function handleApply(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadProducts(true)
}

function handleClear(): void {
  quickSearch.value = ''
  appliedFilters.value = {}
  reloadProducts(true)
}

function handleRemove(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadProducts(true)
}

async function ensureValidationRules(): Promise<boolean> {
  if (validationRules.value) return true
  if (validationRequest) return validationRequest

  validationRequest = (async () => {
    try {
      validationRules.value = await getProductValidationRules()
      return true
    } catch (error) {
      if (await redirectWhenUnauthorized(error)) return false
      toast.error(error instanceof Error ? error.message : 'دریافت قواعد فرم انجام نشد.')
      return false
    } finally {
      validationRequest = null
    }
  })()

  return validationRequest
}

async function ensureUnitOptions(): Promise<boolean> {
  if (unitOptions.value.length) return true

  unitOptionsRequest?.abort()
  const controller = new AbortController()
  unitOptionsRequest = controller
  unitOptionsLoading.value = true
  try {
    const result = await getUnitsOfMeasure({
      paginationEnabled: false,
      sortBy: 'title',
      signal: controller.signal,
    })
    unitOptions.value = result.items
    return true
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return false
    if (await redirectWhenUnauthorized(error)) return false
    toast.error(error instanceof Error ? error.message : 'دریافت واحدهای اندازه‌گیری انجام نشد.')
    return false
  } finally {
    if (unitOptionsRequest === controller) unitOptionsLoading.value = false
  }
}

async function openCreateModal(): Promise<void> {
  if (!canCreate) return
  const [rulesReady, unitsReady] = await Promise.all([
    ensureValidationRules(),
    ensureUnitOptions(),
  ])
  if (!rulesReady || !unitsReady) return

  editingItem.value = null
  resetForm({
    values: { title: '', code: '', unitOfMeasureTitle: '', description: '' },
  })
  modalOpen.value = true
}

async function openEditModal(id: string): Promise<void> {
  const item = rowsById.get(id)
  if (!item) return

  const [rulesReady, unitsReady] = await Promise.all([
    ensureValidationRules(),
    ensureUnitOptions(),
  ])
  if (!rulesReady || !unitsReady) return

  editingItem.value = item
  resetForm({
    values: {
      title: item.title,
      code: item.code,
      unitOfMeasureTitle: item.unitOfMeasureTitle,
      description: item.description ?? '',
    },
  })
  modalOpen.value = true
}

function closeModal(): void {
  if (isSubmitting.value) return
  activeOperationRequest?.abort()
  unitMenuOpen.value = false
  modalOpen.value = false
  editingItem.value = null
  resetForm({
    values: { title: '', code: '', unitOfMeasureTitle: '', description: '' },
  })
}

function selectUnit(unitTitle: string): void {
  if (closeUnitMenuTimer !== undefined) window.clearTimeout(closeUnitMenuTimer)
  unitOfMeasureTitle.value = unitTitle
  unitMenuOpen.value = false
}

function scheduleUnitMenuClose(): void {
  closeUnitMenuTimer = window.setTimeout(() => {
    unitMenuOpen.value = false
  }, 150)
}

const saveProduct = handleSubmit(async (values) => {
  activeOperationRequest?.abort()
  const controller = new AbortController()
  activeOperationRequest = controller
  const itemBeingEdited = editingItem.value
  if ((!itemBeingEdited && !canCreate) || (itemBeingEdited && !canManage)) return
  const payload = {
    title: values.title.trim(),
    code: values.code.trim(),
    unitOfMeasureTitle: values.unitOfMeasureTitle.trim(),
    description: values.description.trim() || null,
    concurrencyStamp: itemBeingEdited?.concurrencyStamp ?? null,
  }

  try {
    if (itemBeingEdited) {
      await updateProduct(itemBeingEdited.id, payload, controller.signal)
      toast.success(`کالای «${payload.title}» با موفقیت ویرایش شد.`)
    } else {
      await createProduct(payload, controller.signal)
      toast.success(`کالای «${payload.title}» با موفقیت افزوده شد.`)
    }

    if (!hasExactUnit.value) {
      unitOptions.value.push({
        id: '',
        concurrencyStamp: '',
        title: payload.unitOfMeasureTitle,
        description: null,
      })
    }

    modalOpen.value = false
    editingItem.value = null
    resetForm({
      values: { title: '', code: '', unitOfMeasureTitle: '', description: '' },
    })
    reloadProducts(!itemBeingEdited)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return

    if (error instanceof MasterDataApiError && Object.keys(error.fieldErrors).length) {
      setErrors({
        title: error.fieldErrors.title?.[0],
        code: error.fieldErrors.code?.[0],
        unitOfMeasureTitle: error.fieldErrors.unitOfMeasureTitle?.[0],
        description: error.fieldErrors.description?.[0],
      })
      return
    }

    if (error instanceof Error && error.message.includes('کد کالا')) {
      setErrors({ code: error.message })
      return
    }

    toast.error(error instanceof Error ? error.message : 'ذخیره کالا انجام نشد.')
  }
})

async function removeProduct(id: string): Promise<void> {
  const item = rowsById.get(id)
  if (!item || !confirmDialog.value) return

  const confirmed = await confirmDialog.value.ask({
    title: 'حذف کالا',
    text: `آیا از حذف «${item.title}» مطمئن هستید؟`,
    icon: 'warning',
    confirmButtonText: 'حذف',
    cancelButtonText: 'انصراف',
    confirmButtonColor: '#dc3545',
  })
  if (!confirmed) return

  activeOperationRequest?.abort()
  const controller = new AbortController()
  activeOperationRequest = controller

  try {
    await deleteProduct(id, controller.signal)
    toast.success(`کالای «${item.title}» حذف شد.`)
    reloadProducts(false)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'حذف کالا انجام نشد.')
  }
}

async function redirectWhenUnauthorized(error: unknown): Promise<boolean> {
  const isUnauthorized =
    (error instanceof MasterDataApiError || error instanceof UnitsOfMeasureApiError) &&
    error.status === 401
  if (!isUnauthorized) return false

  await router.replace({
    name: 'login',
    query: { redirect: '/dashboard/master-data/products' },
  })
  return true
}

watch([modalOpen, importModalOpen], ([isOpen, isImportOpen]) => {
  document.body.classList.toggle('modal-open', isOpen || isImportOpen)
})

onMounted(() => {
  void ensureValidationRules()
  void ensureUnitOptions()
})

onBeforeUnmount(() => {
  importRequest?.abort()
  activeListRequest?.abort()
  activeOperationRequest?.abort()
  unitOptionsRequest?.abort()
  if (closeUnitMenuTimer !== undefined) window.clearTimeout(closeUnitMenuTimer)
  document.body.classList.remove('modal-open')
})
</script>

<template>
  <ConfirmDialog ref="confirmDialog" />

  <div class="container-fluid users-page products-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item"><span>تعریف مفروضات</span></li>
        <li class="breadcrumb-item active"><span>تعریف کالا</span></li>
      </ol>
    </div>

    <div class="row">
      <div class="col-12">
        <div class="card">
          <div class="card-header">
            <div>
              <h4 class="card-title">
                <i class="las la-box mr-1"></i>
                تعریف کالا
              </h4>
              <p class="page-description mb-0 mt-1 text-muted">
                تعریف و مدیریت کالاها و واحد اندازه‌گیری آن‌ها
              </p>
            </div>
            <div v-if="canCreate" class="d-flex align-items-center" style="gap: 0.5rem">
            <button
              class="btn btn-primary btn-sm mobile-icon-button"
              type="button"
              title="افزودن کالا"
              aria-label="افزودن کالا"
              @click="openCreateModal"
            >
              <i class="las la-plus ml-1"></i>
              <span class="mobile-button-label">افزودن کالا</span>
            </button>
            <input ref="importInput" type="file" accept=".xlsx,.xlsm" hidden @change="handleImportFile" />
            <button class="btn btn-outline-primary btn-sm" type="button" :disabled="isImporting" @click="importInput?.click()">
              <span v-if="isImporting" class="spinner-border spinner-border-sm ml-1" aria-hidden="true"></span>
              <i v-else class="las la-file-import ml-1" aria-hidden="true"></i>
              {{ isImporting ? 'در حال ایمپورت...' : 'ایمپورت کالا' }}
            </button>
            </div>
          </div>

          <div class="card-body">
            <div v-if="importSummary" class="alert" :class="importSummary.invalidRows.length ? 'alert-danger' : 'alert-success'" role="status">
              <p v-if="importSummary.invalidRows.length">هیچ کالا یا واحدی ثبت نشد. خطاهای زیر را اصلاح و فایل را دوباره ارسال کنید.</p>
              {{ importSummary.importedCount }} ثبت‌شده،
              {{ importSummary.duplicateCount }} تکراری،
              {{ importSummary.emptyRowCount }} ردیف خالی و
              {{ importSummary.invalidRows.length }} ردیف نامعتبر.
              <details v-if="importSummary.invalidRows.length" class="mt-2" open>
                <summary>مشاهده خطاهای ردیف‌ها</summary>
                <ul style="max-height: 20rem; overflow-y: auto">
                  <li v-for="row in importSummary.invalidRows" :key="row.rowNumber">
                    ردیف {{ row.rowNumber }}: {{ row.message }}
                  </li>
                </ul>
              </details>
            </div>
            <DataTableFilters
              v-model="appliedFilters"
              :definitions="filterDefinitions"
              :search="quickSearch"
              :loading="filtersLoading"
              search-placeholder="جستجو در عنوان، کد، واحد و توضیحات..."
              @update:search="handleSearch"
              @apply="handleApply"
              @clear="handleClear"
              @remove="handleRemove"
            />

            <DataTableScrollContainer class="users-table-container">
              <DataTable
                ref="table"
                :options="options"
                class="display responsive nowrap table-striped table table-sm users-table"
              >
                <template #column-title="{ cellData }">
                  <div class="d-flex align-items-center">
                    <span class="badge light badge-primary ml-2">
                      <i class="las la-box"></i>
                    </span>
                    <strong class="text-black">{{ cellData }}</strong>
                  </div>
                </template>

                <template #column-code="{ cellData }">
                  <span class="d-inline-block" dir="ltr">{{ cellData }}</span>
                </template>

                <template #column-unitOfMeasureTitle="{ cellData }">
                  <span class="badge light badge-info">{{ cellData }}</span>
                </template>

                <template #column-description="{ cellData }">
                  <span class="product-description">{{ cellData ?? '' }}</span>
                </template>

                <template v-if="canManage" #column-actions="{ cellData }">
                  <div class="d-inline-flex align-items-center">
                    <button
                      class="master-data-row-action ml-1"
                      type="button"
                      title="ویرایش"
                      aria-label="ویرایش کالا"
                      @click="openEditModal(cellData)"
                    >
                      <i class="las la-pen" aria-hidden="true"></i>
                    </button>
                    <button
                      class="master-data-row-action"
                      type="button"
                      title="حذف"
                      aria-label="حذف کالا"
                      @click="removeProduct(cellData)"
                    >
                      <i class="las la-trash" aria-hidden="true"></i>
                    </button>
                  </div>
                </template>

                <thead>
                  <tr>
                    <th>ردیف</th>
                    <th>عنوان</th>
                    <th>کد کالا</th>
                    <th>واحد اندازه‌گیری</th>
                    <th>توضیحات</th>
                    <th v-if="canManage">عملیات</th>
                  </tr>
                </thead>
              </DataTable>
            </DataTableScrollContainer>
          </div>
        </div>
      </div>
    </div>
  </div>

  <Teleport to="body">
    <template v-if="importModalOpen">
      <div class="modal show d-block" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="product-import-title" @click.self="closeImportModal" @keydown.esc="closeImportModal">
        <div class="modal-dialog modal-dialog-centered" role="document">
          <form class="modal-content" @submit.prevent="startImport">
            <div class="modal-header">
              <h5 id="product-import-title" class="modal-title">ایمپورت کالا</h5>
              <button type="button" class="close" aria-label="بستن" :disabled="isImporting" @click="closeImportModal"><span aria-hidden="true">&times;</span></button>
            </div>
            <div class="modal-body">
              <p>فایل انتخاب‌شده: <bdi>{{ pendingImportFile?.name }}</bdi></p>
              <label class="mb-0 d-flex align-items-center" style="gap: 0.4rem">
                <input v-model="checkOnlyProductCode" type="checkbox" :disabled="isImporting" />
                فقط کد کالا تکراری بررسی شود
              </label>
            </div>
            <div class="modal-footer modal-footer--split">
              <button type="button" class="btn btn-light" :disabled="isImporting" @click="closeImportModal">انصراف</button>
              <button type="submit" class="btn btn-primary" :disabled="isImporting">
                <span v-if="isImporting" class="spinner-border spinner-border-sm ml-1" aria-hidden="true"></span>
                {{ isImporting ? 'در حال ایمپورت...' : 'شروع ایمپورت' }}
              </button>
            </div>
          </form>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>

  <Teleport to="body">
    <template v-if="modalOpen">
      <div
        class="modal show d-block users-role-modal"
        tabindex="-1"
        role="dialog"
        aria-modal="true"
        aria-labelledby="product-modal-title"
        @click.self="closeModal"
      >
        <div class="modal-dialog modal-dialog-centered" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <h5 id="product-modal-title" class="modal-title">{{ modalTitle }}</h5>
              <button
                type="button"
                class="close"
                aria-label="بستن"
                :disabled="isSubmitting"
                @click="closeModal"
              >
                <span aria-hidden="true">&times;</span>
              </button>
            </div>

            <form novalidate @submit="saveProduct">
              <div class="modal-body">
                <div class="form-group">
                  <label for="product-title">
                    عنوان <span class="text-danger" aria-hidden="true">*</span>
                  </label>
                  <input
                    id="product-title"
                    v-model="title"
                    v-bind="titleAttrs"
                    class="form-control"
                    :class="{ 'is-invalid': errors.title }"
                    type="text"
                    :maxlength="titleMaxLength"
                    autocomplete="off"
                    :disabled="isSubmitting"
                  />
                  <div v-if="errors.title" class="invalid-feedback d-block">{{ errors.title }}</div>
                </div>

                <div class="form-group">
                  <label for="product-code">
                    کد کالا <span class="text-danger" aria-hidden="true">*</span>
                  </label>
                  <input
                    id="product-code"
                    v-model="code"
                    v-bind="codeAttrs"
                    class="form-control"
                    :class="{ 'is-invalid': errors.code }"
                    type="text"
                    dir="ltr"
                    :maxlength="codeMaxLength"
                    autocomplete="off"
                    :disabled="isSubmitting"
                  />
                  <div v-if="errors.code" class="invalid-feedback d-block">{{ errors.code }}</div>
                </div>

                <div class="form-group product-unit-field">
                  <label for="product-unit">
                    واحد اندازه‌گیری <span class="text-danger" aria-hidden="true">*</span>
                  </label>
                  <div class="product-unit-combobox">
                    <input
                      id="product-unit"
                      v-model="unitOfMeasureTitle"
                      v-bind="unitOfMeasureTitleAttrs"
                      class="form-control"
                      :class="{ 'is-invalid': errors.unitOfMeasureTitle }"
                      type="text"
                      role="combobox"
                      aria-autocomplete="list"
                      :aria-expanded="unitMenuOpen"
                      aria-controls="product-unit-options"
                      :maxlength="unitTitleMaxLength"
                      autocomplete="off"
                      placeholder="تایپ یا انتخاب واحد"
                      :disabled="isSubmitting || unitOptionsLoading"
                      @focus="unitMenuOpen = true"
                      @input="unitMenuOpen = true"
                      @blur="scheduleUnitMenuClose"
                    />
                    <i
                      :class="unitOptionsLoading ? 'las la-spinner la-spin' : 'las la-angle-down'"
                      aria-hidden="true"
                    ></i>

                    <div
                      v-if="unitMenuOpen && !unitOptionsLoading"
                      id="product-unit-options"
                      class="product-unit-options"
                      role="listbox"
                    >
                      <button
                        v-for="unit in filteredUnitOptions"
                        :key="unit.id"
                        class="product-unit-option"
                        type="button"
                        role="option"
                        @mousedown.prevent="selectUnit(unit.title)"
                      >
                        {{ unit.title }}
                      </button>
                      <button
                        v-if="unitOfMeasureTitle.trim() && !hasExactUnit"
                        class="product-unit-option product-unit-option--create"
                        type="button"
                        role="option"
                        @mousedown.prevent="selectUnit(unitOfMeasureTitle.trim())"
                      >
                        <i class="las la-plus" aria-hidden="true"></i>
                        افزودن واحد «{{ unitOfMeasureTitle.trim() }}»
                      </button>
                      <span
                        v-if="!filteredUnitOptions.length && (!unitOfMeasureTitle.trim() || hasExactUnit)"
                        class="product-unit-empty"
                      >
                        واحدی پیدا نشد.
                      </span>
                    </div>
                  </div>
                  <small v-if="unitOfMeasureTitle.trim() && !hasExactUnit" class="form-text text-muted">
                    این واحد جدید است و هم‌زمان با ذخیره کالا ثبت می‌شود.
                  </small>
                  <div v-if="errors.unitOfMeasureTitle" class="invalid-feedback d-block">
                    {{ errors.unitOfMeasureTitle }}
                  </div>
                </div>

                <div class="form-group mb-0">
                  <label for="product-description">توضیحات</label>
                  <textarea
                    id="product-description"
                    v-model="description"
                    v-bind="descriptionAttrs"
                    class="form-control"
                    :class="{ 'is-invalid': errors.description }"
                    rows="4"
                    :maxlength="descriptionMaxLength"
                    :disabled="isSubmitting"
                  ></textarea>
                  <div v-if="errors.description" class="invalid-feedback d-block">
                    {{ errors.description }}
                  </div>
                </div>
              </div>

              <div class="modal-footer modal-footer--split">
                <button type="button" class="btn btn-light" :disabled="isSubmitting" @click="closeModal">
                  انصراف
                </button>
                <button class="btn btn-primary" type="submit" :disabled="isSubmitting">
                  <span
                    v-if="isSubmitting"
                    class="spinner-border spinner-border-sm ml-2"
                    role="status"
                  ></span>
                  ذخیره
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>
</template>

<style scoped>
.product-description {
  display: inline-block;
  max-width: 32rem;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: middle;
  white-space: nowrap;
}

.product-unit-combobox {
  position: relative;
}

.product-unit-combobox > i {
  color: var(--text-secondary);
  left: 0.75rem;
  pointer-events: none;
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
}

.product-unit-combobox .form-control {
  padding-left: 2.25rem;
}

.product-unit-options {
  background: var(--surface);
  border: 1px solid var(--border-color);
  border-radius: 0.5rem;
  box-shadow: 0 0.75rem 1.5rem rgba(15, 23, 42, 0.14);
  left: 0;
  max-height: 13rem;
  overflow-y: auto;
  position: absolute;
  right: 0;
  top: calc(100% + 0.35rem);
  z-index: 1060;
}

.product-unit-option {
  background: transparent;
  border: 0;
  color: var(--text-primary);
  cursor: pointer;
  display: block;
  padding: 0.55rem 0.75rem;
  text-align: right;
  width: 100%;
}

.product-unit-option:hover,
.product-unit-option:focus {
  background: var(--primary-light);
  color: var(--primary-dark);
  outline: 0;
}

.product-unit-option--create {
  border-top: 1px solid var(--border-color);
  color: var(--primary);
  font-weight: 600;
}

.product-unit-empty {
  color: var(--text-secondary);
  display: block;
  padding: 0.75rem;
}
</style>
