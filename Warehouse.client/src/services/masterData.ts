import type { ApiResult } from '@/services/auth'
import { expireSession, getSession } from '@/services/auth'

export interface MasterDataItem {
  id: string
  concurrencyStamp: string
  [key: string]: string | null
}

export interface MasterDataListResponse {
  items: MasterDataItem[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
  paginationEnabled: boolean
}

export interface MasterDataListParameters {
  paginationEnabled?: boolean
  pageNumber?: number
  pageSize?: number
  search?: string
  filters?: Record<string, string | undefined>
  sortBy?: string
  sortDescending?: boolean
  signal?: AbortSignal
}

export interface FieldValidationRules {
  required: boolean
  maxLength: number | null
}

export type MasterDataValidationRules = Record<string, FieldValidationRules>
export type SaveMasterDataRequest = Record<string, string | null>

export interface MasterDataImportRowError {
  rowNumber: number
  message: string
}

export interface MasterDataImportSummary {
  processedRowCount: number
  importedCount: number
  duplicateCount: number
  emptyRowCount: number
  invalidRows: MasterDataImportRowError[]
}

interface ValidationProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class MasterDataApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message)
    this.name = 'MasterDataApiError'
  }
}

export async function getMasterDataList(
  basePath: string,
  parameters: MasterDataListParameters = {},
): Promise<MasterDataListResponse> {
  const query = new URLSearchParams({
    paginationEnabled: String(parameters.paginationEnabled ?? true),
    pageNumber: String(parameters.pageNumber ?? 1),
    pageSize: String(parameters.pageSize ?? 10),
  })

  if (parameters.search) query.set('search', parameters.search)
  if (parameters.sortBy) query.set('sortBy', parameters.sortBy)
  if (parameters.sortDescending !== undefined) {
    query.set('sortDescending', String(parameters.sortDescending))
  }

  for (const [key, value] of Object.entries(parameters.filters ?? {})) {
    if (value) query.set(key, value)
  }

  return request<MasterDataListResponse>(
    `${basePath}?${query}`,
    { signal: parameters.signal },
    'دریافت فهرست اطلاعات پایه انجام نشد.',
  )
}

export function getMasterDataValidationRules(
  basePath: string,
  signal?: AbortSignal,
): Promise<MasterDataValidationRules> {
  return request<MasterDataValidationRules>(
    `${basePath}/validation-rules`,
    { signal },
    'دریافت قواعد اعتبارسنجی انجام نشد.',
  )
}

export function createMasterDataItem(
  basePath: string,
  payload: SaveMasterDataRequest,
  signal?: AbortSignal,
): Promise<MasterDataItem> {
  return request<MasterDataItem>(
    basePath,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal,
    },
    'افزودن اطلاعات انجام نشد.',
  )
}

export function updateMasterDataItem(
  basePath: string,
  id: string,
  payload: SaveMasterDataRequest,
  signal?: AbortSignal,
): Promise<MasterDataItem> {
  return request<MasterDataItem>(
    `${basePath}/${encodeURIComponent(id)}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal,
    },
    'ویرایش اطلاعات انجام نشد.',
  )
}

export async function deleteMasterDataItem(
  basePath: string,
  id: string,
  signal?: AbortSignal,
): Promise<void> {
  await request<unknown>(
    `${basePath}/${encodeURIComponent(id)}`,
    { method: 'DELETE', signal },
    'حذف اطلاعات انجام نشد.',
    false,
  )
}

export function importMasterDataFile(
  basePath: string,
  file: File,
  signal?: AbortSignal,
  fields: Record<string, string> = {},
): Promise<MasterDataImportSummary> {
  const formData = new FormData()
  formData.append('File', file)
  Object.entries(fields).forEach(([key, value]) => formData.append(key, value))

  return request<MasterDataImportSummary>(
    `${basePath}/import`,
    {
      method: 'POST',
      body: formData,
      signal,
    },
    'ایمپورت فایل Excel انجام نشد.',
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
    throw new MasterDataApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
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
    throw new MasterDataApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) expireSession()

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

  throw new MasterDataApiError(message, response.status, fieldErrors)
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
