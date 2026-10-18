<script setup lang="ts">
import DataTable from 'datatables.net-vue3'
import DataTablesCore from 'datatables.net-bs4'
import type { AjaxCallback, AjaxData, ColumnsConfig, Config } from 'datatables.net'
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import ConfirmDialog from '@/components/ConfirmDialog.vue'
import DataTableScrollContainer from '@/components/DataTableScrollContainer.vue'
import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import { getSession } from '@/services/auth'
import { getRoles, RolesApiError, type RoleListItem } from '@/services/roles'
import { toast } from '@/services/toast'
import {
  changeUserPassword,
  createManagedUser,
  getUsers,
  setUserActiveStatus,
  setUserRoles,
  updateUser,
  UsersApiError,
  type UserListItem,
} from '@/services/users'
import type {
  DataTableFilterDefinition,
  DataTableFilterValues,
  FilterOption,
} from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'

defineOptions({ name: 'UsersView' })

DataTable.use(DataTablesCore)

const router = useRouter()
const defaultUserAvatar = '/mophy/images/user-icon.png'
type ConfirmDialogInstance = {
  ask(options: {
    title: string
    text?: string
    icon?: 'success' | 'error' | 'warning' | 'info' | 'question'
    confirmButtonText?: string
    cancelButtonText?: string
    confirmButtonColor?: string
  }): Promise<boolean>
}

const confirmDialog = ref<ConfirmDialogInstance | null>(null)
const table = ref<{
  dt: { ajax: { reload: (callback: null, resetPaging: boolean) => unknown } }
} | null>(null)
const quickSearch = ref('')
const appliedFilters = ref<DataTableFilterValues>({})
const filtersLoading = ref(false)
const usersById = new Map<string, UserListItem>()
const canManageUserRoles = getSession()?.permissions.includes('UserRolesManage') ?? false
const canManageUsers = getSession()?.permissions.includes('UsersManage') ?? false
const canManageAnyUserSetting = canManageUserRoles || canManageUsers
const canCreateUsers = canManageUsers && canManageUserRoles

const userModalOpen = ref(false)
const userModalMode = ref<'create' | 'edit' | 'password'>('edit')
const userModalError = ref('')
const userSaving = ref(false)
const statusSavingUserId = ref('')
const editFullName = ref('')
const editPhoneNumber = ref('')
const editEmail = ref('')
const editUserName = ref('')
const newPassword = ref('')
const confirmNewPassword = ref('')
const createIsActive = ref(false)
const createRoleNames = ref<string[]>([])

const roleModalOpen = ref(false)
const selectedUser = ref<UserListItem | null>(null)
const availableRoles = ref<RoleListItem[]>([])
const selectedRoleNames = ref<string[]>([])
const roleSearch = ref('')
const roleModalError = ref('')
const rolesLoading = ref(false)
const rolesSaving = ref(false)

let activeRequest: AbortController | null = null
let rolesRequest: AbortController | null = null
let saveRolesRequest: AbortController | null = null
let userOperationRequest: AbortController | null = null
let latestDraw = 0

const filterDefinitions: DataTableFilterDefinition[] = [
  {
    key: 'fullName',
    label: 'نام و نام خانوادگی',
    type: 'text',
    placeholder: 'نام را وارد کنید',
    maxlength: 200,
  },
  {
    key: 'userName',
    label: 'نام کاربری',
    type: 'text',
    placeholder: 'نام کاربری را وارد کنید',
    maxlength: 256,
  },
  {
    key: 'email',
    label: 'ایمیل',
    type: 'text',
    placeholder: 'ایمیل را وارد کنید',
    maxlength: 256,
  },
  {
    key: 'isActive',
    label: 'وضعیت حساب',
    type: 'boolean',
    trueLabel: 'فعال',
    falseLabel: 'غیرفعال',
  },
  {
    key: 'roleName',
    label: 'نقش',
    type: 'select',
    placeholder: 'همه نقش‌ها',
    loadOptions: loadRoleFilterOptions,
  },
]

const filteredRoles = computed(() => {
  const search = roleSearch.value.trim().toLocaleLowerCase()
  if (!search) return availableRoles.value

  return availableRoles.value.filter((role) =>
    role.name.toLocaleLowerCase().includes(search),
  )
})

const columns: ColumnsConfig[] = [
  createRowNumberColumn(),
  { data: 'fullName', name: 'fullName', title: 'نام کامل', className: 'all' },
  { data: 'userName', name: 'userName', title: 'نام کاربری', className: 'min-tablet-l' },
  { data: 'email', name: 'email', title: 'ایمیل', className: 'min-tablet-l' },
  { data: 'phoneNumber', name: 'phoneNumber', title: 'شماره تلفن', className: 'min-tablet-l' },
  {
    data: 'roles',
    name: 'roles',
    title: 'نقش‌ها',
    className: 'min-tablet-l',
    orderable: false,
    searchable: false,
  },
  {
    data: 'isActive',
    name: 'isActive',
    title: 'وضعیت حساب',
    className: 'min-tablet-l',
    searchable: false,
  },
  {
    data: 'createdAtUtc',
    name: 'createdAtUtc',
    title: 'تاریخ ایجاد',
    className: 'min-tablet-l',
  },
]

if (canManageAnyUserSetting) {
  columns.push({
    data: 'id',
    name: 'actions',
    title: 'عملیات',
    className: 'min-tablet-l',
    orderable: false,
    searchable: false,
  })
}

const options: Config = {
  ajax: loadUsers,
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
    emptyTable: 'کاربری برای نمایش وجود ندارد.',
    info: '_START_–_END_ از _TOTAL_',
    infoEmpty: '۰ از ۰',
    infoFiltered: '',
    lengthMenu: '_MENU_',
    loadingRecords: 'در حال دریافت کاربران...',
    processing: 'در حال پردازش...',
    search: 'جستجو:',
    searchPlaceholder: 'نام، تلفن یا ایمیل',
    zeroRecords: 'کاربری با این مشخصات پیدا نشد.',
    paginate: {
      first: '<<',
      last: '>>',
      next: '>',
      previous: '<',
    },
  },
}

async function loadUsers(
  request: AjaxData,
  callback: AjaxCallback,
): Promise<void> {
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
    const result = await getUsers({
      paginationEnabled: true,
      pageNumber: Math.floor(start / pageSize) + 1,
      pageSize,
      search: quickSearch.value || undefined,
      fullName: stringFilter('fullName'),
      userName: stringFilter('userName'),
      email: stringFilter('email'),
      isActive: booleanFilter('isActive'),
      roleName: stringFilter('roleName'),
      sortBy,
      sortDescending: orderedColumn?.dir === 'desc',
      signal: controller.signal,
    })

    if (draw < latestDraw) return
    usersById.clear()
    for (const user of result.items) usersById.set(user.id, user)

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

    if (error instanceof UsersApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: '/dashboard/users' } })
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

function booleanFilter(key: string): boolean | undefined {
  const value = appliedFilters.value[key]
  return typeof value === 'boolean' ? value : undefined
}

function reloadUsers(resetPaging: boolean): void {
  table.value?.dt.ajax.reload(null, resetPaging)
}

function handleSearch(value: string): void {
  quickSearch.value = value
  reloadUsers(true)
}

function handleApply(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadUsers(true)
}

function handleClear(): void {
  quickSearch.value = ''
  appliedFilters.value = {}
  reloadUsers(true)
}

function handleRemove(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadUsers(true)
}

async function loadRoleFilterOptions(): Promise<FilterOption[]> {
  try {
    const result = await getRoles()
    return result.items.map((role) => ({ value: role.name, label: role.name }))
  } catch (error) {
    if (error instanceof RolesApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: '/dashboard/users' } })
      return []
    }

    toast.error(error instanceof Error ? error.message : 'دریافت نقش‌ها انجام نشد.')
    return []
  }
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('fa-IR', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function getUserProfileImageUrl(user: UserListItem): string {
  if (!user.profileImage || !user.profileImageContentType) return defaultUserAvatar
  return `data:${user.profileImageContentType};base64,${user.profileImage}`
}

function refreshUsers(): void {
  reloadUsers(false)
}

async function openCreateUserModal(): Promise<void> {
  selectedUser.value = null
  editFullName.value = ''
  editPhoneNumber.value = ''
  editEmail.value = ''
  editUserName.value = ''
  newPassword.value = ''
  confirmNewPassword.value = ''
  createIsActive.value = false
  createRoleNames.value = []
  availableRoles.value = []
  userModalMode.value = 'create'
  userModalError.value = ''
  userModalOpen.value = true

  rolesRequest?.abort()
  rolesRequest = new AbortController()
  rolesLoading.value = true
  try {
    const result = await getRoles({ signal: rolesRequest.signal })
    availableRoles.value = result.items
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (error instanceof RolesApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: '/dashboard/users' } })
      return
    }
    toast.error(error instanceof Error ? error.message : 'دریافت نقش‌ها انجام نشد.')
  } finally {
    rolesLoading.value = false
  }
}

function openEditModal(userId: string): void {
  const user = usersById.get(userId)
  if (!user) return

  selectedUser.value = user
  editFullName.value = user.fullName
  editPhoneNumber.value = user.phoneNumber
  editEmail.value = user.email
  editUserName.value = user.userName
  userModalMode.value = 'edit'
  userModalError.value = ''
  userModalOpen.value = true
}

function openPasswordModal(userId: string): void {
  const user = usersById.get(userId)
  if (!user) return

  selectedUser.value = user
  newPassword.value = ''
  confirmNewPassword.value = ''
  userModalMode.value = 'password'
  userModalError.value = ''
  userModalOpen.value = true
}

function closeUserModal(): void {
  if (userSaving.value) return

  userOperationRequest?.abort()
  rolesRequest?.abort()
  userModalOpen.value = false
  userModalError.value = ''
  selectedUser.value = null
  newPassword.value = ''
  confirmNewPassword.value = ''
  createIsActive.value = false
  createRoleNames.value = []
}

async function saveManagedUser(): Promise<void> {
  if (
    !editFullName.value ||
    !editPhoneNumber.value ||
    !editEmail.value ||
    !editUserName.value
  ) {
    userModalError.value = 'تکمیل همه فیلدهای اطلاعات کاربر الزامی است.'
    return
  }
  if (newPassword.value.length < 8) {
    userModalError.value = 'رمز عبور باید حداقل ۸ نویسه باشد.'
    return
  }
  if (newPassword.value !== confirmNewPassword.value) {
    userModalError.value = 'تکرار رمز عبور با رمز عبور یکسان نیست.'
    return
  }

  userOperationRequest?.abort()
  userOperationRequest = new AbortController()
  userSaving.value = true
  userModalError.value = ''

  try {
    await createManagedUser(
      {
        fullName: editFullName.value,
        phoneNumber: editPhoneNumber.value,
        email: editEmail.value,
        userName: editUserName.value,
        password: newPassword.value,
        isActive: createIsActive.value,
        roleNames: createRoleNames.value,
      },
      userOperationRequest.signal,
    )
    userModalOpen.value = false
    refreshUsers()
    toast.success('کاربر جدید با موفقیت افزوده شد.')
  } catch (error) {
    await handleUserOperationError(error, 'افزودن کاربر انجام نشد.')
  } finally {
    userSaving.value = false
  }
}

async function submitUserModal(): Promise<void> {
  if (userModalMode.value === 'create') {
    await saveManagedUser()
  } else if (userModalMode.value === 'edit') {
    await saveUserInformation()
  } else {
    await saveUserPassword()
  }
}

async function saveUserInformation(): Promise<void> {
  if (!selectedUser.value) return

  if (!editFullName.value || !editPhoneNumber.value || !editEmail.value || !editUserName.value) {
    userModalError.value = 'تکمیل همه فیلدهای اطلاعات کاربر الزامی است.'
    return
  }

  userOperationRequest?.abort()
  userOperationRequest = new AbortController()
  userSaving.value = true
  userModalError.value = ''

  try {
    await updateUser(
      selectedUser.value.id,
      {
        fullName: editFullName.value,
        phoneNumber: editPhoneNumber.value,
        email: editEmail.value,
        userName: editUserName.value,
      },
      userOperationRequest.signal,
    )
    userModalOpen.value = false
    selectedUser.value = null
    refreshUsers()
    toast.success('اطلاعات کاربر با موفقیت ویرایش شد.')
  } catch (error) {
    await handleUserOperationError(error, 'ویرایش اطلاعات کاربر انجام نشد.')
  } finally {
    userSaving.value = false
  }
}

async function saveUserPassword(): Promise<void> {
  if (!selectedUser.value) return

  if (newPassword.value.length < 8) {
    userModalError.value = 'رمز عبور باید حداقل ۸ نویسه باشد.'
    return
  }
  if (newPassword.value !== confirmNewPassword.value) {
    userModalError.value = 'تکرار رمز عبور با رمز عبور جدید یکسان نیست.'
    return
  }

  userOperationRequest?.abort()
  userOperationRequest = new AbortController()
  userSaving.value = true
  userModalError.value = ''

  try {
    await changeUserPassword(
      selectedUser.value.id,
      newPassword.value,
      userOperationRequest.signal,
    )
    userModalOpen.value = false
    selectedUser.value = null
    toast.success('رمز عبور کاربر با موفقیت تغییر کرد.')
  } catch (error) {
    await handleUserOperationError(error, 'تغییر رمز عبور کاربر انجام نشد.')
  } finally {
    userSaving.value = false
  }
}

async function toggleUserActiveStatus(userId: string): Promise<void> {
  const user = usersById.get(userId)
  if (!user || statusSavingUserId.value) return

  const nextStatus = !user.isActive
  const action = nextStatus ? 'فعال' : 'غیرفعال'
  const confirmed = await confirmDialog.value?.ask({
    title: `${action}‌سازی حساب کاربری`,
    text: `آیا از ${action}‌سازی حساب ${user.fullName || user.userName} مطمئن هستید؟`,
    icon: nextStatus ? 'question' : 'warning',
    confirmButtonText: nextStatus ? 'فعال شود' : 'غیرفعال شود',
    confirmButtonColor: nextStatus ? '#28a745' : '#ffb800',
  })

  if (!confirmed) {
    return
  }

  statusSavingUserId.value = userId
  try {
    await setUserActiveStatus(userId, nextStatus)
    refreshUsers()
    toast.success(`حساب کاربر با موفقیت ${action} شد.`)
  } catch (error) {
    await handleUserOperationError(error, 'تغییر وضعیت حساب کاربر انجام نشد.')
  } finally {
    statusSavingUserId.value = ''
  }
}

async function handleUserOperationError(
  error: unknown,
  fallback: string,
): Promise<void> {
  if (error instanceof DOMException && error.name === 'AbortError') return

  if (error instanceof UsersApiError && error.status === 401) {
    await router.replace({ name: 'login', query: { redirect: '/dashboard/users' } })
    return
  }

  const message = error instanceof Error ? error.message : fallback
  toast.error(message)
}

async function openRoleModal(userId: string): Promise<void> {
  const user = usersById.get(userId)
  if (!user) return

  selectedUser.value = user
  selectedRoleNames.value = [...user.roles]
  roleSearch.value = ''
  roleModalError.value = ''
  roleModalOpen.value = true

  rolesRequest?.abort()
  rolesRequest = new AbortController()
  rolesLoading.value = true

  try {
    const result = await getRoles({ signal: rolesRequest.signal })
    availableRoles.value = result.items
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return

    if (error instanceof RolesApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: '/dashboard/users' } })
      return
    }

    toast.error(error instanceof Error ? error.message : 'دریافت نقش‌ها انجام نشد.')
  } finally {
    rolesLoading.value = false
  }
}

function closeRoleModal(): void {
  if (rolesSaving.value) return

  rolesRequest?.abort()
  roleModalOpen.value = false
  roleModalError.value = ''
  selectedUser.value = null
  selectedRoleNames.value = []
  roleSearch.value = ''
}

async function saveUserRoles(): Promise<void> {
  if (!selectedUser.value || rolesLoading.value) return

  saveRolesRequest?.abort()
  saveRolesRequest = new AbortController()
  rolesSaving.value = true
  roleModalError.value = ''

  try {
    await setUserRoles(
      selectedUser.value.id,
      selectedRoleNames.value,
      saveRolesRequest.signal,
    )

    roleModalOpen.value = false
    selectedUser.value = null
    selectedRoleNames.value = []
    roleSearch.value = ''
    refreshUsers()
    toast.success('نقش‌های کاربر با موفقیت ذخیره شدند.')
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return

    if (error instanceof UsersApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: '/dashboard/users' } })
      return
    }

    toast.error(error instanceof Error ? error.message : 'ذخیره نقش‌های کاربر انجام نشد.')
  } finally {
    rolesSaving.value = false
  }
}

watch([roleModalOpen, userModalOpen], ([isRoleOpen, isUserOpen]) => {
  document.body.classList.toggle('modal-open', isRoleOpen || isUserOpen)
})

onBeforeUnmount(() => {
  activeRequest?.abort()
  rolesRequest?.abort()
  saveRolesRequest?.abort()
  userOperationRequest?.abort()
  document.body.classList.remove('modal-open')
})
</script>

<template>
  <div class="container-fluid users-page">
    <ConfirmDialog ref="confirmDialog" />

    <div class="page-titles">
      <!--<h4>کاربران</h4>-->
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item active"><span>فهرست کاربران</span></li>
      </ol>
    </div>

    <div class="row">
      <div class="col-12">
        <div class="card">
          <div class="card-header">
            <div>             
              <h4 class="card-title">
                <i class="flaticon-381-user-7  mr-1"></i>
                فهرست کاربران
              </h4>
              <p class="page-description mb-0 mt-1 text-muted">کاربران ثبت‌شده در سامانه و نقش‌های تخصیص‌یافته</p>
            </div>
            <button
              v-if="canCreateUsers"
              class="btn btn-primary btn-sm mobile-icon-button"
              type="button"
              title="افزودن کاربر"
              aria-label="افزودن کاربر"
              @click="openCreateUserModal"
            >
              <i class="las la-user-plus ml-1"></i>
              <span class="mobile-button-label">افزودن کاربر</span>
            </button>
          </div>
          <div class="card-body">
            <DataTableFilters
              v-model="appliedFilters"
              :definitions="filterDefinitions"
              :search="quickSearch"
              :loading="filtersLoading"
              search-placeholder="نام، ایمیل، نام کاربری یا شماره تلفن..."
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
                <template #column-fullName="{ cellData, rowData }">
                  <div class="d-flex align-items-center">
                    <img
                      class="rounded-circle ml-2 users-table__avatar"
                      :src="getUserProfileImageUrl(rowData)"
                      :alt="cellData ? `تصویر ${cellData}` : 'تصویر کاربر'"
                    />
                    <strong class="text-black">{{ cellData || '—' }}</strong>
                  </div>
                </template>

                <template #column-email="{ cellData }">
                  <span dir="ltr" class="d-inline-block">{{ cellData }}</span>
                </template>

                <template #column-phoneNumber="{ cellData }">
                  <span dir="ltr" class="d-inline-block">{{ cellData || '—' }}</span>
                </template>

                <template #column-roles="{ cellData }">
                  <div class="users-table__roles">
                    <span v-for="role in cellData" :key="role" class="badge light badge-primary">{{ role }}</span>
                    <span v-if="!cellData.length" class="text-muted">بدون نقش</span>
                  </div>
                </template>

                <template #column-isActive="{ cellData }">
                  <span :class="cellData ? 'badge-success' : 'badge-warning'" class="badge light">
                    {{ cellData ? 'فعال' : 'در انتظار تأیید' }}
                  </span>
                </template>

                <template #column-createdAtUtc="{ cellData }">
                  <span class="text-nowrap">{{ formatDate(cellData) }}</span>
                </template>

                <template #column-actions="{ cellData }">
                  <div class="users-table__actions">
                    <button
                      v-if="canManageUsers"
                      class="btn btn-info btn-xs users-table__role-action"
                      type="button"
                      title="ویرایش اطلاعات کاربر"
                      aria-label="ویرایش اطلاعات کاربر"
                      @click="openEditModal(cellData)"
                    >
                      <i class="las la-user-edit"></i>
                    </button>
                    <button
                      v-if="canManageUsers"
                      class="btn btn-secondary btn-xs users-table__role-action"
                      type="button"
                      title="تغییر رمز عبور"
                      aria-label="تغییر رمز عبور"
                      @click="openPasswordModal(cellData)"
                    >
                      <i class="las la-key"></i>
                    </button>
                    <button
                      v-if="canManageUsers"
                      :class="usersById.get(cellData)?.isActive ? 'btn-warning' : 'btn-success'"
                      class="btn btn-xs users-table__role-action"
                      type="button"
                      :disabled="statusSavingUserId === cellData"
                      :title="usersById.get(cellData)?.isActive ? 'غیرفعال‌سازی حساب' : 'فعال‌سازی حساب'"
                      :aria-label="usersById.get(cellData)?.isActive ? 'غیرفعال‌سازی حساب' : 'فعال‌سازی حساب'"
                      @click="toggleUserActiveStatus(cellData)"
                    >
                      <span v-if="statusSavingUserId === cellData" class="spinner-border spinner-border-sm"></span>
                      <i v-else :class="usersById.get(cellData)?.isActive ? 'las la-user-slash' : 'las la-user-check'"></i>
                    </button>
                    <button
                      v-if="canManageUserRoles"
                      class="btn btn-primary btn-xs users-table__role-action"
                      type="button"
                      title="ویرایش نقش‌های کاربر"
                      aria-label="ویرایش نقش‌های کاربر"
                      @click="openRoleModal(cellData)"
                    >
                      <i class="flaticon-381-id-card"></i>
                    </button>
                  </div>
                </template>

                <thead>
                  <tr>
                    <th>ردیف</th>
                    <th>نام کامل</th>
                    <th>نام کاربری</th>
                    <th>ایمیل</th>
                    <th>شماره تلفن</th>
                    <th>نقش‌ها</th>
                    <th>وضعیت حساب</th>
                    <th>تاریخ ایجاد</th>
                    <th v-if="canManageAnyUserSetting">عملیات</th>
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
    <template v-if="roleModalOpen">
      <div
        class="modal show d-block users-role-modal"
        tabindex="-1"
        role="dialog"
        aria-modal="true"
        aria-labelledby="users-role-modal-title"
        @click.self="closeRoleModal"
      >
        <div
          class="modal-dialog modal-dialog-centered"
          :class="{ 'modal-lg': userModalMode === 'create' }"
          role="document"
        >
          <div class="modal-content">
            <div class="modal-header">
              <div>
                <h5 id="users-role-modal-title" class="modal-title">نقش‌های کاربر</h5>
                <p v-if="selectedUser" class="mb-0 mt-1 text-muted">
                  {{ selectedUser.userName }}
                  <span dir="ltr" class="d-inline-block">({{ selectedUser.email }})</span>
                </p>
              </div>
              <button
                type="button"
                class="close"
                aria-label="بستن"
                :disabled="rolesSaving"
                @click="closeRoleModal"
              >
                <span aria-hidden="true">&times;</span>
              </button>
            </div>

            <div class="modal-body">
              <div v-if="roleModalError" class="alert alert-danger" role="alert">
                <i class="las la-exclamation-circle ml-2"></i>{{ roleModalError }}
              </div>

              <div
                v-if="!rolesLoading && availableRoles.length"
                class="users-role-modal__search"
              >
                <i class="las la-search" aria-hidden="true"></i>
                <input
                  v-model="roleSearch"
                  type="search"
                  class="form-control form-control-sm"
                  placeholder="جستجوی نقش..."
                  aria-label="جستجوی نقش"
                  :disabled="rolesSaving"
                />
              </div>

              <div v-if="rolesLoading" class="py-4 text-center text-muted">
                <span class="spinner-border spinner-border-sm ml-2" role="status"></span>
                در حال دریافت نقش‌ها...
              </div>

              <div v-else-if="filteredRoles.length" class="users-role-modal__list">
                <div
                  v-for="role in filteredRoles"
                  :key="role.id"
                  class="custom-control custom-checkbox users-role-option"
                >
                  <input
                    :id="`user-role-${role.id}`"
                    v-model="selectedRoleNames"
                    :value="role.name"
                    type="checkbox"
                    class="custom-control-input"
                    :disabled="rolesSaving"
                  />
                  <label class="custom-control-label" :for="`user-role-${role.id}`">
                    <strong>{{ role.name }}</strong>
                  </label>
                </div>
              </div>

              <p
                v-else-if="availableRoles.length && !roleModalError"
                class="mb-0 text-muted"
              >
                نقشی مطابق عبارت جستجو پیدا نشد.
              </p>

              <p v-else-if="!roleModalError" class="mb-0 text-muted">
                نقشی برای انتخاب وجود ندارد.
              </p>
            </div>

            <div class="modal-footer modal-footer--split">
              <button
                type="button"
                class="btn btn-light btn-sm"
                :disabled="rolesSaving"
                @click="closeRoleModal"
              >
                انصراف
              </button>
              <button
                type="button"
                class="btn btn-primary btn-sm"
                :disabled="rolesLoading || rolesSaving || !!roleModalError"
                @click="saveUserRoles"
              >
                <span
                  v-if="rolesSaving"
                  class="spinner-border spinner-border-sm ml-2"
                  role="status"
                ></span>
                ذخیره نقش‌ها
              </button>
            </div>
          </div>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>

    <template v-if="userModalOpen">
      <div
        class="modal show d-block users-role-modal"
        tabindex="-1"
        role="dialog"
        aria-modal="true"
        aria-labelledby="users-management-modal-title"
        @click.self="closeUserModal"
      >
        <div class="modal-dialog modal-dialog-centered" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <div>
                <h5 id="users-management-modal-title" class="modal-title">
                  {{
                    userModalMode === 'create'
                      ? 'افزودن کاربر جدید'
                      : userModalMode === 'edit'
                        ? 'ویرایش اطلاعات کاربر'
                        : 'تغییر رمز عبور'
                  }}
                </h5>
                <p v-if="selectedUser" class="mb-0 mt-1 text-muted">
                  {{ selectedUser.fullName || selectedUser.userName }}
                  <span dir="ltr" class="d-inline-block">({{ selectedUser.email }})</span>
                </p>
              </div>
              <button
                type="button"
                class="close"
                aria-label="بستن"
                :disabled="userSaving"
                @click="closeUserModal"
              >
                <span aria-hidden="true">&times;</span>
              </button>
            </div>

            <form
              @submit.prevent="submitUserModal"
            >
              <div class="modal-body">
                <div v-if="userModalError" class="alert alert-danger" role="alert">
                  <i class="las la-exclamation-circle ml-2"></i>{{ userModalError }}
                </div>

                <template v-if="userModalMode === 'create'">
                  <div class="row">
                    <div class="col-md-6">
                      <div class="form-group">
                        <label for="create-user-full-name">نام و نام خانوادگی</label>
                        <input
                          id="create-user-full-name"
                          v-model.trim="editFullName"
                          type="text"
                          class="form-control"
                          maxlength="200"
                          required
                          :disabled="userSaving"
                        />
                      </div>
                    </div>
                    <div class="col-md-6">
                      <div class="form-group">
                        <label for="create-user-phone">شماره تلفن</label>
                        <input
                          id="create-user-phone"
                          v-model.trim="editPhoneNumber"
                          type="tel"
                          dir="ltr"
                          class="form-control"
                          maxlength="32"
                          required
                          :disabled="userSaving"
                        />
                      </div>
                    </div>
                    <div class="col-md-6">
                      <div class="form-group">
                        <label for="create-user-email">ایمیل</label>
                        <input
                          id="create-user-email"
                          v-model.trim="editEmail"
                          type="email"
                          dir="ltr"
                          class="form-control"
                          maxlength="256"
                          required
                          :disabled="userSaving"
                        />
                      </div>
                    </div>
                    <div class="col-md-6">
                      <div class="form-group">
                        <label for="create-user-name">نام کاربری</label>
                        <input
                          id="create-user-name"
                          v-model.trim="editUserName"
                          type="text"
                          class="form-control"
                          maxlength="256"
                          required
                          :disabled="userSaving"
                        />
                      </div>
                    </div>
                    <div class="col-md-6">
                      <div class="form-group">
                        <label for="create-user-password">رمز عبور</label>
                        <input
                          id="create-user-password"
                          v-model="newPassword"
                          type="password"
                          dir="ltr"
                          class="form-control"
                          minlength="8"
                          maxlength="128"
                          autocomplete="new-password"
                          required
                          :disabled="userSaving"
                        />
                      </div>
                    </div>
                    <div class="col-md-6">
                      <div class="form-group">
                        <label for="create-user-password-confirmation">تکرار رمز عبور</label>
                        <input
                          id="create-user-password-confirmation"
                          v-model="confirmNewPassword"
                          type="password"
                          dir="ltr"
                          class="form-control"
                          minlength="8"
                          maxlength="128"
                          autocomplete="new-password"
                          required
                          :disabled="userSaving"
                        />
                      </div>
                    </div>
                  </div>

                  <div class="custom-control custom-checkbox mb-4">
                    <input
                      id="create-user-active"
                      v-model="createIsActive"
                      type="checkbox"
                      class="custom-control-input"
                      :disabled="userSaving"
                    />
                    <label class="custom-control-label" for="create-user-active">
                      حساب کاربر از زمان ایجاد فعال باشد
                    </label>
                  </div>

                  <div class="form-group mb-0">
                    <label class="d-block">نقش‌های کاربر</label>
                    <div v-if="rolesLoading" class="py-3 text-center text-muted">
                      <span class="spinner-border spinner-border-sm ml-2" role="status"></span>
                      در حال دریافت نقش‌ها...
                    </div>
                    <div v-else-if="availableRoles.length" class="users-role-modal__list">
                      <div
                        v-for="role in availableRoles"
                        :key="role.id"
                        class="custom-control custom-checkbox users-role-option"
                      >
                        <input
                          :id="`create-user-role-${role.id}`"
                          v-model="createRoleNames"
                          :value="role.name"
                          type="checkbox"
                          class="custom-control-input"
                          :disabled="userSaving"
                        />
                        <label
                          class="custom-control-label"
                          :for="`create-user-role-${role.id}`"
                        >
                          <strong>{{ role.name }}</strong>
                        </label>
                      </div>
                    </div>
                    <p v-else class="mb-0 text-muted">نقشی برای انتخاب وجود ندارد.</p>
                  </div>
                </template>

                <template v-else-if="userModalMode === 'edit'">
                  <div class="form-group">
                    <label for="edit-user-full-name">نام و نام خانوادگی</label>
                    <input
                      id="edit-user-full-name"
                      v-model.trim="editFullName"
                      type="text"
                      class="form-control"
                      maxlength="200"
                      required
                      :disabled="userSaving"
                    />
                  </div>
                  <div class="form-group">
                    <label for="edit-user-phone">شماره تلفن</label>
                    <input
                      id="edit-user-phone"
                      v-model.trim="editPhoneNumber"
                      type="tel"
                      dir="ltr"
                      class="form-control"
                      maxlength="32"
                      required
                      :disabled="userSaving"
                    />
                  </div>
                  <div class="form-group">
                    <label for="edit-user-email">ایمیل</label>
                    <input
                      id="edit-user-email"
                      v-model.trim="editEmail"
                      type="email"
                      dir="ltr"
                      class="form-control"
                      maxlength="256"
                      required
                      :disabled="userSaving"
                    />
                  </div>
                  <div class="form-group mb-0">
                    <label for="edit-user-name">نام کاربری</label>
                    <input
                      id="edit-user-name"
                      v-model.trim="editUserName"
                      type="text"
                      class="form-control"
                      maxlength="256"
                      required
                      :disabled="userSaving"
                    />
                  </div>
                </template>

                <template v-else>
                  <div class="form-group">
                    <label for="new-user-password">رمز عبور جدید</label>
                    <input
                      id="new-user-password"
                      v-model="newPassword"
                      type="password"
                      dir="ltr"
                      class="form-control"
                      minlength="8"
                      maxlength="128"
                      autocomplete="new-password"
                      required
                      :disabled="userSaving"
                    />
                  </div>
                  <div class="form-group mb-0">
                    <label for="confirm-new-user-password">تکرار رمز عبور جدید</label>
                    <input
                      id="confirm-new-user-password"
                      v-model="confirmNewPassword"
                      type="password"
                      dir="ltr"
                      class="form-control"
                      minlength="8"
                      maxlength="128"
                      autocomplete="new-password"
                      required
                      :disabled="userSaving"
                    />
                  </div>
                </template>
              </div>

              <div class="modal-footer modal-footer--split">
                <button
                  type="button"
                  class="btn btn-light btn-sm"
                  :disabled="userSaving"
                  @click="closeUserModal"
                >
                  انصراف
                </button>
                <button
                  type="submit"
                  class="btn btn-primary btn-sm"
                  :disabled="userSaving || (userModalMode === 'create' && rolesLoading)"
                >
                  <span
                    v-if="userSaving"
                    class="spinner-border spinner-border-sm ml-2"
                    role="status"
                  ></span>
                  {{
                    userModalMode === 'create'
                      ? 'افزودن کاربر'
                      : userModalMode === 'edit'
                        ? 'ذخیره اطلاعات'
                        : 'تغییر رمز عبور'
                  }}
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
