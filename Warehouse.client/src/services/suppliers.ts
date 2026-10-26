import type { ApiResult } from '@/services/auth'
import { expireSession, getSession } from '@/services/auth'
import { getMasterDataList } from '@/services/masterData'

const basePath = '/endpoint/master-data/suppliers'

export interface SupplierOption {
  id: string
  title: string
  description: string | null
}

export interface SupplierSearchParameters {
  search: string
  limit?: number
  signal?: AbortSignal
}

/** حداقل تعداد نویسه لازم برای جستجوی تأمین‌کننده */
export const SUPPLIER_SEARCH_MIN_LENGTH = 2

/** حداکثر تعداد ردیف بازگشتی از جستجوی تأمین‌کننده */
export const SUPPLIER_SEARCH_MAX_RESULTS = 30

export class SuppliersApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message)
    this.name = 'SuppliersApiError'
  }
}

/**
 * جستجوی تأمین‌کنندگان بر اساس عنوان. حداقل دو نویسه لازم است و
 * حداکثر ۳۰ ردیف برمی‌گردد تا کل فهرست از سرور دریافت نشود.
 */
export function searchSuppliers(
  parameters: SupplierSearchParameters,
): Promise<SupplierOption[]> {
  const query = new URLSearchParams({ search: parameters.search.trim() })
  if (parameters.limit) query.set('limit', String(parameters.limit))

  return request<SupplierOption[]>(
    `${basePath}/lookup?${query}`,
    { signal: parameters.signal },
    'جستجوی تأمین‌کنندگان انجام نشد.',
  )
}

export function getSupplierById(
  id: string,
  signal?: AbortSignal,
): Promise<SupplierOption> {
  return request<SupplierOption>(
    `${basePath}/${encodeURIComponent(id)}`,
    { signal },
    'دریافت تأمین‌کننده انجام نشد.',
  )
}

/**
 * دریافت کل فهرست تأمین‌کنندگان. فقط برای فیلترهای جدول استفاده می‌شود
 * که گزینه‌هایشان از پیش بارگذاری می‌شوند؛ فرم‌ها باید از جستجو استفاده کنند.
 */
export async function getAllSuppliers(
  signal?: AbortSignal,
): Promise<SupplierOption[]> {
  const response = await getMasterDataList(basePath, {
    paginationEnabled: false,
    pageSize: 100,
    signal,
  })

  return response.items.map(item => ({
    id: item.id,
    title: item.title ?? '',
    description: item.description ?? null,
  }))
}

interface ValidationProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function request<T>(
  path: string,
  init: RequestInit,
  fallbackMessage: string,
): Promise<T> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new SuppliersApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
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
    throw new SuppliersApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) expireSession()

  let payload: ApiResult<T> | ValidationProblemDetails | null = null
  try {
    payload = (await response.json()) as ApiResult<T> | ValidationProblemDetails
  } catch {
    // A status-specific fallback is returned below.
  }

  const result = payload as ApiResult<T> | null
  if (response.ok && result?.isSuccess && result.data !== null) {
    return result.data as T
  }

  const problem = payload as ValidationProblemDetails | null
  const message =
    result?.messages?.find(Boolean) ??
    Object.values(problem?.errors ?? {}).flat().find(Boolean) ??
    problem?.detail ??
    problem?.title ??
    (response.status === 403 ? 'شما مجوز انجام این عملیات را ندارید.' : fallbackMessage)

  throw new SuppliersApiError(message, response.status)
}
