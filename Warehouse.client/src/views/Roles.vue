<script setup lang="ts">
import DataTable from 'datatables.net-vue3'
import DataTablesCore from 'datatables.net-bs4'
import type { AjaxCallback, AjaxData, ColumnsConfig, Config } from 'datatables.net'
import { onBeforeUnmount, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import DataTableScrollContainer from '@/components/DataTableScrollContainer.vue'
import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import { getSession } from '@/services/auth'
import { createRole, getRoles, RolesApiError } from '@/services/roles'
import { toast } from '@/services/toast'
import type {
  DataTableFilterDefinition,
  DataTableFilterValues,
  NumericFilterValue,
} from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'

defineOptions({ name: 'RolesView' })

DataTable.use(DataTablesCore)

const router = useRouter()
const table = ref<{
  dt: { ajax: { reload: (callback: null, resetPaging: boolean) => unknown } }
} | null>(null)
const quickSearch = ref('')
const appliedFilters = ref<DataTableFilterValues>({})
const filtersLoading = ref(false)
const canManageRolePolicies =
  getSession()?.permissions.includes('RolePoliciesManage') ?? false
const canManageRoles = getSession()?.permissions.includes('RolesManage') ?? false
const createModalOpen = ref(false)
const newRoleName = ref('')
const createRoleError = ref('')
const roleSaving = ref(false)
let activeRequest: AbortController | null = null
let roleOperationRequest: AbortController | null = null
let latestDraw = 0

const filterDefinitions: DataTableFilterDefinition[] = [
  {
    key: 'roleName',
    label: 'نام نقش',
    type: 'text',
    placeholder: 'نام نقش را وارد کنید',
    maxlength: 256,
  },
  {
    key: 'userCount',
    label: 'تعداد کاربران',
    type: 'numeric-comparison',
    numericType: 'integer',
    min: 0,
  },
  {
    key: 'permissionCount',
    label: 'تعداد دسترسی‌ها',
    type: 'numeric-comparison',
    numericType: 'integer',
    min: 0,
  },
]

const columns: ColumnsConfig[] = [
  createRowNumberColumn(),
  { data: 'name', name: 'name', title: 'نام نقش', className: 'all' },
  {
    data: 'userCount',
    name: 'userCount',
    title: 'تعداد کاربران',
    className: 'min-tablet-l',
  },
  {
    data: 'permissionCount',
    name: 'permissionCount',
    title: 'تعداد دسترسی‌ها',
    className: 'min-tablet-l',
  },
]

if (canManageRolePolicies) {
  columns.push({
    data: 'name',
    name: 'permissions',
    title: 'مدیریت دسترسی‌ها',
    className: 'min-tablet-l',
    orderable: false,
    searchable: false,
  })
}

const options: Config = {
  ajax: loadRoles,
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
    emptyTable: 'نقشی برای نمایش وجود ندارد.',
    info: '_START_–_END_ از _TOTAL_',
    infoEmpty: '۰ از ۰',
    infoFiltered: '',
    lengthMenu: '_MENU_',
    loadingRecords: 'در حال دریافت نقش‌ها...',
    processing: 'در حال پردازش...',
    search: 'جستجو:',
    searchPlaceholder: 'نام نقش',
    zeroRecords: 'نقشی با این مشخصات پیدا نشد.',
    paginate: {
      first: '<<',
      last: '>>',
      next: '>',
      previous: '<',
    },
  },
}

async function loadRoles(request: AjaxData, callback: AjaxCallback): Promise<void> {
  activeRequest?.abort()
  const controller = new AbortController()
  activeRequest = controller
  filtersLoading.value = true

  const draw = request.draw ?? 0
  latestDraw = Math.max(latestDraw, draw)
  const start = request.start ?? 0
  const pageSize = request.length && request.length > 0 ? request.length : 10
  const orderedColumn = request.order?.[0]
  const columnName = orderedColumn
    ? request.columns?.[orderedColumn.column]?.name
    : undefined
  const sortBy = typeof columnName === 'string' ? columnName : undefined

  try {
    const result = await getRoles({
      paginationEnabled: true,
      pageNumber: Math.floor(start / pageSize) + 1,
      pageSize,
      search: quickSearch.value || undefined,
      roleName: stringFilter('roleName'),
      userCount: numericFilter('userCount'),
      permissionCount: numericFilter('permissionCount'),
      sortBy,
      sortDescending: orderedColumn?.dir === 'desc',
      signal: controller.signal,
    })

    if (draw < latestDraw) return
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

    if (error instanceof RolesApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: '/dashboard/roles' } })
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

function numericFilter(key: string): NumericFilterValue | null {
  const value = appliedFilters.value[key]
  return value && typeof value === 'object' && !Array.isArray(value) && 'operator' in value
    ? value as NumericFilterValue
    : null
}

function reloadRoles(resetPaging: boolean): void {
  table.value?.dt.ajax.reload(null, resetPaging)
}

function handleSearch(value: string): void {
  quickSearch.value = value
  reloadRoles(true)
}

function handleApply(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadRoles(true)
}

function handleClear(): void {
  quickSearch.value = ''
  appliedFilters.value = {}
  reloadRoles(true)
}

function handleRemove(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadRoles(true)
}

async function openRolePermissions(roleName: string): Promise<void> {
  await router.push({ name: 'role-permissions', params: { roleName } })
}

function openCreateRoleModal(): void {
  newRoleName.value = ''
  createRoleError.value = ''
  createModalOpen.value = true
}

function closeCreateRoleModal(): void {
  if (roleSaving.value) return
  roleOperationRequest?.abort()
  createModalOpen.value = false
  createRoleError.value = ''
}

async function saveRole(): Promise<void> {
  const name = newRoleName.value.trim()
  if (!name) {
    createRoleError.value = 'وارد کردن نام نقش الزامی است.'
    return
  }

  roleOperationRequest?.abort()
  roleOperationRequest = new AbortController()
  roleSaving.value = true
  createRoleError.value = ''

  try {
    await createRole(name, roleOperationRequest.signal)
    createModalOpen.value = false
    newRoleName.value = ''
    reloadRoles(false)
    toast.success(`نقش «${name}» با موفقیت افزوده شد.`)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (error instanceof RolesApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: '/dashboard/roles' } })
      return
    }
    toast.error(error instanceof Error ? error.message : 'افزودن نقش انجام نشد.')
  } finally {
    roleSaving.value = false
  }
}

watch(createModalOpen, (isOpen) => {
  document.body.classList.toggle('modal-open', isOpen)
})

onBeforeUnmount(() => {
  activeRequest?.abort()
  roleOperationRequest?.abort()
  document.body.classList.remove('modal-open')
})
</script>

<template>
  <div class="container-fluid users-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item active"><span>لیست نقش‌ها</span></li>
      </ol>
    </div>

    <div class="row">
      <div class="col-12">
        <div class="card">
          <div class="card-header">
            <div>
              <h4 class="card-title">
                <i class="flaticon-381-id-card mr-1"></i>
                لیست نقش‌ها
              </h4>
              <p class="page-description mb-0 mt-1 text-muted">
                نقش‌های تعریف‌شده در سامانه و مجوزهای تخصیص‌یافته
              </p>
            </div>
            <button
              v-if="canManageRoles"
              class="btn btn-primary btn-sm mobile-icon-button"
              type="button"
              title="افزودن نقش"
              aria-label="افزودن نقش"
              @click="openCreateRoleModal"
            >
              <i class="las la-plus ml-1"></i>
              <span class="mobile-button-label">افزودن نقش</span>
            </button>
          </div>

          <div class="card-body">
            <DataTableFilters
              v-model="appliedFilters"
              :definitions="filterDefinitions"
              :search="quickSearch"
              :loading="filtersLoading"
              search-placeholder="جستجو در نقش‌ها..."
              @update:search="handleSearch"
              @apply="handleApply"
              @clear="handleClear"
              @remove="handleRemove"
            />

            <DataTableScrollContainer class="users-table-container">
              <DataTable
                ref="table"
                :options="options"
                class="display responsive nowrap table table-striped table-sm users-table"
              >
                <template #column-name="{ cellData }">
                  <div class="d-flex align-items-center">
                    <span class="badge light badge-primary ml-2">
                      <i class="flaticon-381-id-card"></i>
                    </span>
                    <strong class="text-black">{{ cellData }}</strong>
                  </div>
                </template>

                <template #column-userCount="{ cellData }">
                  <span class="badge light badge-info">{{ cellData }}</span>
                </template>

                <template #column-permissionCount="{ cellData }">
                  <span class="badge light badge-primary">{{ cellData }}</span>
                </template>

                <template v-if="canManageRolePolicies" #column-permissions="{ cellData }">
                  <button
                    class="btn btn-primary btn-xs roles-table__manage-action"
                    type="button"
                    title="مدیریت نقش"
                    aria-label="مدیریت نقش"
                    @click="openRolePermissions(cellData)"
                  >
                    <i class="las la-user-shield" aria-hidden="true"></i>
                  </button>
                </template>

                <thead>
                  <tr>
                    <th>ردیف</th>
                    <th>نام نقش</th>
                    <th>تعداد کاربران</th>
                    <th>تعداد دسترسی‌ها</th>
                    <th v-if="canManageRolePolicies">مدیریت دسترسی‌ها</th>
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
    <template v-if="createModalOpen">
      <div
        class="modal show d-block users-role-modal"
        tabindex="-1"
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-role-modal-title"
        @click.self="closeCreateRoleModal"
      >
        <div class="modal-dialog modal-dialog-centered" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <h5 id="create-role-modal-title" class="modal-title">افزودن نقش جدید</h5>
              <button
                type="button"
                class="close"
                aria-label="بستن"
                :disabled="roleSaving"
                @click="closeCreateRoleModal"
              >
                <span aria-hidden="true">&times;</span>
              </button>
            </div>

            <form @submit.prevent="saveRole">
              <div class="modal-body">
                <div v-if="createRoleError" class="alert alert-danger" role="alert">
                  <i class="las la-exclamation-circle ml-2"></i>{{ createRoleError }}
                </div>
                <div class="form-group mb-0">
                  <label for="new-role-name">نام نقش</label>
                  <input
                    id="new-role-name"
                    v-model.trim="newRoleName"
                    class="form-control"
                    type="text"
                    maxlength="256"
                    autocomplete="off"
                    required
                    :disabled="roleSaving"
                  />
                  <small class="form-text text-muted">
                    پس از ایجاد نقش می‌توانید دسترسی‌های آن را از جدول مدیریت کنید.
                  </small>
                </div>
              </div>
              <div class="modal-footer modal-footer--split">
                <button
                  type="button"
                  class="btn btn-light btn-sm"
                  :disabled="roleSaving"
                  @click="closeCreateRoleModal"
                >
                  انصراف
                </button>
                <button class="btn btn-primary btn-sm" type="submit" :disabled="roleSaving">
                  <span
                    v-if="roleSaving"
                    class="spinner-border spinner-border-sm ml-2"
                    role="status"></span>
                  افزودن نقش
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
