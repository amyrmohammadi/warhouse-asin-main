<script setup lang="ts">
import { toTypedSchema } from '@vee-validate/zod'
import type { AjaxCallback, AjaxData, ColumnsConfig, Config } from 'datatables.net'
import DataTablesCore from 'datatables.net-bs4'
import DataTable from 'datatables.net-vue3'
import { Field, useForm } from 'vee-validate'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { z, type ZodTypeAny } from 'zod'

import ConfirmDialog from '@/components/ConfirmDialog.vue'
import DataTableScrollContainer from '@/components/DataTableScrollContainer.vue'
import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import { getSession } from '@/services/auth'
import {
  createMasterDataItem,
  deleteMasterDataItem,
  getMasterDataList,
  getMasterDataValidationRules,
  importMasterDataFile,
  MasterDataApiError,
  type MasterDataItem,
  type MasterDataValidationRules,
  updateMasterDataItem,
} from '@/services/masterData'
import { toast } from '@/services/toast'
import type {
  DataTableFilterDefinition,
  DataTableFilterValues,
} from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'

interface MasterDataFieldConfig {
  key: string
  label: string
  placeholder: string
  type?: 'text' | 'textarea' | 'tel'
  sortable?: boolean
  tableClassName?: string
  inputDirection?: 'ltr' | 'rtl'
}

interface MasterDataPageConfig {
  basePath: string
  routePath: string
  singularLabel: string
  pluralLabel: string
  pageDescription: string
  icon: string
  quickSearchPlaceholder: string
  fields: MasterDataFieldConfig[]
  import?: {
    label: string
  }
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

const props = defineProps<{ config: MasterDataPageConfig }>()

DataTable.use(DataTablesCore)

const router = useRouter()
const table = ref<{
  dt: { ajax: { reload: (callback: null, resetPaging: boolean) => unknown } }
} | null>(null)
const confirmDialog = ref<ConfirmDialogInstance | null>(null)
const quickSearch = ref('')
const appliedFilters = ref<DataTableFilterValues>({})
const filtersLoading = ref(false)
const importInput = ref<HTMLInputElement | null>(null)
const isImporting = ref(false)
const modalOpen = ref(false)
const editingItem = ref<MasterDataItem | null>(null)
const validationRules = ref<MasterDataValidationRules | null>(null)
const rowsById = new Map<string, MasterDataItem>()
const canManage = getSession()?.permissions.includes('MasterDataManage') ?? false
let activeListRequest: AbortController | null = null
let activeOperationRequest: AbortController | null = null
let activeImportRequest: AbortController | null = null
let validationRequest: Promise<boolean> | null = null
let latestDraw = 0

const modalTitle = computed(() =>
  editingItem.value
    ? `ویرایش ${props.config.singularLabel}`
    : `افزودن ${props.config.singularLabel}`,
)

const validationSchema = computed(() => {
  const shape: Record<string, ZodTypeAny> = {}

  for (const field of props.config.fields) {
    const rules = validationRules.value?.[field.key]
    let schema = z.string().trim()

    if (rules?.required) {
      schema = schema.min(1, `وارد کردن ${field.label} الزامی است.`)
    }

    if (rules?.maxLength) {
      schema = schema.max(
        rules.maxLength,
        `${field.label} نباید بیشتر از ${rules.maxLength} نویسه باشد.`,
      )
    }

    shape[field.key] = schema
  }

  return toTypedSchema(z.object(shape))
})

const { handleSubmit, isSubmitting, resetForm, setErrors } = useForm<Record<string, string>>({
  validationSchema,
  initialValues: emptyFormValues(),
})

const filterDefinitions = computed<DataTableFilterDefinition[]>(() =>
  props.config.fields.map((field) => ({
    key: field.key,
    label: field.label,
    type: 'text',
    placeholder: field.placeholder,
    maxlength: validationRules.value?.[field.key]?.maxLength ?? undefined,
  })),
)

const columns: ColumnsConfig[] = [
  createRowNumberColumn(),
  ...props.config.fields.map((field, index) => ({
    data: field.key,
    name: field.key,
    title: field.label,
    className: index === 0 ? 'all' : (field.tableClassName ?? 'min-tablet-l'),
    orderable: field.sortable ?? true,
  })),
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
  ajax: loadItems,
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
    emptyTable: `${props.config.singularLabel} برای نمایش وجود ندارد.`,
    info: '_START_–_END_ از _TOTAL_',
    infoEmpty: '۰ از ۰',
    infoFiltered: '',
    lengthMenu: '_MENU_',
    loadingRecords: `در حال دریافت ${props.config.pluralLabel}...`,
    processing: 'در حال پردازش...',
    zeroRecords: `${props.config.singularLabel} با این مشخصات پیدا نشد.`,
    paginate: {
      first: '<<',
      last: '>>',
      next: '>',
      previous: '<',
    },
  },
}

async function loadItems(request: AjaxData, callback: AjaxCallback): Promise<void> {
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
    const filters = Object.fromEntries(
      props.config.fields.map((field) => [field.key, stringFilter(field.key)]),
    )
    const result = await getMasterDataList(props.config.basePath, {
      paginationEnabled: true,
      pageNumber: Math.floor(start / pageSize) + 1,
      pageSize,
      search: quickSearch.value.trim() || undefined,
      filters,
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

function emptyFormValues(): Record<string, string> {
  return Object.fromEntries(props.config.fields.map((field) => [field.key, '']))
}

function stringFilter(key: string): string | undefined {
  const value = appliedFilters.value[key]
  return typeof value === 'string' && value.trim() ? value.trim() : undefined
}

function reloadItems(resetPaging: boolean): void {
  table.value?.dt.ajax.reload(null, resetPaging)
}

function openImportPicker(): void {
  if (isImporting.value) return
  importInput.value?.click()
}

async function handleImportFile(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  activeImportRequest?.abort()
  const controller = new AbortController()
  activeImportRequest = controller
  isImporting.value = true

  try {
    const result = await importMasterDataFile(
      props.config.basePath,
      file,
      controller.signal,
    )
    const invalidCount = result.invalidRows.length
    const message = [
      `${result.importedCount} مورد ثبت شد`,
      `${result.duplicateCount} مورد تکراری`,
      `${invalidCount} مورد نامعتبر`,
    ].join('، ')

    if (invalidCount > 0) {
      toast.warning(message, 'ایمپورت با هشدار انجام شد')
    } else {
      toast.success(message, 'ایمپورت انجام شد')
    }

    if (result.importedCount > 0) reloadItems(false)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'ایمپورت فایل Excel انجام نشد.')
  } finally {
    if (activeImportRequest === controller) {
      activeImportRequest = null
      isImporting.value = false
    }
  }
}

function handleSearch(value: string): void {
  quickSearch.value = value
  reloadItems(true)
}

function handleApply(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadItems(true)
}

function handleClear(): void {
  quickSearch.value = ''
  appliedFilters.value = {}
  reloadItems(true)
}

function handleRemove(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadItems(true)
}

async function ensureValidationRules(): Promise<boolean> {
  if (validationRules.value) return true
  if (validationRequest) return validationRequest

  validationRequest = (async () => {
    try {
      validationRules.value = await getMasterDataValidationRules(props.config.basePath)
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

async function openCreateModal(): Promise<void> {
  if (!(await ensureValidationRules())) return
  editingItem.value = null
  resetForm({ values: emptyFormValues() })
  modalOpen.value = true
}

async function openEditModal(id: string): Promise<void> {
  const item = rowsById.get(id)
  if (!item || !(await ensureValidationRules())) return

  editingItem.value = item
  resetForm({
    values: Object.fromEntries(
      props.config.fields.map((field) => [field.key, item[field.key] ?? '']),
    ),
  })
  modalOpen.value = true
}

function closeModal(): void {
  if (isSubmitting.value) return
  activeOperationRequest?.abort()
  modalOpen.value = false
  editingItem.value = null
  resetForm({ values: emptyFormValues() })
}

const saveItem = handleSubmit(async (values) => {
  activeOperationRequest?.abort()
  const controller = new AbortController()
  activeOperationRequest = controller
  const itemBeingEdited = editingItem.value
  const payload = Object.fromEntries(
    props.config.fields.map((field) => {
      const value = values[field.key]?.trim() ?? ''
      const required = validationRules.value?.[field.key]?.required ?? false
      return [field.key, value || (required ? '' : null)]
    }),
  )
  if (itemBeingEdited) payload.concurrencyStamp = itemBeingEdited.concurrencyStamp
  const displayValue = String(payload[props.config.fields[0].key] ?? '')

  try {
    if (itemBeingEdited) {
      await updateMasterDataItem(
        props.config.basePath,
        itemBeingEdited.id,
        payload,
        controller.signal,
      )
      toast.success(`${props.config.singularLabel} «${displayValue}» با موفقیت ویرایش شد.`)
    } else {
      await createMasterDataItem(props.config.basePath, payload, controller.signal)
      toast.success(`${props.config.singularLabel} «${displayValue}» با موفقیت افزوده شد.`)
    }

    modalOpen.value = false
    editingItem.value = null
    resetForm({ values: emptyFormValues() })
    reloadItems(!itemBeingEdited)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return

    if (error instanceof MasterDataApiError && Object.keys(error.fieldErrors).length) {
      setErrors(
        Object.fromEntries(
          props.config.fields.map((field) => [
            field.key,
            error.fieldErrors[field.key]?.[0],
          ]),
        ),
      )
      return
    }

    toast.error(error instanceof Error ? error.message : 'ذخیره اطلاعات انجام نشد.')
  }
})

async function removeItem(id: string): Promise<void> {
  const item = rowsById.get(id)
  if (!item || !confirmDialog.value) return
  const displayValue = item[props.config.fields[0].key] ?? ''

  const confirmed = await confirmDialog.value.ask({
    title: `حذف ${props.config.singularLabel}`,
    text: `آیا از حذف «${displayValue}» مطمئن هستید؟`,
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
    await deleteMasterDataItem(props.config.basePath, id, controller.signal)
    toast.success(`${props.config.singularLabel} «${displayValue}» حذف شد.`)
    reloadItems(false)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'حذف اطلاعات انجام نشد.')
  }
}

function maxLengthFor(key: string): number | undefined {
  return validationRules.value?.[key]?.maxLength ?? undefined
}

async function redirectWhenUnauthorized(error: unknown): Promise<boolean> {
  if (!(error instanceof MasterDataApiError) || error.status !== 401) return false
  await router.replace({
    name: 'login',
    query: { redirect: props.config.routePath },
  })
  return true
}

watch(modalOpen, (isOpen) => {
  document.body.classList.toggle('modal-open', isOpen)
})

onMounted(() => {
  void ensureValidationRules()
})

onBeforeUnmount(() => {
  activeListRequest?.abort()
  activeOperationRequest?.abort()
  activeImportRequest?.abort()
  document.body.classList.remove('modal-open')
})
</script>

<template>
  <ConfirmDialog ref="confirmDialog" />

  <div class="container-fluid users-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item"><span>تعریف مفروضات</span></li>
        <li class="breadcrumb-item active"><span>{{ config.singularLabel }}</span></li>
      </ol>
    </div>

    <div class="row">
      <div class="col-12">
        <div class="card">
          <div class="card-header">
            <div>
              <h4 class="card-title">
                <i :class="[config.icon, 'mr-1']"></i>
                {{ config.pluralLabel }}
              </h4>
              <p class="page-description mb-0 mt-1 text-muted">
                {{ config.pageDescription }}
              </p>
            </div>
            <div v-if="canManage" class="d-flex align-items-center">
              <template v-if="config.import">
                <input
                  ref="importInput"
                  class="d-none"
                  type="file"
                  accept=".xlsx,.xlsm,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,application/vnd.ms-excel.sheet.macroEnabled.12"
                  @change="handleImportFile"
                />
                <button
                  class="btn btn-outline-primary btn-sm mobile-icon-button ml-2"
                  type="button"
                  :disabled="isImporting"
                  :title="config.import.label"
                  :aria-label="config.import.label"
                  @click="openImportPicker"
                >
                  <span
                    v-if="isImporting"
                    class="spinner-border spinner-border-sm ml-1"
                    role="status"
                    aria-hidden="true"
                  ></span>
                  <i v-else class="las la-file-import ml-1" aria-hidden="true"></i>
                  <span class="mobile-button-label">{{ config.import.label }}</span>
                </button>
              </template>

              <button
                class="btn btn-primary btn-sm mobile-icon-button"
                type="button"
                :title="`افزودن ${config.singularLabel}`"
                :aria-label="`افزودن ${config.singularLabel}`"
                @click="openCreateModal"
              >
                <i class="las la-plus ml-1"></i>
                <span class="mobile-button-label">افزودن {{ config.singularLabel }}</span>
              </button>
            </div>
          </div>

          <div class="card-body">
            <DataTableFilters
              v-model="appliedFilters"
              :definitions="filterDefinitions"
              :search="quickSearch"
              :loading="filtersLoading"
              :search-placeholder="config.quickSearchPlaceholder"
              @update:search="handleSearch"
              @apply="handleApply"
              @clear="handleClear"
              @remove="handleRemove"
            />

            <DataTableScrollContainer class="users-table-container">
              <DataTable
                ref="table"
                :options="options"
                class="display responsive nowrap table table-sm users-table"
              >
                <template v-if="canManage" #column-actions="{ cellData }">
                  <div class="d-inline-flex align-items-center">
                    <button
                      class="master-data-row-action ml-1"
                      type="button"
                      title="ویرایش"
                      :aria-label="`ویرایش ${config.singularLabel}`"
                      @click="openEditModal(cellData)"
                    >
                      <i class="las la-pen" aria-hidden="true"></i>
                    </button>
                    <button
                      class="master-data-row-action"
                      type="button"
                      title="حذف"
                      :aria-label="`حذف ${config.singularLabel}`"
                      @click="removeItem(cellData)"
                    >
                      <i class="las la-trash" aria-hidden="true"></i>
                    </button>
                  </div>
                </template>

                <thead>
                  <tr>
                    <th>ردیف</th>
                    <th v-for="field in config.fields" :key="field.key">
                      {{ field.label }}
                    </th>
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
    <template v-if="modalOpen">
      <div
        class="modal show d-block users-role-modal"
        tabindex="-1"
        role="dialog"
        aria-modal="true"
        aria-labelledby="master-data-modal-title"
        @click.self="closeModal"
      >
        <div class="modal-dialog modal-dialog-centered" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <h5 id="master-data-modal-title" class="modal-title">{{ modalTitle }}</h5>
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

            <form novalidate @submit="saveItem">
              <div class="modal-body">
                <Field
                  v-for="fieldConfig in config.fields"
                  :key="fieldConfig.key"
                  v-slot="{ field, errorMessage }"
                  :name="fieldConfig.key"
                >
                  <div class="form-group">
                    <label :for="`master-data-${fieldConfig.key}`">
                      {{ fieldConfig.label }}
                      <span
                        v-if="validationRules?.[fieldConfig.key]?.required"
                        class="text-danger"
                        aria-hidden="true"
                      >*</span>
                    </label>

                    <textarea
                      v-if="fieldConfig.type === 'textarea'"
                      :id="`master-data-${fieldConfig.key}`"
                      v-bind="field"
                      class="form-control"
                      :class="{ 'is-invalid': errorMessage }"
                      rows="4"
                      :maxlength="maxLengthFor(fieldConfig.key)"
                      :placeholder="fieldConfig.placeholder"
                      :disabled="isSubmitting"
                    ></textarea>
                    <input
                      v-else
                      :id="`master-data-${fieldConfig.key}`"
                      v-bind="field"
                      class="form-control"
                      :class="{ 'is-invalid': errorMessage }"
                      :type="fieldConfig.type === 'tel' ? 'tel' : 'text'"
                      :dir="fieldConfig.inputDirection"
                      :maxlength="maxLengthFor(fieldConfig.key)"
                      :placeholder="fieldConfig.placeholder"
                      autocomplete="off"
                      :disabled="isSubmitting"
                    />

                    <div v-if="errorMessage" class="invalid-feedback d-block">
                      {{ errorMessage }}
                    </div>
                  </div>
                </Field>
              </div>

              <div class="modal-footer modal-footer--split">
                <button
                  type="button"
                  class="btn btn-light"
                  :disabled="isSubmitting"
                  @click="closeModal"
                >
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
