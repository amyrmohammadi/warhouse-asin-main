import type { ApiResult } from '@/services/auth'
import { clearSession, getSession } from '@/services/auth'

const basePath = '/endpoint/master-data/goods-issue-destinations'

export interface GoodsIssueDestinationOption {
  id: string
  title: string
  description: string | null
}

export interface GoodsIssueDestinationSearchParameters {
  search: string
  limit?: number
  signal?: AbortSignal
}

export const GOODS_ISSUE_DESTINATION_SEARCH_MIN_LENGTH = 2
export const GOODS_ISSUE_DESTINATION_SEARCH_MAX_RESULTS = 30

export class GoodsIssueDestinationsApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message)
    this.name = 'GoodsIssueDestinationsApiError'
  }
}

export function searchGoodsIssueDestinations(
  parameters: GoodsIssueDestinationSearchParameters,
): Promise<GoodsIssueDestinationOption[]> {
  const query = new URLSearchParams({ search: parameters.search.trim() })
  if (parameters.limit) query.set('limit', String(parameters.limit))
  return request<GoodsIssueDestinationOption[]>(
    `${basePath}/lookup?${query}`,
    { signal: parameters.signal },
    'جستجوی مقصدها انجام نشد.',
  )
}

export function getGoodsIssueDestinationById(
  id: string,
  signal?: AbortSignal,
): Promise<GoodsIssueDestinationOption> {
  return request<GoodsIssueDestinationOption>(
    `${basePath}/${encodeURIComponent(id)}`,
    { signal },
    'دریافت مقصد انجام نشد.',
  )
}

interface ValidationProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function request<T>(path: string, init: RequestInit, fallbackMessage: string): Promise<T> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new GoodsIssueDestinationsApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch(path, {
      ...init,
      headers: { ...init.headers, Authorization: `Bearer ${session.accessToken}` },
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new GoodsIssueDestinationsApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) clearSession()
  let payload: ApiResult<T> | ValidationProblemDetails | null = null
  try { payload = await response.json() as ApiResult<T> | ValidationProblemDetails } catch { /* پاسخ بدون بدنه */ }
  const result = payload as ApiResult<T> | null
  if (response.ok && result?.isSuccess && result.data !== null) return result.data

  const problem = payload as ValidationProblemDetails | null
  const message = result?.messages?.find(Boolean)
    ?? Object.values(problem?.errors ?? {}).flat().find(Boolean)
    ?? problem?.detail
    ?? problem?.title
    ?? (response.status === 403 ? 'شما مجوز مشاهده مقصدها را ندارید.' : fallbackMessage)
  throw new GoodsIssueDestinationsApiError(message, response.status)
}
