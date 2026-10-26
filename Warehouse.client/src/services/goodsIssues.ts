import type { ApiResult } from '@/services/auth'
import { clearSession, getSession } from '@/services/auth'
import type { DateFilterValue, NumericFilterValue, TextFilterValue } from '@/types/dataTableFilters'

export interface GoodsIssueItem {
  id: string
  productId: string | null
  productTitle: string | null
  productUnitOfMeasureTitle: string | null
  productCode: string
  quantity: number
  assetNumber: string | null
  destinationId: string
  destinationTitle: string
  exitReason: string
  description: string | null
  deliveringUnit: string
  returnAtUtc: string | null
}

export interface GoodsIssue {
  id: string
  concurrencyStamp: string
  issueNumber: string
  issuedAtUtc: string
  description: string | null
  issuerId: string
  issuerFullName: string
  items: GoodsIssueItem[]
}

export interface GoodsIssueItemRow extends GoodsIssueItem {
  goodsIssueId: string
  issueNumber: string
  issuedAtUtc: string
  issueDescription: string | null
  issuerFullName: string
}

export interface GoodsIssueList<T> {
  items: T[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
}

export interface GoodsIssueOptions {
  people: Array<{ id: string; fullName: string; phoneNumber: string }>
}

export interface SaveGoodsIssueItem {
  productId: string | null
  productCode: string
  quantity: number
  assetNumber: string | null
  destinationId: string | null
  destinationTitle: string | null
  exitReason: string
  description: string | null
  deliveringUnit: string
  returnAtUtc: string | null
}

export interface SaveGoodsIssueRequest {
  issueNumber: string
  issuedAtUtc: string
  description: string | null
  issuerId: string | null
  issuerFullName: string | null
  items: SaveGoodsIssueItem[]
  concurrencyStamp: string | null
}

export interface GoodsIssueListFilters {
  issuedAt?: DateFilterValue
  issueNumberFilter?: TextFilterValue
  productTitle?: TextFilterValue
  productCode?: TextFilterValue
  quantity?: NumericFilterValue
  assetNumber?: TextFilterValue
  destinationId?: string
  deliveringUnit?: TextFilterValue
  issuerId?: string
}

export interface GoodsIssueListParameters extends GoodsIssueListFilters {
  pageNumber?: number
  pageSize?: number
  search?: string
  sortBy?: string
  sortDescending?: boolean
  signal?: AbortSignal
}

export class GoodsIssuesApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message)
    this.name = 'GoodsIssuesApiError'
  }
}

export function getGoodsIssues(
  mode: 'issues' | 'items',
  parameters: GoodsIssueListParameters = {},
): Promise<GoodsIssueList<GoodsIssue | GoodsIssueItemRow>> {
  const query = new URLSearchParams({
    pageNumber: String(parameters.pageNumber ?? 1),
    pageSize: String(parameters.pageSize ?? 10),
  })
  appendListParameters(query, parameters)
  const path = mode === 'items' ? '/endpoint/inventory/goods-issues/items' : '/endpoint/inventory/goods-issues'
  return request(`${path}?${query}`, { signal: parameters.signal }, 'دریافت تاریخچه خروج کالا انجام نشد.')
}

export function getGoodsIssue(id: string, signal?: AbortSignal): Promise<GoodsIssue> {
  return request(`/endpoint/inventory/goods-issues/${encodeURIComponent(id)}`, { signal }, 'دریافت سند خروج انجام نشد.')
}

export function getGoodsIssueOptions(signal?: AbortSignal): Promise<GoodsIssueOptions> {
  return request('/endpoint/inventory/goods-issues/options', { signal }, 'دریافت اطلاعات فرم انجام نشد.')
}

export function createGoodsIssue(payload: SaveGoodsIssueRequest, signal?: AbortSignal): Promise<GoodsIssue> {
  return request('/endpoint/inventory/goods-issues', jsonRequest('POST', payload, signal), 'ثبت خروج کالا انجام نشد.')
}

export function updateGoodsIssue(id: string, payload: SaveGoodsIssueRequest, signal?: AbortSignal): Promise<GoodsIssue> {
  return request(`/endpoint/inventory/goods-issues/${encodeURIComponent(id)}`, jsonRequest('PUT', payload, signal), 'ویرایش خروج کالا انجام نشد.')
}

export async function deleteGoodsIssue(id: string, signal?: AbortSignal): Promise<void> {
  await request(`/endpoint/inventory/goods-issues/${encodeURIComponent(id)}`, { method: 'DELETE', signal }, 'حذف خروج کالا انجام نشد.', false)
}

export async function exportGoodsIssueItems(parameters: GoodsIssueListParameters = {}): Promise<Blob> {
  const query = new URLSearchParams()
  appendListParameters(query, parameters)
  const response = await authorizedFetch(`/endpoint/inventory/goods-issues/items/export?${query}`, {
    signal: parameters.signal,
  })
  if (!response.ok) throw new GoodsIssuesApiError('دریافت فایل خروجی انجام نشد.', response.status)
  return response.blob()
}

function appendListParameters(query: URLSearchParams, parameters: GoodsIssueListParameters): void {
  if (parameters.search?.trim()) query.set('search', parameters.search.trim())
  if (parameters.sortBy) query.set('sortBy', parameters.sortBy)
  if (parameters.sortDescending !== undefined) query.set('sortDescending', String(parameters.sortDescending))
  appendFilters(query, parameters)
}

function appendFilters(query: URLSearchParams, filters: GoodsIssueListFilters): void {
  if (filters.destinationId) query.set('destinationId', filters.destinationId)
  if (filters.issuerId) query.set('issuerId', filters.issuerId)
  appendDateFilter(query, 'issuedAt', filters.issuedAt)
  appendTextFilter(query, 'issueNumberFilter', filters.issueNumberFilter)
  appendTextFilter(query, 'productTitle', filters.productTitle)
  appendTextFilter(query, 'productCode', filters.productCode)
  appendNumericFilter(query, 'quantity', filters.quantity)
  appendTextFilter(query, 'assetNumber', filters.assetNumber)
  appendTextFilter(query, 'deliveringUnit', filters.deliveringUnit)
}

function appendTextFilter(query: URLSearchParams, key: string, filter?: TextFilterValue): void {
  if (!filter?.value.trim()) return
  query.set(`${key}.operator`, filter.operator)
  query.set(`${key}.value`, filter.value.trim())
}

function appendNumericFilter(query: URLSearchParams, key: string, filter?: NumericFilterValue): void {
  if (!filter || filter.value === null) return
  query.set(`${key}.operator`, filter.operator)
  query.set(`${key}.value`, String(filter.value))
  if (filter.operator === 'between' && filter.valueTo !== null && filter.valueTo !== undefined)
    query.set(`${key}.valueTo`, String(filter.valueTo))
}

function appendDateFilter(query: URLSearchParams, key: string, filter?: DateFilterValue): void {
  if (!filter?.value) return
  query.set(`${key}.operator`, filter.operator)
  query.set(`${key}.value`, filter.value)
  query.set(`${key}.utcOffsetMinutes`, String(-new Date().getTimezoneOffset()))
  if (filter.operator === 'between' && filter.valueTo) query.set(`${key}.valueTo`, filter.valueTo)
}

function jsonRequest(method: string, payload: SaveGoodsIssueRequest, signal?: AbortSignal): RequestInit {
  return { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload), signal }
}

async function authorizedFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const session = getSession()
  if (!session?.accessToken) throw new GoodsIssuesApiError('نشست شما منقضی شده است.', 401)
  try {
    const response = await fetch(path, { ...init, headers: { ...init.headers, Authorization: `Bearer ${session.accessToken}` } })
    if (response.status === 401) clearSession()
    return response
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new GoodsIssuesApiError('ارتباط با سرور برقرار نشد.', 0)
  }
}

async function request<T>(path: string, init: RequestInit, fallback: string, requireData = true): Promise<T> {
  const response = await authorizedFetch(path, init)
  let payload: ApiResult<T> | { title?: string; detail?: string; errors?: Record<string, string[]> } | null = null
  try { payload = await response.json() } catch { /* پاسخ بدون بدنه */ }
  const result = payload as ApiResult<T> | null
  if (response.ok && result?.isSuccess && (!requireData || result.data !== null)) return result.data as T
  const problem = payload as { title?: string; detail?: string; errors?: Record<string, string[]> } | null
  const fieldErrors = Object.fromEntries(
    Object.entries(problem?.errors ?? {}).map(([key, messages]) => [
      key.length ? key[0].toLowerCase() + key.slice(1) : key,
      messages,
    ]),
  )
  const validationMessage = Object.values(fieldErrors).flat().find(Boolean)
  throw new GoodsIssuesApiError(
    result?.messages?.find(Boolean)
      ?? validationMessage
      ?? problem?.detail
      ?? problem?.title
      ?? (response.status === 403 ? 'شما مجوز انجام این عملیات را ندارید.' : fallback),
    response.status,
    fieldErrors,
  )
}
