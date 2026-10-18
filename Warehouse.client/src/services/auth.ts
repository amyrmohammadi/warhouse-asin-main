export interface ApiResult<T> {
  isSuccess: boolean
  messages: string[]
  data: T | null
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest extends LoginRequest {
  fullName: string
  phoneNumber: string
  userName?: string
}

export interface LoginResponse {
  userId: string
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
  permissions: string[]
}

export interface AuthSession extends LoginResponse {
  email: string
}

const sessionKey = 'vue-clean-architecture.auth'

interface StoredSession {
  session: AuthSession
  storage: Storage
}

function getStorage(remember: boolean): Storage {
  return remember ? localStorage : sessionStorage
}

function readSessionFrom(storage: Storage): AuthSession | null {
  const value = storage.getItem(sessionKey)

  if (!value) return null

  try {
    return JSON.parse(value) as AuthSession
  } catch {
    storage.removeItem(sessionKey)
    return null
  }
}

function getStoredSession(): StoredSession | null {
  const localSession = readSessionFrom(localStorage)
  if (localSession) return { session: localSession, storage: localStorage }

  const temporarySession = readSessionFrom(sessionStorage)
  return temporarySession
    ? { session: temporarySession, storage: sessionStorage }
    : null
}

function normalizeMessages(payload: unknown, fallback: string): string[] {
  if (payload && typeof payload === 'object') {
    const result = payload as {
      messages?: unknown
      detail?: unknown
      title?: unknown
      errors?: Record<string, unknown>
    }

    if (Array.isArray(result.messages)) {
      const messages = result.messages.filter(
        (message): message is string => typeof message === 'string' && message.length > 0,
      )
      if (messages.length) return messages
    }

    if (result.errors) {
      const validationMessages = Object.values(result.errors).flatMap((value) =>
        Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [],
      )
      if (validationMessages.length) return validationMessages
    }

    if (typeof result.detail === 'string' && result.detail) return [result.detail]
    if (typeof result.title === 'string' && result.title) return [result.title]
  }

  return [fallback]
}

async function post<T>(path: string, body: unknown, fallbackMessage: string): Promise<T> {
  let response: Response

  try {
    response = await fetch(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
  } catch {
    throw new Error('ارتباط با سرور برقرار نشد. لطفاً از در حال اجرا بودن سرور مطمئن شوید.')
  }

  let payload: ApiResult<T> | unknown
  try {
    payload = await response.json()
  } catch {
    throw new Error(fallbackMessage)
  }

  if (!response.ok) {
    throw new Error(normalizeMessages(payload, fallbackMessage).join('\n'))
  }

  const result = payload as ApiResult<T>
  if (!result.isSuccess || result.data === null) {
    throw new Error(normalizeMessages(payload, fallbackMessage).join('\n'))
  }

  return result.data
}

export async function login(request: LoginRequest, remember = false): Promise<AuthSession> {
  const data = await post<LoginResponse>(
    '/endpoint/auth/login',
    request,
    'ورود به حساب کاربری انجام نشد.',
  )
  const session: AuthSession = { ...data, email: request.email }

  clearSession()
  getStorage(remember).setItem(sessionKey, JSON.stringify(session))

  return session
}

export async function register(request: RegisterRequest): Promise<string> {
  const data = await post<{ userId: string }>(
    '/endpoint/auth/register',
    request,
    'ثبت‌نام انجام نشد.',
  )

  return data.userId
}

export function getSession(): AuthSession | null {
  return getStoredSession()?.session ?? null
}

export async function synchronizeSessionPermissions(): Promise<AuthSession | null> {
  const stored = getStoredSession()
  if (!stored?.session.accessToken) return null

  const response = await fetch('/endpoint/auth/access', {
    headers: { Authorization: `Bearer ${stored.session.accessToken}` },
  })

  if (response.status === 401) {
    clearSession()
    return null
  }

  let result: ApiResult<{ permissions: string[] }> | null = null
  try {
    result = (await response.json()) as ApiResult<{ permissions: string[] }>
  } catch {
    // Keep the current session when a temporary server response cannot be parsed.
  }

  if (!response.ok || !result?.isSuccess || !result.data) {
    throw new Error('همگام‌سازی دسترسی‌های کاربر انجام نشد.')
  }

  const session: AuthSession = {
    ...stored.session,
    permissions: result.data.permissions,
  }
  stored.storage.setItem(sessionKey, JSON.stringify(session))
  return session
}

export function hasValidSession(): boolean {
  const session = getSession()

  if (!session?.accessToken || !session.accessTokenExpiresAtUtc) return false

  if (Date.parse(session.accessTokenExpiresAtUtc) <= Date.now()) {
    clearSession()
    return false
  }

  return true
}

export function clearSession(): void {
  localStorage.removeItem(sessionKey)
  sessionStorage.removeItem(sessionKey)
}
