<script setup lang="ts">
import DataTable from 'datatables.net-vue3'
import DataTablesCore from 'datatables.net-bs4'
import type { AjaxCallback, AjaxData, ColumnsConfig, Config } from 'datatables.net'
import { onBeforeUnmount, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import DataTableScrollContainer from '@/components/DataTableScrollContainer.vue'
import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import {
  AuditLogsApiError,
  getAuditLogs,
  type AuditAction,
  type AuditLogListItem,
} from '@/services/auditLogs'
import { toast } from '@/services/toast'
import type {
  DataTableFilterDefinition,
  DataTableFilterValues,
} from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'

defineOptions({ name: 'AuditLogsView' })

DataTable.use(DataTablesCore)

const router = useRouter()
const selectedAudit = ref<AuditLogListItem | null>(null)
const table = ref<{
  dt: { ajax: { reload: (callback: null, resetPaging: boolean) => unknown } }
} | null>(null)
const quickSearch = ref('')
const appliedFilters = ref<DataTableFilterValues>({})
const filtersLoading = ref(false)
const auditLogsById = new Map<number, AuditLogListItem>()
let activeRequest: AbortController | null = null
let latestDraw = 0

const filterDefinitions: DataTableFilterDefinition[] = [
  {
    key: 'userFullName',
    label: 'نام کاربر',
    type: 'text',
    placeholder: 'نام کاربر را وارد کنید',
    maxlength: 200,
  },
  {
    key: 'action',
    label: 'نوع عملیات',
    type: 'select',
    placeholder: 'همه عملیات‌ها',
    options: [
      { value: 'Create', label: 'ایجاد' },
      { value: 'Update', label: 'ویرایش' },
      { value: 'Delete', label: 'حذف' },
    ],
  },
  {
    key: 'tableName',
    label: 'نام جدول',
    type: 'text',
    placeholder: 'نام جدول را وارد کنید',
    maxlength: 128,
  },
  {
    key: 'primaryKey',
    label: 'کلید رکورد',
    type: 'text',
    placeholder: 'کلید رکورد را وارد کنید',
    maxlength: 256,
  },
  {
    key: 'changedColumns',
    label: 'ستون تغییرکرده',
    type: 'text',
    placeholder: 'نام ستون را وارد کنید',
    maxlength: 256,
  },
]

const columns: ColumnsConfig[] = [
  createRowNumberColumn(),
  { data: 'occurredAtUtc', name: 'occurredAtUtc', title: 'زمان رخداد', className: 'all' },
  {
    data: 'userFullName',
    name: 'userFullName',
    title: 'نام کامل کاربر',
    className: 'min-tablet-l',
  },
  { data: 'action', name: 'action', title: 'عملیات', className: 'min-tablet-l' },
  { data: 'tableName', name: 'tableName', title: 'جدول', className: 'min-tablet-l' },
  {
    data: 'primaryKey',
    name: 'primaryKey',
    title: 'کلید رکورد',
    className: 'min-tablet-l',
    orderable: false,
  },
  {
    data: 'changedColumns',
    name: 'changedColumns',
    title: 'ستون‌های تغییرکرده',
    className: 'min-tablet-l',
    orderable: false,
  },
  {
    data: 'id',
    name: 'details',
    title: 'جزئیات',
    className: 'min-tablet-l',
    orderable: false,
    searchable: false,
  },
]

const options: Config = {
  ajax: loadAuditLogs,
  columns,
  serverSide: true,
  processing: true,
  searching: false,
  pageLength: 10,
  lengthMenu: [5, 10, 25, 50],
  order: [[1, 'desc']],
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
    emptyTable: 'گزارشی برای نمایش وجود ندارد.',
    info: '_START_–_END_ از _TOTAL_',
    infoEmpty: '۰ از ۰',
    infoFiltered: '',
    lengthMenu: '_MENU_',
    loadingRecords: 'در حال دریافت گزارش‌ها...',
    processing: 'در حال پردازش...',
    search: 'جستجو:',
    searchPlaceholder: 'کاربر، جدول، شناسه یا عملیات',
    zeroRecords: 'گزارشی با این مشخصات پیدا نشد.',
    paginate: {
      first: '<<',
      last: '>>',
      next: '>',
      previous: '<',
    },
  },
}

async function loadAuditLogs(request: AjaxData, callback: AjaxCallback): Promise<void> {
  activeRequest?.abort()
  const controller = new AbortController()
  activeRequest = controller
  filtersLoading.value = true

  const draw = request.draw ?? 0
  latestDraw = Math.max(latestDraw, draw)
  const start = request.start ?? 0
  const pageSize = request.length && request.length > 0 ? request.length : 10
  const orderedColumn = request.order?.[0]
  const columnName = orderedColumn ? request.columns?.[orderedColumn.column]?.name : undefined
  const sortBy = typeof columnName === 'string' ? columnName : undefined

  try {
    const result = await getAuditLogs({
      paginationEnabled: true,
      pageNumber: Math.floor(start / pageSize) + 1,
      pageSize,
      search: quickSearch.value || undefined,
      userFullName: stringFilter('userFullName'),
      action: actionFilter(),
      tableName: stringFilter('tableName'),
      primaryKey: stringFilter('primaryKey'),
      changedColumns: stringFilter('changedColumns'),
      sortBy,
      sortDescending: orderedColumn?.dir !== 'asc',
      signal: controller.signal,
    })

    if (draw < latestDraw) return
    auditLogsById.clear()
    for (const audit of result.items) auditLogsById.set(audit.id, audit)

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

    if (error instanceof AuditLogsApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: '/dashboard/audit-logs' } })
      return
    }

    toast.error(error instanceof Error ? error.message : 'خطای ناشناخته‌ای رخ داد.')
    callback({ draw, recordsTotal: 0, recordsFiltered: 0, data: [] })
  } finally {
    if (activeRequest === controller) filtersLoading.value = false
  }
}

function stringFilter(key: string): string | undefined {
  const value = appliedFilters.value[key]
  return typeof value === 'string' && value.trim() ? value.trim() : undefined
}

function actionFilter(): AuditAction | undefined {
  const value = stringFilter('action')
  return value === 'Create' || value === 'Update' || value === 'Delete'
    ? value
    : undefined
}

function reloadAuditLogs(resetPaging: boolean): void {
  table.value?.dt.ajax.reload(null, resetPaging)
}

function handleSearch(value: string): void {
  quickSearch.value = value
  reloadAuditLogs(true)
}

function handleApply(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadAuditLogs(true)
}

function handleClear(): void {
  quickSearch.value = ''
  appliedFilters.value = {}
  reloadAuditLogs(true)
}

function handleRemove(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadAuditLogs(true)
}

function actionLabel(action: AuditAction): string {
  return { Create: 'ایجاد', Update: 'ویرایش', Delete: 'حذف' }[action]
}

function actionClass(action: AuditAction): string {
  return {
    Create: 'badge-success',
    Update: 'badge-info',
    Delete: 'badge-danger',
  }[action]
}

function actorLabel(audit: AuditLogListItem): string {
  if (audit.userFullName === 'System') return 'سیستم'
  return audit.userFullName || ''
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('fa-IR', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(new Date(value))
}

function formatJson(value: string | null): string {
  if (!value) return ''

  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}

function formatChangedColumns(value: string | null): string {
  if (!value) return ''

  try {
    const columns = JSON.parse(value) as unknown
    return Array.isArray(columns) ? columns.join('، ') : value
  } catch {
    return value
  }
}

function openDetails(auditId: number): void {
  selectedAudit.value = auditLogsById.get(auditId) ?? null
}

function closeDetails(): void {
  selectedAudit.value = null
}

watch(selectedAudit, (audit) => {
  document.body.classList.toggle('modal-open', audit !== null)
})

onBeforeUnmount(() => {
  activeRequest?.abort()
  document.body.classList.remove('modal-open')
})
</script>

<template>
  <div class="container-fluid users-page audit-logs-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item active"><span>گزارش فعالیت کاربران</span></li>
      </ol>
    </div>

    <div class="row">
      <div class="col-12">
        <div class="card">
          <div class="card-header">
            <div>
              <h4 class="card-title">
                <i class="las la-history mr-1"></i>
                گزارش فعالیت کاربران
              </h4>
              <p class="page-description mb-0 mt-1 text-muted">
                تاریخچه ایجاد، ویرایش و حذف اطلاعات ثبت‌شده در سامانه
              </p>
            </div>
          </div>

          <div class="card-body">
            <DataTableFilters
              v-model="appliedFilters"
              :definitions="filterDefinitions"
              :search="quickSearch"
              :loading="filtersLoading"
              search-placeholder="کاربر، جدول، شناسه یا عملیات..."
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
                <template #column-occurredAtUtc="{ cellData }">
                  <span class="text-nowrap">{{ formatDate(cellData) }}</span>
                </template>

                <template #column-userFullName="{ rowData }">
                  <strong class="text-black">{{ actorLabel(rowData) }}</strong>
                </template>

                <template #column-action="{ cellData }">
                  <span class="badge light" :class="actionClass(cellData)">
                    {{ actionLabel(cellData) }}
                  </span>
                </template>

                <template #column-tableName="{ cellData }">
                  <code dir="ltr">{{ cellData }}</code>
                </template>

                <template #column-primaryKey="{ cellData }">
                  <code class="audit-logs__compact-json" dir="ltr">{{ cellData }}</code>
                </template>

                <template #column-changedColumns="{ cellData }">
                  <span>{{ formatChangedColumns(cellData) }}</span>
                </template>

                <template #column-details="{ cellData }">
                  <button
                    class="btn btn-primary btn-xs users-table__role-action"
                    type="button"
                    title="مشاهده جزئیات"
                    aria-label="مشاهده جزئیات"
                    @click="openDetails(cellData)"
                  >
                    <i class="las la-eye"></i>
                  </button>
                </template>

                <thead>
                  <tr>
                    <th>ردیف</th>
                    <th>زمان رخداد</th>
                    <th>کاربر</th>
                    <th>عملیات</th>
                    <th>جدول</th>
                    <th>کلید رکورد</th>
                    <th>ستون‌های تغییرکرده</th>
                    <th>جزئیات</th>
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
    <template v-if="selectedAudit">
      <div
        class="modal show d-block users-role-modal audit-details-modal"
        tabindex="-1"
        role="dialog"
        aria-modal="true"
        aria-labelledby="audit-details-title"
        @click.self="closeDetails"
      >
        <div class="modal-dialog modal-dialog-centered modal-lg" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <div>
                <h5 id="audit-details-title" class="modal-title">جزئیات گزارش</h5>
                <p class="mb-0 mt-1 text-muted">
                  {{ selectedAudit.tableName }} — {{ formatDate(selectedAudit.occurredAtUtc) }}
                </p>
              </div>
              <button type="button" class="close" aria-label="بستن" @click="closeDetails">
                <span aria-hidden="true">&times;</span>
              </button>
            </div>

            <div class="modal-body">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <span class="text-muted d-block">کاربر</span>
                  <strong>{{ actorLabel(selectedAudit) }}</strong>
                </div>
                <div class="col-md-6 mb-3">
                  <span class="text-muted d-block">نوع عملیات</span>
                  <span class="badge light" :class="actionClass(selectedAudit.action)">
                    {{ actionLabel(selectedAudit.action) }}
                  </span>
                </div>
                <div class="col-12 mb-3">
                  <span class="text-muted d-block">کلید رکورد</span>
                  <pre dir="ltr">{{ formatJson(selectedAudit.primaryKey) }}</pre>
                </div>
                <div class="col-md-6 mb-3">
                  <span class="text-muted d-block">مقادیر قبلی</span>
                  <pre dir="ltr">{{ formatJson(selectedAudit.oldValues) }}</pre>
                </div>
                <div class="col-md-6 mb-3">
                  <span class="text-muted d-block">مقادیر جدید</span>
                  <pre dir="ltr">{{ formatJson(selectedAudit.newValues) }}</pre>
                </div>
                <div class="col-12">
                  <span class="text-muted d-block">ستون‌های تغییرکرده</span>
                  <div>{{ formatChangedColumns(selectedAudit.changedColumns) }}</div>
                </div>
              </div>
            </div>

            <div class="modal-footer">
              <button type="button" class="btn btn-light btn-sm" @click="closeDetails">
                بستن
              </button>
            </div>
          </div>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>
</template>
