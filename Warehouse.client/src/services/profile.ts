import type { ApiResult } from '@/services/auth'
import { clearSession, getSession } from '@/services/auth'

export interface CurrentUserProfile {
  userId: string
  userName: string
  email: string
  fullName: string
  phoneNumber: string
  profileImageContentType: string | null
  profileImage: string | null
}

export interface UpdateCurrentUserProfileRequest {
  fullName: string
  phoneNumber: string
  profileImage: File | null
  removeProfileImage: boolean
}

interface ApiOperationResult {
  isSuccess: boolean
  messages: string[]
}

export const profileUpdatedEvent = 'warehouse:profile-updated'

export class ProfileApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message)
    this.name = 'ProfileApiError'
  }
}

export function getProfileImageDataUrl(profile: CurrentUserProfile | null): string | null {
  if (!profile?.profileImage || !profile.profileImageContentType) return null
  return `data:${profile.profileImageContentType};base64,${profile.profileImage}`
}

export async function getCurrentUserProfile(signal?: AbortSignal): Promise<CurrentUserProfile> {
  const result = await send<CurrentUserProfile>(
    '/endpoint/auth/profile',
    { method: 'GET', signal },
    'دریافت اطلاعات حساب کاربری انجام نشد.',
  )

  if (!result.data) throw new ProfileApiError('اطلاعات حساب کاربری دریافت نشد.', 500)
  return result.data
}

export async function updateCurrentUserProfile(
  request: UpdateCurrentUserProfileRequest,
  signal?: AbortSignal,
): Promise<void> {
  const formData = new FormData()
  formData.append('fullName', request.fullName)
  formData.append('phoneNumber', request.phoneNumber)
  formData.append('removeProfileImage', String(request.removeProfileImage))
  if (request.profileImage) formData.append('profileImage', request.profileImage)

  await send<never>(
    '/endpoint/auth/profile',
    { method: 'PUT', body: formData, signal },
    'ویرایش حساب کاربری انجام نشد.',
  )
}

export async function changeCurrentUserPassword(
  currentPassword: string,
  newPassword: string,
  signal?: AbortSignal,
): Promise<void> {
  await send<never>(
    '/endpoint/auth/profile/password',
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentPassword, newPassword }),
      signal,
    },
    'تغییر رمز عبور انجام نشد.',
  )
}

async function send<T>(
  path: string,
  options: RequestInit,
  fallbackMessage: string,
): Promise<ApiResult<T>> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new ProfileApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    const headers = new Headers(options.headers)
    headers.set('Authorization', `Bearer ${session.accessToken}`)
    response = await fetch(path, { ...options, headers })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ProfileApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) clearSession()

  let result: (ApiResult<T> & ApiOperationResult) | null = null
  try {
    result = (await response.json()) as ApiResult<T> & ApiOperationResult
  } catch {
    // Use the status-specific fallback below.
  }

  if (!response.ok || !result?.isSuccess) {
    const message = result?.messages?.find(Boolean) ?? fallbackMessage
    throw new ProfileApiError(message, response.status)
  }

  return result
}
