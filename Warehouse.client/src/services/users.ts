import type { ApiResult } from '@/services/auth'
import { clearSession, getSession } from '@/services/auth'

export interface UserListItem {
  id: string
  userName: string
  email: string
  fullName: string
  phoneNumber: string
  isActive: boolean
  emailConfirmed: boolean
  createdAtUtc: string
  roles: string[]
  profileImageContentType: string | null
  profileImage: string | null
}

export interface UpdateUserRequest {
  fullName: string
  phoneNumber: string
  email: string
  userName: string
}

export interface CreateManagedUserRequest {
  fullName: string
  phoneNumber: string
  email: string
  userName?: string
  password: string
  isActive: boolean
  roleNames: string[]
}

export interface GetUsersResponse {
  items: UserListItem[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
  paginationEnabled: boolean
}

export interface GetUsersParameters {
  paginationEnabled?: boolean
  pageNumber: number
  pageSize: number
  search?: string
  fullName?: string
  userName?: string
  email?: string
  isActive?: boolean
  roleName?: string
  sortBy?: string
  sortDescending?: boolean
  signal?: AbortSignal
}

interface ApiOperationResult {
  isSuccess: boolean
  messages: string[]
}

export class UsersApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message)
    this.name = 'UsersApiError'
  }
}

export async function getUsers(parameters: GetUsersParameters): Promise<GetUsersResponse> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new UsersApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  const query = new URLSearchParams({
    paginationEnabled: String(parameters.paginationEnabled ?? false),
    pageNumber: String(parameters.pageNumber),
    pageSize: String(parameters.pageSize),
  })

  if (parameters.search) query.set('search', parameters.search)
  if (parameters.fullName) query.set('fullName', parameters.fullName)
  if (parameters.userName) query.set('userName', parameters.userName)
  if (parameters.email) query.set('email', parameters.email)
  if (parameters.isActive !== undefined) query.set('isActive', String(parameters.isActive))
  if (parameters.roleName) query.set('roleName', parameters.roleName)
  if (parameters.sortBy) query.set('sortBy', parameters.sortBy)
  if (parameters.sortDescending) query.set('sortDescending', 'true')

  let response: Response
  try {
    response = await fetch(`/endpoint/admin/users?${query}`, {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      signal: parameters.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new UsersApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) clearSession()

  let result: ApiResult<GetUsersResponse> | null = null
  try {
    result = (await response.json()) as ApiResult<GetUsersResponse>
  } catch {
    // The status-specific message below is more useful than a JSON parse error.
  }

  if (!response.ok || !result?.isSuccess || !result.data) {
    const message = result?.messages?.find(Boolean) ??
      (response.status === 403
        ? 'شما مجوز مشاهده فهرست کاربران را ندارید.'
        : 'دریافت فهرست کاربران انجام نشد.')
    throw new UsersApiError(message, response.status)
  }

  return result.data
}

export async function createManagedUser(
  request: CreateManagedUserRequest,
  signal?: AbortSignal,
): Promise<void> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new UsersApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch('/endpoint/admin/users', {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new UsersApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) clearSession()

  let result: ApiOperationResult | null = null
  try {
    result = (await response.json()) as ApiOperationResult
  } catch {
    // The status-specific message below is more useful than a JSON parse error.
  }

  if (!response.ok || !result?.isSuccess) {
    const message =
      result?.messages?.find(Boolean) ??
      (response.status === 403
        ? 'شما مجوز افزودن کاربر و تخصیص نقش را ندارید.'
        : 'افزودن کاربر انجام نشد.')
    throw new UsersApiError(message, response.status)
  }
}

export async function setUserRoles(
  userId: string,
  roleNames: string[],
  signal?: AbortSignal,
): Promise<void> {
  await sendUserOperation(
    `/endpoint/admin/users/${userId}/roles`,
    { roleNames },
    'شما مجوز ویرایش نقش‌های کاربران را ندارید.',
    'ذخیره نقش‌های کاربر انجام نشد.',
    signal,
  )
}

export async function updateUser(
  userId: string,
  request: UpdateUserRequest,
  signal?: AbortSignal,
): Promise<void> {
  await sendUserOperation(
    `/endpoint/admin/users/${userId}`,
    request,
    'شما مجوز ویرایش اطلاعات کاربران را ندارید.',
    'ویرایش اطلاعات کاربر انجام نشد.',
    signal,
  )
}

export async function setUserActiveStatus(
  userId: string,
  isActive: boolean,
  signal?: AbortSignal,
): Promise<void> {
  await sendUserOperation(
    `/endpoint/admin/users/${userId}/active-status`,
    { isActive },
    'شما مجوز تغییر وضعیت حساب کاربران را ندارید.',
    'تغییر وضعیت حساب کاربر انجام نشد.',
    signal,
  )
}

export async function changeUserPassword(
  userId: string,
  newPassword: string,
  signal?: AbortSignal,
): Promise<void> {
  await sendUserOperation(
    `/endpoint/admin/users/${userId}/password`,
    { newPassword },
    'شما مجوز تغییر رمز عبور کاربران را ندارید.',
    'تغییر رمز عبور کاربر انجام نشد.',
    signal,
  )
}

async function sendUserOperation(
  path: string,
  body: unknown,
  forbiddenMessage: string,
  fallbackMessage: string,
  signal?: AbortSignal,
): Promise<void> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new UsersApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch(path, {
      method: 'PUT',
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new UsersApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) clearSession()

  let result: ApiOperationResult | null = null
  try {
    result = (await response.json()) as ApiOperationResult
  } catch {
    // The status-specific message below is more useful than a JSON parse error.
  }

  if (!response.ok || !result?.isSuccess) {
    const message =
      result?.messages?.find(Boolean) ??
      (response.status === 403
        ? forbiddenMessage
        : fallbackMessage)
    throw new UsersApiError(message, response.status)
  }
}
