import type { ApiResult } from '@/services/auth'
import { expireSession, getSession } from '@/services/auth'
import type { NumericFilterValue } from '@/types/dataTableFilters'

export interface RoleListItem {
  id: string
  name: string
  userCount: number
  permissionCount: number
}

export interface GetRolesResponse {
  items: RoleListItem[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
  paginationEnabled: boolean
}

export interface GetRolesParameters {
  paginationEnabled?: boolean
  pageNumber?: number
  pageSize?: number
  search?: string
  roleName?: string
  userCount?: NumericFilterValue | null
  permissionCount?: NumericFilterValue | null
  sortBy?: string
  sortDescending?: boolean
  signal?: AbortSignal
}

export interface RolePermission {
  name: string
  displayName: string
  isAssigned: boolean
}

export interface RolePermissionGroup {
  key: string
  displayName: string
  permissions: RolePermission[]
}

export interface GetRolePoliciesResponse {
  roleName: string
  groups: RolePermissionGroup[]
}

export class RolesApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message)
    this.name = 'RolesApiError'
  }
}

export async function getRoles(parameters: GetRolesParameters = {}): Promise<GetRolesResponse> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new RolesApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  const query = new URLSearchParams({
    paginationEnabled: String(parameters.paginationEnabled ?? false),
    pageNumber: String(parameters.pageNumber ?? 1),
    pageSize: String(parameters.pageSize ?? 20),
  })

  if (parameters.search) query.set('search', parameters.search)
  if (parameters.roleName) query.set('roleName', parameters.roleName)
  if (parameters.sortBy) query.set('sortBy', parameters.sortBy)
  if (parameters.sortDescending !== undefined) {
    query.set('sortDescending', String(parameters.sortDescending))
  }
  appendNumericFilter(query, 'userCount', parameters.userCount)
  appendNumericFilter(query, 'permissionCount', parameters.permissionCount)

  let response: Response
  try {
    response = await fetch(`/endpoint/admin/roles?${query}`, {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      signal: parameters.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new RolesApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) expireSession()

  let result: ApiResult<GetRolesResponse> | null = null
  try {
    result = (await response.json()) as ApiResult<GetRolesResponse>
  } catch {
    // The status-specific message below is more useful than a JSON parse error.
  }

  if (!response.ok || !result?.isSuccess || !result.data) {
    const message =
      result?.messages?.find(Boolean) ??
      (response.status === 403
        ? 'شما مجوز مشاهده فهرست نقش‌ها را ندارید.'
        : 'دریافت فهرست نقش‌ها انجام نشد.')
    throw new RolesApiError(message, response.status)
  }

  return result.data
}

function appendNumericFilter(
  query: URLSearchParams,
  key: string,
  filter: NumericFilterValue | null | undefined,
): void {
  if (!filter || filter.value === null) return

  query.set(`${key}.operator`, filter.operator)
  query.set(`${key}.value`, String(filter.value))
  if (filter.operator === 'between' && filter.valueTo !== null && filter.valueTo !== undefined) {
    query.set(`${key}.valueTo`, String(filter.valueTo))
  }
}

export async function createRole(name: string, signal?: AbortSignal): Promise<void> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new RolesApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch('/endpoint/admin/roles', {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ name }),
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new RolesApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) expireSession()

  let result: ApiResult<unknown> | null = null
  try {
    result = (await response.json()) as ApiResult<unknown>
  } catch {
    // The status-specific message below is more useful than a JSON parse error.
  }

  if (!response.ok || !result?.isSuccess) {
    const message =
      result?.messages?.find(Boolean) ??
      (response.status === 403
        ? 'شما مجوز افزودن نقش را ندارید.'
        : 'افزودن نقش انجام نشد.')
    throw new RolesApiError(message, response.status)
  }
}

export async function updateRole(
  roleId: string,
  name: string,
  signal?: AbortSignal,
): Promise<void> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new RolesApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch(`/endpoint/admin/roles/${encodeURIComponent(roleId)}`, {
      method: 'PUT',
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ name }),
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new RolesApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  await ensureRoleOperationSucceeded(response, 'ویرایش نقش انجام نشد.')
}

export async function deleteRole(roleId: string, signal?: AbortSignal): Promise<void> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new RolesApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch(`/endpoint/admin/roles/${encodeURIComponent(roleId)}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${session.accessToken}` },
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new RolesApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  await ensureRoleOperationSucceeded(response, 'حذف نقش انجام نشد.')
}

async function ensureRoleOperationSucceeded(
  response: Response,
  fallbackMessage: string,
): Promise<void> {
  if (response.status === 401) expireSession()

  let result: ApiResult<unknown> | null = null
  try {
    result = (await response.json()) as ApiResult<unknown>
  } catch {
    // پیام متناسب با وضعیت HTTP در ادامه برگردانده می‌شود.
  }

  if (!response.ok || !result?.isSuccess) {
    const message = result?.messages?.find(Boolean)
      ?? (response.status === 403 ? 'شما مجوز مدیریت نقش‌ها را ندارید.' : fallbackMessage)
    throw new RolesApiError(message, response.status)
  }
}

export async function getRolePolicies(
  roleName: string,
  signal?: AbortSignal,
): Promise<GetRolePoliciesResponse> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new RolesApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch(
      `/endpoint/admin/roles/${encodeURIComponent(roleName)}/policies`,
      {
        headers: { Authorization: `Bearer ${session.accessToken}` },
        signal,
      },
    )
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new RolesApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) expireSession()

  let result: ApiResult<GetRolePoliciesResponse> | null = null
  try {
    result = (await response.json()) as ApiResult<GetRolePoliciesResponse>
  } catch {
    // The status-specific message below is more useful than a JSON parse error.
  }

  if (!response.ok || !result?.isSuccess || !result.data) {
    const message =
      result?.messages?.find(Boolean) ??
      (response.status === 403
        ? 'شما مجوز مدیریت دسترسی‌های نقش را ندارید.'
        : 'دریافت دسترسی‌های نقش انجام نشد.')
    throw new RolesApiError(message, response.status)
  }

  return result.data
}

export async function setRolePolicies(
  roleName: string,
  policies: string[],
  signal?: AbortSignal,
): Promise<void> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new RolesApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch(
      `/endpoint/admin/roles/${encodeURIComponent(roleName)}/policies`,
      {
        method: 'PUT',
        headers: {
          Authorization: `Bearer ${session.accessToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ policies }),
        signal,
      },
    )
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new RolesApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) expireSession()

  let result: ApiResult<unknown> | null = null
  try {
    result = (await response.json()) as ApiResult<unknown>
  } catch {
    // The status-specific message below is more useful than a JSON parse error.
  }

  if (!response.ok || !result?.isSuccess) {
    const message =
      result?.messages?.find(Boolean) ??
      (response.status === 403
        ? 'شما مجوز مدیریت دسترسی‌های نقش را ندارید.'
        : 'ذخیره دسترسی‌های نقش انجام نشد.')
    throw new RolesApiError(message, response.status)
  }
}
