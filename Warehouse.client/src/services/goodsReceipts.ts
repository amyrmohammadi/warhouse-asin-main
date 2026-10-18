import type { ApiResult } from '@/services/auth'
import { clearSession, getSession } from '@/services/auth'

const basePath = '/endpoint/inventory/goods-receipts'

export type InvoiceStatus = 'DoesNotHave' | 'HasInvoice'

export interface GoodsReceiptItem {
  id: string
  productId: string
  productTitle: string
  productCode: string
  unitOfMeasureTitle: string
  quantity: number
  requestNumber: string | null
  description: string | null
}

export interface GoodsReceipt {
  id: string
  enteredAtUtc: string
  description: string | null
  delivererId: string
  delivererFullName: string
  warehouseId: string
  warehouseTitle: string
  invoiceStatus: InvoiceStatus
  invoiceNumber: string | null
  supplierId: string | null
  counterparty: string | null
  receiptNumber: string | null
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
  invoiceStatus: InvoiceStatus
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

export interface GoodsReceiptOptions {
  people: Array<{ id: string; fullName: string; phoneNumber: string }>
  warehouses: Array<{ id: string; title: string; description: string | null }>
  products: Array<{
    id: string
    title: string
    code: string
    unitOfMeasureId: string
    unitOfMeasureTitle: string
    description: string | null
  }>
  suppliers: Array<{ id: string; title: string; description: string | null }>
  unitsOfMeasure: Array<{ id: string; title: string; description: string | null }>
}

export interface GoodsReceiptListParameters {
  pageNumber?: number
  pageSize?: number
  search?: string
  sortBy?: string
  sortDescending?: boolean
  signal?: AbortSignal
}

export interface CreateGoodsReceiptItemRequest {
  productId: string | null
  productTitle: string | null
  productCode: string | null
  unitOfMeasureId: string | null
  quantity: number
  requestNumber: string | null
  description: string | null
}

export interface CreateGoodsReceiptRequest {
  enteredAtUtc: string
  description: string | null
  delivererId: string | null
  delivererFullName: string | null
  warehouseId: string
  invoiceStatus: InvoiceStatus
  invoiceNumber: string | null
  supplierId: string | null
  supplierTitle: string | null
  receiptNumber: string | null
  pageNumber: string
  items: CreateGoodsReceiptItemRequest[]
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

export function getGoodsReceipts(
  parameters: GoodsReceiptListParameters = {},
): Promise<GoodsReceiptListResponse> {
  const query = new URLSearchParams({
    pageNumber: String(parameters.pageNumber ?? 1),
    pageSize: String(parameters.pageSize ?? 10),
  })
  if (parameters.search) query.set('search', parameters.search)
  if (parameters.sortBy) query.set('sortBy', parameters.sortBy)
  if (parameters.sortDescending !== undefined) {
    query.set('sortDescending', String(parameters.sortDescending))
  }
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
  if (parameters.search) query.set('search', parameters.search)
  if (parameters.sortBy) query.set('sortBy', parameters.sortBy)
  if (parameters.sortDescending !== undefined) {
    query.set('sortDescending', String(parameters.sortDescending))
  }
  return request<GoodsReceiptItemRowListResponse>(`${basePath}/items?${query}`, {
    signal: parameters.signal,
  }, 'دریافت ردیف‌های کالای ورودی انجام نشد.')
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
