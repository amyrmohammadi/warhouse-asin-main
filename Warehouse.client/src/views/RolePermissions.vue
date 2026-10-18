<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import ConfirmDialog from '@/components/ConfirmDialog.vue'
import {
  getRolePolicies,
  RolesApiError,
  setRolePolicies,
  type RolePermissionGroup,
} from '@/services/roles'
import { toast } from '@/services/toast'

defineOptions({ name: 'RolePermissionsView' })

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

const route = useRoute()
const router = useRouter()
const confirmDialog = ref<ConfirmDialogInstance | null>(null)
const roleName = ref(String(route.params.roleName ?? ''))
const groups = ref<RolePermissionGroup[]>([])
const selectedPolicies = ref<string[]>([])
const initialPolicies = ref<string[]>([])
const loading = ref(true)
const saving = ref(false)
let activeRequest: AbortController | null = null

const allPermissionNames = computed(() =>
  groups.value.flatMap((group) => group.permissions.map((permission) => permission.name)),
)

const selectedCount = computed(() => selectedPolicies.value.length)
const hasChanges = computed(() => {
  const current = [...selectedPolicies.value].sort()
  const initial = [...initialPolicies.value].sort()
  return current.length !== initial.length || current.some((value, index) => value !== initial[index])
})
const allSelected = computed(
  () =>
    allPermissionNames.value.length > 0 &&
    allPermissionNames.value.every((name) => selectedPolicies.value.includes(name)),
)

function isGroupSelected(group: RolePermissionGroup): boolean {
  return (
    group.permissions.length > 0 &&
    group.permissions.every((permission) => selectedPolicies.value.includes(permission.name))
  )
}

function isGroupPartiallySelected(group: RolePermissionGroup): boolean {
  const selectedPermissionCount = group.permissions.filter((permission) =>
    selectedPolicies.value.includes(permission.name),
  ).length

  return selectedPermissionCount > 0 && selectedPermissionCount < group.permissions.length
}

function toggleGroup(group: RolePermissionGroup): void {
  const names = group.permissions.map((permission) => permission.name)
  if (isGroupSelected(group)) {
    selectedPolicies.value = selectedPolicies.value.filter((name) => !names.includes(name))
    return
  }

  selectedPolicies.value = [...new Set([...selectedPolicies.value, ...names])]
}

function toggleAll(): void {
  selectedPolicies.value = allSelected.value ? [] : [...allPermissionNames.value]
}

async function loadPermissions(): Promise<void> {
  activeRequest?.abort()
  activeRequest = new AbortController()
  loading.value = true

  try {
    const result = await getRolePolicies(roleName.value, activeRequest.signal)
    roleName.value = result.roleName
    groups.value = result.groups
    const assigned = result.groups.flatMap((group) =>
      group.permissions
        .filter((permission) => permission.isAssigned)
        .map((permission) => permission.name),
    )
    selectedPolicies.value = assigned
    initialPolicies.value = [...assigned]
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (error instanceof RolesApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: route.fullPath } })
      return
    }
    if (error instanceof RolesApiError && error.status === 403) {
      await router.replace({ name: 'roles' })
      return
    }
    toast.error(error instanceof Error ? error.message : 'خطای ناشناخته‌ای رخ داد.')
  } finally {
    loading.value = false
  }
}

async function savePermissions(): Promise<void> {
  if (!hasChanges.value || saving.value) return

  const confirmed = await confirmDialog.value?.ask({
    title: 'ذخیره دسترسی‌های نقش',
    text: `دسترسی‌های نقش «${roleName.value}» به‌روزرسانی شود؟`,
    icon: 'question',
    confirmButtonText: 'ذخیره شود',
  })
  if (!confirmed) return

  activeRequest?.abort()
  activeRequest = new AbortController()
  saving.value = true

  try {
    await setRolePolicies(roleName.value, selectedPolicies.value, activeRequest.signal)
    initialPolicies.value = [...selectedPolicies.value]
    toast.success('دسترسی‌های نقش با موفقیت ذخیره شد.')
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (error instanceof RolesApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: route.fullPath } })
      return
    }
    toast.error(error instanceof Error ? error.message : 'خطای ناشناخته‌ای رخ داد.')
  } finally {
    saving.value = false
  }
}

onMounted(loadPermissions)
onBeforeUnmount(() => activeRequest?.abort())
</script>

<template>
  <ConfirmDialog ref="confirmDialog" />

  <div class="container-fluid role-permissions-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item"><RouterLink to="/dashboard/roles">نقش‌ها</RouterLink></li>
        <li class="breadcrumb-item active"><span>مدیریت دسترسی‌ها</span></li>
      </ol>
    </div>

    <div class="card">
      <div class="card-header role-permissions-page__header">
        <div>
          <h4 class="card-title mb-1">
            <i class="las la-user-shield ml-1"></i>
            دسترسی‌های نقش «{{ roleName }}»
          </h4>
          <p class="page-description mb-0 text-muted">دسترسی‌ها بر اساس حوزه کاری دسته‌بندی شده‌اند.</p>
        </div>
        <RouterLink class="btn btn-light btn-sm mobile-icon-button" to="/dashboard/roles" title="بازگشت" aria-label="بازگشت">
          <i class="las la-arrow-right ml-1"></i><span class="mobile-button-label">بازگشت</span>
        </RouterLink>
      </div>

      <div class="card-body">
        <div v-if="loading" class="role-permissions-page__loading text-muted">
          <span class="spinner-border spinner-border-sm ml-2" role="status"></span>
          در حال دریافت دسترسی‌ها...
        </div>

        <template v-else-if="groups.length">
          <div class="role-permissions-page__toolbar">
            <div class="d-flex align-items-center">
              <span class="badge light badge-primary ml-3">
                {{ selectedCount }} از {{ allPermissionNames.length }} انتخاب شده
              </span>
              <button class="btn btn-outline-primary btn-sm" type="button" @click="toggleAll">
                {{ allSelected ? 'لغو انتخاب همه' : 'انتخاب همه' }}
              </button>
            </div>
          </div>

          <div class="row">
            <div v-for="group in groups" :key="group.key" class="col-xl-4 col-md-6 mb-4">
              <section class="role-permission-group h-100">
                <header
                  class="role-permission-group__header"
                  :class="{
                    'role-permission-group__header--complete': isGroupSelected(group),
                    'role-permission-group__header--partial': isGroupPartiallySelected(group),
                  }"
                >
                  <div>
                    <h5 class="mb-1">{{ group.displayName }}</h5>
                    <small class="text-muted">{{ group.permissions.length }} دسترسی</small>
                  </div>
                  <input
                    class="role-permission-group__checkbox"
                    type="checkbox"
                    :checked="isGroupSelected(group)"
                    :indeterminate="isGroupPartiallySelected(group)"
                    :title="isGroupSelected(group) ? 'لغو انتخاب گروه' : 'انتخاب گروه'"
                    :aria-label="isGroupSelected(group) ? 'لغو انتخاب گروه' : 'انتخاب گروه'"
                    @change="toggleGroup(group)"
                  />
                </header>

                <label
                  v-for="permission in group.permissions"
                  :key="permission.name"
                  class="role-permission-item"
                >
                  <input
                    v-model="selectedPolicies"
                    class="role-permission-item__checkbox"
                    type="checkbox"
                    :value="permission.name"
                  />
                  <span>
                    <strong class="d-block text-black">{{ permission.displayName }}</strong>
                    <small class="text-muted" dir="ltr">{{ permission.name }}</small>
                  </span>
                </label>
              </section>
            </div>
          </div>
        </template>
      </div>

      <div v-if="!loading && groups.length" class="card-footer d-flex justify-content-end">
        <button
          class="btn btn-primary"
          type="button"
          :disabled="!hasChanges || saving"
          @click="savePermissions"
        >
          <span v-if="saving" class="spinner-border spinner-border-sm ml-2"></span>
          <i v-else class="las la-save ml-1"></i>
          ذخیره دسترسی‌ها
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.role-permissions-page__header,
.role-permissions-page__toolbar,
.role-permission-group__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.role-permissions-page__toolbar {
  margin-bottom: 1.5rem;
  justify-content: flex-end;
  flex-wrap: wrap;
}

.role-permissions-page__loading {
  padding: 3rem 1rem;
  text-align: center;
}

.role-permission-group {
  border: 1px solid #e8e8ef;
  border-radius: 0.75rem;
  overflow: hidden;
  background: #fff;
}

.role-permission-group__header {
  padding: 1rem 1.25rem;
  background: #f8f9fc;
  border-bottom: 1px solid #e8e8ef;
  transition: background-color 0.2s ease, border-color 0.2s ease;
}

.role-permission-group__header--complete {
  background-color: #d1fae5 !important;
  border-bottom-color: #d1fae5;
}

.role-permission-group__header--partial {
  background-color: var(--primary-light) !important;
  border-bottom-color: var(--primary-light);
}

.role-permission-group__header--complete h5 {
  color: #166534;
}

.role-permission-group__header--partial h5 {
  color: var(--primary-dark);
}

.role-permission-item {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.9rem 1.25rem;
  margin: 0;
  cursor: pointer;
  border-bottom: 1px solid #f1f1f5;
}

.role-permission-item:last-child {
  border-bottom: 0;
}

.role-permission-item:hover {
  background: #fbfcff;
}

.role-permission-item__checkbox,
.role-permission-group__checkbox {
  appearance: none;
  -webkit-appearance: none;
  background-color: transparent;
  background-position: center;
  background-repeat: no-repeat;
  background-size: 0.8rem 0.8rem;
  border: 2px solid var(--secondary);
  border-radius: 0.25rem;
  cursor: pointer;
}

.role-permission-item__checkbox {
  width: 1.1rem;
  height: 1.1rem;
  margin-top: 0.2rem;
}

.role-permission-group__checkbox {
  width: 1.25rem;
  height: 1.25rem;
  margin: 0;
}

.role-permission-item__checkbox:checked,
.role-permission-group__checkbox:checked {
  background-color: var(--secondary);
  background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'%3e%3cpath fill='none' stroke='%23fff' stroke-linecap='round' stroke-linejoin='round' stroke-width='2.5' d='M3 8l3 3 7-7'/%3e%3c/svg%3e");
}

.role-permission-group__checkbox:indeterminate {
  background-color: var(--secondary);
  background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'%3e%3cpath fill='none' stroke='%23fff' stroke-linecap='round' stroke-width='2.5' d='M3 8h10'/%3e%3c/svg%3e");
}

.role-permission-item__checkbox:focus-visible,
.role-permission-group__checkbox:focus-visible {
  outline: 2px solid color-mix(in srgb, var(--secondary) 35%, transparent);
  outline-offset: 2px;
}

.role-permission-item__checkbox::after,
.role-permission-group__checkbox::after {
  content: none !important;
  display: none !important;
}

@media (max-width: 575.98px) {
  .role-permissions-page__header,
  .role-permissions-page__toolbar {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
