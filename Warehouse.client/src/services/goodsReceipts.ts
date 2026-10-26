import type { ApiResult } from '@/services/auth'
import { expireSession, getSession } from '@/services/auth'
import type { DateFilterValue, NumericFilterValue, TextFilterValue } from '@/types/dataTableFilters'

const basePath = '/endpoint/inventory/goods-receipts'

export type InvoiceStatus = 'DoesNotHave' | 'HasInvoice'
export type InvoiceStatusFilter = InvoiceStatus | 'NotEntered'

export interface GoodsReceiptItem {
  id: string
  productId: string
  productTitle: string
  productCode: string
  unitOfMeasureId: string
  unitOfMeasureTitle: string
  warehouseId: string
  warehouseTitle: string
  invoiceStatus: InvoiceStatus | null
  invoiceNumber: string | null
  supplierId: string | null
  counterparty: string | null
  receiptNumber: string | null
  quantity: number
  requestNumber: string | null
  description: string | null
}

export interface GoodsReceipt {
  id: string
  concurrencyStamp: string
  enteredAtUtc: string
  description: string | null
  delivererId: string
  delivererFullName: string
  pageNumber: string
  items: GoodsReceiptItem[]
}

export interface GoodsReceiptListResponse {
  items: GoodsReceipt[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
}

export interface GoodsReceiptItemRow {
  id: string
  goodsReceiptId: string
  productId: string
  productTitle: string
  productCode: string
  unitOfMeasureTitle: string
  quantity: number
  requestNumber: string | null
  itemDescription: string | null
  enteredAtUtc: string
  receiptDescription: string | null
  delivererFullName: string
  warehouseTitle: string
  invoiceStatus: InvoiceStatus | null
  invoiceNumber: string | null
  supplierId: string | null
  counterparty: string | null
  receiptNumber: string | null
  pageNumber: string
}

export interface GoodsReceiptItemRowListResponse {
  items: GoodsReceiptItemRow[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
}

export interface EntityHistoryChange {
  fieldName: string
  fieldLabel: string
  oldValue: string
  newValue: string
}

export interface EntityHistoryEntry {
  id: number
  action: 'Create' | 'Update' | 'Delete'
  actionLabel: string
  sectionLabel: string
  userFullName: string
  occurredAtUtc: string
  changes: EntityHistoryChange[]
}

export interface GoodsReceiptItemHistory {
  itemId: string
  itemTitle: string
  entries: EntityHistoryEntry[]
}

export interface GoodsReceiptOptions {
  people: Array<{ id: string; concurrencyStamp: string; fullName: string; phoneNumber: string }>
  warehouses: Array<{ id: string; concurrencyStamp: string; title: string; description: string | null }>
  unitsOfMeasure: Array<{ id: string; concurrencyStamp: string; title: string; description: string | null }>
}

export interface GoodsReceiptListParameters {
  pageNumber?: number
  pageSize?: number
  search?: string
  enteredAt?: DateFilterValue
  productTitle?: TextFilterValue
  productCode?: TextFilterValue
  pageNumberFilter?: TextFilterValue
  quantity?: NumericFilterValue
  invoiceNumber?: TextFilterValue
  receiptNumber?: TextFilterValue
  warehouseId?: string
  delivererId?: string
  supplierId?: string
  invoiceStatus?: InvoiceStatusFilter
  sortBy?: string
  sortDescending?: boolean
  signal?: AbortSignal
}

export interface CreateGoodsReceiptItemRequest {
  productId: string | null
  productTitle: string | null
  productCode: string | null
  unitOfMeasureId: string | null
  warehouseId: string
  invoiceStatus: InvoiceStatus | null
  invoiceNumber: string | null
  supplierId: string | null
  supplierTitle: string | null
  receiptNumber: string | null
  quantity: number
  requestNumber: string | null
  description: string | null
}

export interface CreateGoodsReceiptRequest {
  enteredAtUtc: string
  description: string | null
  delivererId: string | null
  delivererFullName: string | null
  pageNumber: string
  items: CreateGoodsReceiptItemRequest[]
  concurrencyStamp: string | null
}

interface ValidationProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class GoodsReceiptsApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message)
    this.name = 'GoodsReceiptsApiError'
  }
}

export interface GoodsReceiptImportSummary {
  processedRowCount: number
  importedReceiptCount: number
  importedItemCount: number
  createdDelivererCount: number
  invalidRows: Array<{ rowNumber: number; message: string }>
}

export function importGoodsReceipts(file: File, signal?: AbortSignal): Promise<GoodsReceiptImportSummary> {
  const body = new FormData()
  body.append('File', file)
  return request<GoodsReceiptImportSummary>(`${basePath}/import`, { method: 'POST', body, signal }, 'ایمپورت ورود کالا انجام نشد.')
}

export function getGoodsReceipts(
  parameters: GoodsReceiptListParameters = {},
): Promise<GoodsReceiptListResponse> {
  const query = new URLSearchParams({
    pageNumber: String(parameters.pageNumber ?? 1),
    pageSize: String(parameters.pageSize ?? 10),
  })
  appendListParameters(query, parameters)
  return request<GoodsReceiptListResponse>(`${basePath}?${query}`, {
    signal: parameters.signal,
  }, 'دریافت تاریخچه ورود کالا انجام نشد.')
}

export function getGoodsReceiptItems(
  parameters: GoodsReceiptListParameters = {},
): Promise<GoodsReceiptItemRowListResponse> {
  const query = new URLSearchParams({
    pageNumber: String(parameters.pageNumber ?? 1),
    pageSize: String(parameters.pageSize ?? 10),
  })
  appendListParameters(query, parameters)
  return request<GoodsReceiptItemRowListResponse>(`${basePath}/items?${query}`, {
    signal: parameters.signal,
  }, 'دریافت ردیف‌های کالای ورودی انجام نشد.')
}

export function getGoodsReceiptItemHistory(
  itemId: string,
  signal?: AbortSignal,
): Promise<GoodsReceiptItemHistory> {
  return request<GoodsReceiptItemHistory>(
    `${basePath}/items/${encodeURIComponent(itemId)}/history`,
    { signal },
    'دریافت تاریخچه تغییرات کالا انجام نشد.',
  )
}

export async function exportGoodsReceiptItems(
  parameters: GoodsReceiptListParameters = {},
): Promise<Blob> {
  const query = new URLSearchParams()
  appendListParameters(query, parameters)
  const session = getSession()
  if (!session?.accessToken) {
    throw new GoodsReceiptsApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch(`${basePath}/items/export?${query}`, {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      signal: parameters.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new GoodsReceiptsApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) expireSession()
  if (response.ok) return response.blob()

  let message = response.status === 403
    ? 'شما مجوز دریافت خروجی را ندارید.'
    : 'دریافت خروجی Excel انجام نشد.'
  try {
    const payload = await response.json() as ApiResult<unknown> | ValidationProblemDetails
    const result = payload as ApiResult<unknown>
    const problem = payload as ValidationProblemDetails
    message = result.messages?.find(Boolean)
      ?? Object.values(problem.errors ?? {}).flat().find(Boolean)
      ?? problem.detail
      ?? problem.title
      ?? message
  } catch {
    // از پیام متناسب با وضعیت HTTP استفاده می‌شود.
  }

  throw new GoodsReceiptsApiError(message, response.status)
}

function appendListParameters(
  query: URLSearchParams,
  parameters: GoodsReceiptListParameters,
): void {
  const values: Array<[string, string | undefined]> = [
    ['search', parameters.search],
    ['warehouseId', parameters.warehouseId],
    ['delivererId', parameters.delivererId],
    ['supplierId', parameters.supplierId],
    ['invoiceStatus', parameters.invoiceStatus],
    ['sortBy', parameters.sortBy],
  ]
  values.forEach(([key, value]) => {
    if (value) query.set(key, value)
  })
  appendDateFilter(query, 'enteredAt', parameters.enteredAt)
  appendTextFilter(query, 'productTitle', parameters.productTitle)
  appendTextFilter(query, 'productCode', parameters.productCode)
  appendTextFilter(query, 'pageNumberFilter', parameters.pageNumberFilter)
  appendNumericFilter(query, 'quantity', parameters.quantity)
  appendTextFilter(query, 'invoiceNumber', parameters.invoiceNumber)
  appendTextFilter(query, 'receiptNumber', parameters.receiptNumber)
  if (parameters.sortDescending !== undefined) {
    query.set('sortDescending', String(parameters.sortDescending))
  }
}

function appendTextFilter(
  query: URLSearchParams,
  key: string,
  filter: TextFilterValue | undefined,
): void {
  if (!filter?.value.trim()) return
  query.set(`${key}.operator`, filter.operator)
  query.set(`${key}.value`, filter.value.trim())
}

function appendNumericFilter(
  query: URLSearchParams,
  key: string,
  filter: NumericFilterValue | undefined,
): void {
  if (!filter || filter.value === null) return
  query.set(`${key}.operator`, filter.operator)
  query.set(`${key}.value`, String(filter.value))
  if (filter.operator === 'between' && filter.valueTo !== null && filter.valueTo !== undefined) {
    query.set(`${key}.valueTo`, String(filter.valueTo))
  }
}

function appendDateFilter(
  query: URLSearchParams,
  key: string,
  filter: DateFilterValue | undefined,
): void {
  if (!filter?.value) return
  query.set(`${key}.operator`, filter.operator)
  query.set(`${key}.value`, filter.value)
  query.set(`${key}.utcOffsetMinutes`, String(-new Date().getTimezoneOffset()))
  if (filter.operator === 'between' && filter.valueTo) {
    query.set(`${key}.valueTo`, filter.valueTo)
  }
}

export function getGoodsReceiptOptions(signal?: AbortSignal): Promise<GoodsReceiptOptions> {
  return request<GoodsReceiptOptions>(`${basePath}/options`, { signal }, 'دریافت اطلاعات فرم انجام نشد.')
}

export function createGoodsReceipt(
  payload: CreateGoodsReceiptRequest,
  signal?: AbortSignal,
): Promise<GoodsReceipt> {
  return request<GoodsReceipt>(basePath, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
    signal,
  }, 'ثبت ورود کالا انجام نشد.')
}

export function getGoodsReceipt(id: string, signal?: AbortSignal): Promise<GoodsReceipt> {
  return request<GoodsReceipt>(`${basePath}/${encodeURIComponent(id)}`, { signal }, 'دریافت سند ورود کالا انجام نشد.')
}

export function updateGoodsReceipt(id: string, payload: CreateGoodsReceiptRequest, signal?: AbortSignal): Promise<GoodsReceipt> {
  return request<GoodsReceipt>(`${basePath}/${encodeURIComponent(id)}`, {
    method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload), signal,
  }, 'ویرایش سند ورود کالا انجام نشد.')
}

export async function deleteGoodsReceipt(id: string, signal?: AbortSignal): Promise<void> {
  await request<unknown>(`${basePath}/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    signal,
  }, 'حذف سند ورود کالا انجام نشد.', false)
}

async function request<T>(
  path: string,
  init: RequestInit,
  fallbackMessage: string,
  requireData = true,
): Promise<T> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new GoodsReceiptsApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
  }

  let response: Response
  try {
    response = await fetch(path, {
      ...init,
      headers: { ...init.headers, Authorization: `Bearer ${session.accessToken}` },
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new GoodsReceiptsApiError('ارتباط با سرور برقرار نشد.', 0)
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
  const fieldErrors = Object.fromEntries(
    Object.entries(problem?.errors ?? {}).map(([key, messages]) => [
      key.length ? key[0].toLowerCase() + key.slice(1) : key,
      messages,
    ]),
  )
  const message = result?.messages?.find(Boolean)
    ?? Object.values(fieldErrors).flat().find(Boolean)
    ?? problem?.detail
    ?? problem?.title
    ?? (response.status === 403 ? 'شما مجوز انجام این عملیات را ندارید.' : fallbackMessage)
  throw new GoodsReceiptsApiError(message, response.status, fieldErrors)
}
