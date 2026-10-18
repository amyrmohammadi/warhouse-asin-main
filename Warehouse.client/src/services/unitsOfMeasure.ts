import type { ApiResult } from '@/services/auth'
import { clearSession, getSession } from '@/services/auth'

const basePath = '/endpoint/master-data/units-of-measure'

export interface UnitOfMeasure {
  id: string
  title: string
  description: string | null
}

export interface GetUnitsOfMeasureResponse {
  items: UnitOfMeasure[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
  paginationEnabled: boolean
}

export interface GetUnitsOfMeasureParameters {
  paginationEnabled?: boolean
  pageNumber?: number
  pageSize?: number
  search?: string
  title?: string
  description?: string
  sortBy?: string
  sortDescending?: boolean
  signal?: AbortSignal
}

export interface SaveUnitOfMeasureRequest {
  title: string
  description: string | null
}

export interface FieldValidationRules {
  required: boolean
  maxLength: number | null
}

export type UnitOfMeasureValidationRules = Record<string, FieldValidationRules>

interface ValidationProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class UnitsOfMeasureApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message)
    this.name = 'UnitsOfMeasureApiError'
  }
}

export async function getUnitsOfMeasure(
  parameters: GetUnitsOfMeasureParameters = {},
): Promise<GetUnitsOfMeasureResponse> {
  const query = new URLSearchParams({
    paginationEnabled: String(parameters.paginationEnabled ?? true),
    pageNumber: String(parameters.pageNumber ?? 1),
    pageSize: String(parameters.pageSize ?? 10),
  })

  if (parameters.search) query.set('search', parameters.search)
  if (parameters.title) query.set('title', parameters.title)
  if (parameters.description) query.set('description', parameters.description)
  if (parameters.sortBy) query.set('sortBy', parameters.sortBy)
  if (parameters.sortDescending !== undefined) {
    query.set('sortDescending', String(parameters.sortDescending))
  }

  return request<GetUnitsOfMeasureResponse>(
    `${basePath}?${query}`,
    { signal: parameters.signal },
    'دریافت فهرست واحدهای اندازه‌گیری انجام نشد.',
  )
}

export function getUnitOfMeasureValidationRules(
  signal?: AbortSignal,
): Promise<UnitOfMeasureValidationRules> {
  return request<UnitOfMeasureValidationRules>(
    `${basePath}/validation-rules`,
    { signal },
    'دریافت قواعد اعتبارسنجی انجام نشد.',
  )
}

export function createUnitOfMeasure(
  payload: SaveUnitOfMeasureRequest,
  signal?: AbortSignal,
): Promise<UnitOfMeasure> {
  return request<UnitOfMeasure>(
    basePath,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal,
    },
    'افزودن واحد اندازه‌گیری انجام نشد.',
  )
}

export function updateUnitOfMeasure(
  id: string,
  payload: SaveUnitOfMeasureRequest,
  signal?: AbortSignal,
): Promise<UnitOfMeasure> {
  return request<UnitOfMeasure>(
    `${basePath}/${encodeURIComponent(id)}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal,
    },
    'ویرایش واحد اندازه‌گیری انجام نشد.',
  )
}

export async function deleteUnitOfMeasure(id: string, signal?: AbortSignal): Promise<void> {
  await request<unknown>(
    `${basePath}/${encodeURIComponent(id)}`,
    { method: 'DELETE', signal },
    'حذف واحد اندازه‌گیری انجام نشد.',
    false,
  )
}

async function request<T>(
  path: string,
  init: RequestInit,
  fallbackMessage: string,
  requireData = true,
): Promise<T> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new UnitsOfMeasureApiError(
      'نشست شما منقضی شده است. لطفاً دوباره وارد شوید.',
      401,
    )
  }

  let response: Response
  try {
    response = await fetch(path, {
      ...init,
      headers: {
        ...init.headers,
        Authorization: `Bearer ${session.accessToken}`,
      },
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new UnitsOfMeasureApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) clearSession()

  let payload: ApiResult<T> | ValidationProblemDetails | null = null
  try {
    payload = (await response.json()) as ApiResult<T> | ValidationProblemDetails
  } catch {
    // A status-specific fallback is returned below.
  }

  const result = payload as ApiResult<T> | null
  if (response.ok && result?.isSuccess && (!requireData || result.data !== null)) {
    return result.data as T
  }

  const problem = payload as ValidationProblemDetails | null
  const fieldErrors = normalizeFieldErrors(problem?.errors)
  const message =
    result?.messages?.find(Boolean) ??
    Object.values(fieldErrors).flat().find(Boolean) ??
    problem?.detail ??
    problem?.title ??
    (response.status === 403 ? 'شما مجوز انجام این عملیات را ندارید.' : fallbackMessage)

  throw new UnitsOfMeasureApiError(message, response.status, fieldErrors)
}

function normalizeFieldErrors(
  errors: Record<string, string[]> | undefined,
): Record<string, string[]> {
  if (!errors) return {}

  return Object.fromEntries(
    Object.entries(errors).map(([key, messages]) => [
      key.length ? key[0].toLowerCase() + key.slice(1) : key,
      messages,
    ]),
  )
}
