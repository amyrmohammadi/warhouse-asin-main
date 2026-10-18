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
import { toast } from '@/services/toast'
import {
  createUnitOfMeasure,
  deleteUnitOfMeasure,
  getUnitOfMeasureValidationRules,
  getUnitsOfMeasure,
  type UnitOfMeasure,
  type UnitOfMeasureValidationRules,
  UnitsOfMeasureApiError,
  updateUnitOfMeasure,
} from '@/services/unitsOfMeasure'
import type {
  DataTableFilterDefinition,
  DataTableFilterValues,
} from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'

defineOptions({ name: 'UnitsOfMeasureView' })

DataTable.use(DataTablesCore)

interface FormValues {
  title: string
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
const quickSearch = ref('')
const appliedFilters = ref<DataTableFilterValues>({})
const filtersLoading = ref(false)
const modalOpen = ref(false)
const editingItem = ref<UnitOfMeasure | null>(null)
const validationRules = ref<UnitOfMeasureValidationRules | null>(null)
const rowsById = new Map<string, UnitOfMeasure>()
const canManage =
  getSession()?.permissions.includes('MasterDataManage') ?? false
let activeListRequest: AbortController | null = null
let activeOperationRequest: AbortController | null = null
let validationRequest: Promise<boolean> | null = null
let latestDraw = 0

const modalTitle = computed(() =>
  editingItem.value ? 'ویرایش واحد اندازه‌گیری' : 'افزودن واحد اندازه‌گیری',
)
const titleMaxLength = computed(() => validationRules.value?.title?.maxLength ?? undefined)
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
    description: '',
  },
})
const [title, titleAttrs] = defineField('title')
const [description, descriptionAttrs] = defineField('description')

const filterDefinitions = computed<DataTableFilterDefinition[]>(() => [
  {
    key: 'title',
    label: 'عنوان',
    type: 'text',
    placeholder: 'عنوان واحد را وارد کنید',
    maxlength: titleMaxLength.value,
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
  ajax: loadUnits,
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
    emptyTable: 'واحد اندازه‌گیری برای نمایش وجود ندارد.',
    info: '_START_–_END_ از _TOTAL_',
    infoEmpty: '۰ از ۰',
    infoFiltered: '',
    lengthMenu: '_MENU_',
    loadingRecords: 'در حال دریافت واحدهای اندازه‌گیری...',
    processing: 'در حال پردازش...',
    zeroRecords: 'واحدی با این مشخصات پیدا نشد.',
    paginate: {
      first: '<<',
      last: '>>',
      next: '>',
      previous: '<',
    },
  },
}

async function loadUnits(request: AjaxData, callback: AjaxCallback): Promise<void> {
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
    const result = await getUnitsOfMeasure({
      paginationEnabled: true,
      pageNumber: Math.floor(start / pageSize) + 1,
      pageSize,
      search: quickSearch.value.trim() || undefined,
      title: stringFilter('title'),
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

function stringFilter(key: string): string | undefined {
  const value = appliedFilters.value[key]
  return typeof value === 'string' && value.trim() ? value.trim() : undefined
}

function reloadUnits(resetPaging: boolean): void {
  table.value?.dt.ajax.reload(null, resetPaging)
}

function handleSearch(value: string): void {
  quickSearch.value = value
  reloadUnits(true)
}

function handleApply(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadUnits(true)
}

function handleClear(): void {
  quickSearch.value = ''
  appliedFilters.value = {}
  reloadUnits(true)
}

function handleRemove(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadUnits(true)
}

async function ensureValidationRules(): Promise<boolean> {
  if (validationRules.value) return true
  if (validationRequest) return validationRequest

  validationRequest = (async () => {
    try {
      validationRules.value = await getUnitOfMeasureValidationRules()
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
  resetForm({ values: { title: '', description: '' } })
  modalOpen.value = true
}

async function openEditModal(id: string): Promise<void> {
  const item = rowsById.get(id)
  if (!item || !(await ensureValidationRules())) return

  editingItem.value = item
  resetForm({
    values: {
      title: item.title,
      description: item.description ?? '',
    },
  })
  modalOpen.value = true
}

function closeModal(): void {
  if (isSubmitting.value) return
  activeOperationRequest?.abort()
  modalOpen.value = false
  editingItem.value = null
  resetForm({ values: { title: '', description: '' } })
}

const saveUnit = handleSubmit(async (values) => {
  activeOperationRequest?.abort()
  const controller = new AbortController()
  activeOperationRequest = controller
  const itemBeingEdited = editingItem.value
  const payload = {
    title: values.title.trim(),
    description: values.description.trim() || null,
  }

  try {
    if (itemBeingEdited) {
      await updateUnitOfMeasure(itemBeingEdited.id, payload, controller.signal)
      toast.success(`واحد «${payload.title}» با موفقیت ویرایش شد.`)
    } else {
      await createUnitOfMeasure(payload, controller.signal)
      toast.success(`واحد «${payload.title}» با موفقیت افزوده شد.`)
    }

    modalOpen.value = false
    editingItem.value = null
    resetForm({ values: { title: '', description: '' } })
    reloadUnits(!itemBeingEdited)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return

    if (error instanceof UnitsOfMeasureApiError && Object.keys(error.fieldErrors).length) {
      setErrors({
        title: error.fieldErrors.title?.[0],
        description: error.fieldErrors.description?.[0],
      })
      return
    }

    toast.error(error instanceof Error ? error.message : 'ذخیره واحد اندازه‌گیری انجام نشد.')
  }
})

async function removeUnit(id: string): Promise<void> {
  const item = rowsById.get(id)
  if (!item || !confirmDialog.value) return

  const confirmed = await confirmDialog.value.ask({
    title: 'حذف واحد اندازه‌گیری',
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
    await deleteUnitOfMeasure(id, controller.signal)
    toast.success(`واحد «${item.title}» حذف شد.`)
    reloadUnits(false)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'حذف واحد اندازه‌گیری انجام نشد.')
  }
}

async function redirectWhenUnauthorized(error: unknown): Promise<boolean> {
  if (!(error instanceof UnitsOfMeasureApiError) || error.status !== 401) return false
  await router.replace({
    name: 'login',
    query: { redirect: '/dashboard/master-data/units-of-measure' },
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
        <li class="breadcrumb-item active"><span>واحد اندازه‌گیری</span></li>
      </ol>
    </div>

    <div class="row">
      <div class="col-12">
        <div class="card">
          <div class="card-header">
            <div>
              <h4 class="card-title">
                <i class="las la-balance-scale mr-1"></i>
                واحدهای اندازه‌گیری
              </h4>
              <p class="page-description mb-0 mt-1 text-muted">
                تعریف و مدیریت واحدهای قابل استفاده در عملیات انبار
              </p>
            </div>
            <button
              v-if="canManage"
              class="btn btn-primary btn-sm mobile-icon-button"
              type="button"
              title="افزودن واحد اندازه‌گیری"
              aria-label="افزودن واحد اندازه‌گیری"
              @click="openCreateModal"
            >
              <i class="las la-plus ml-1"></i>
              <span class="mobile-button-label">افزودن واحد</span>
            </button>
          </div>

          <div class="card-body">
            <DataTableFilters
              v-model="appliedFilters"
              :definitions="filterDefinitions"
              :search="quickSearch"
              :loading="filtersLoading"
              search-placeholder="جستجو در عنوان و توضیحات..."
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
                      <i class="las la-balance-scale"></i>
                    </span>
                    <strong class="text-black">{{ cellData }}</strong>
                  </div>
                </template>

                <template #column-description="{ cellData }">
                  <span class="unit-description">{{ cellData || '—' }}</span>
                </template>

                <template v-if="canManage" #column-actions="{ cellData }">
                  <div class="d-inline-flex align-items-center">
                    <button
                      class="master-data-row-action ml-1"
                      type="button"
                      title="ویرایش"
                      aria-label="ویرایش واحد اندازه‌گیری"
                      @click="openEditModal(cellData)"
                    >
                      <i class="las la-pen" aria-hidden="true"></i>
                    </button>
                    <button
                      class="master-data-row-action"
                      type="button"
                      title="حذف"
                      aria-label="حذف واحد اندازه‌گیری"
                      @click="removeUnit(cellData)"
                    >
                      <i class="las la-trash" aria-hidden="true"></i>
                    </button>
                  </div>
                </template>

                <thead>
                  <tr>
                    <th>ردیف</th>
                    <th>عنوان</th>
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
    <template v-if="modalOpen">
      <div
        class="modal show d-block users-role-modal"
        tabindex="-1"
        role="dialog"
        aria-modal="true"
        aria-labelledby="unit-modal-title"
        @click.self="closeModal"
      >
        <div class="modal-dialog modal-dialog-centered" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <h5 id="unit-modal-title" class="modal-title">{{ modalTitle }}</h5>
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

            <form novalidate @submit="saveUnit">
              <div class="modal-body">
                <div class="form-group">
                  <label for="unit-title">
                    عنوان
                    <span class="text-danger" aria-hidden="true">*</span>
                  </label>
                  <input
                    id="unit-title"
                    v-model="title"
                    v-bind="titleAttrs"
                    class="form-control"
                    :class="{ 'is-invalid': errors.title }"
                    type="text"
                    :maxlength="titleMaxLength"
                    autocomplete="off"
                    :disabled="isSubmitting"
                  />
                  <div v-if="errors.title" class="invalid-feedback d-block">
                    {{ errors.title }}
                  </div>
                </div>

                <div class="form-group mb-0">
                  <label for="unit-description">توضیحات</label>
                  <textarea
                    id="unit-description"
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
                <button
                  type="button"
                  class="btn btn-light "
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

<style scoped>
.unit-description {
  display: inline-block;
  max-width: 32rem;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: middle;
  white-space: nowrap;
}
</style>
