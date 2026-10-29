import type { ApiResult } from '@/services/auth'
import { expireSession, getSession } from '@/services/auth'
import type { TextFilterValue } from '@/types/dataTableFilters'

export interface InvoiceSummary {
  price: number | null
  id: string
  invoiceNumber: string
  description: string | null
  invoiceDate: string | null
  processingStatus: InvoiceProcessingStatus
  counterparty: string | null
  receiptNumbers: string[]
  itemsWithoutReceiptCount?: number | null
}

export type InvoiceProcessingStatus = 'UnderReview' | 'SentToFinance' | 'FinanceApproved'

export interface InvoiceItem {
  price: number | null
  id: string
  goodsReceiptId: string
  goodsReceiptConcurrencyStamp: string
  productTitle: string
  productCode: string
  unitOfMeasureTitle: string
  quantity: number
  warehouseTitle: string
  enteredAtUtc: string
  pageNumber: string
  receiptNumber: string | null
  description: string | null
}

export interface InvoiceDetails extends InvoiceSummary {
  supplierId: string | null
  items: InvoiceItem[]
}

export interface UpdateInvoiceItemRequest {
  receiptNumber: string | null
  concurrencyStamp: string
}

export interface UpdateInvoiceRequest {
  price: number | null
  invoiceNumber: string
  supplierId: string | null
  description: string | null
  invoiceDate: string | null
  processingStatus: InvoiceProcessingStatus
  items: Array<{
    price: number | null
    id: string
    receiptNumber: string | null
    goodsReceiptConcurrencyStamp: string
  }>
}

export interface UpdateInvoiceItemResult {
  id: string
  receiptNumber: string | null
  concurrencyStamp: string
}

export interface InvoiceListResult {
  items: InvoiceSummary[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
}

export interface InvoiceListParameters {
  pageNumber?: number
  pageSize?: number
  search?: string
  sortBy?: string
  sortDescending?: boolean
  invoiceNumber?: TextFilterValue
  counterparty?: TextFilterValue
  signal?: AbortSignal
}

export class InvoicesApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message)
    this.name = 'InvoicesApiError'
  }
}

export function getInvoices(parameters: InvoiceListParameters = {}): Promise<InvoiceListResult> {
  const query = new URLSearchParams({
    pageNumber: String(parameters.pageNumber ?? 1),
    pageSize: String(parameters.pageSize ?? 50),
  })
  if (parameters.search?.trim()) query.set('search', parameters.search.trim())
  if (parameters.sortBy) query.set('sortBy', parameters.sortBy)
  if (parameters.sortDescending !== undefined)
    query.set('sortDescending', String(parameters.sortDescending))
  appendTextFilter(query, 'invoiceNumber', parameters.invoiceNumber)
  appendTextFilter(query, 'counterparty', parameters.counterparty)

  return request(
    `/endpoint/inventory/invoices?${query}`,
    { signal: parameters.signal },
    'دریافت فهرست فاکتورها انجام نشد.',
  )
}

export function getInvoice(id: string, signal?: AbortSignal): Promise<InvoiceDetails> {
  return request(
    `/endpoint/inventory/invoices/${encodeURIComponent(id)}`,
    { signal },
    'دریافت کالاهای فاکتور انجام نشد.',
  )
}

export function updateInvoiceItem(
  invoiceId: string,
  itemId: string,
  payload: UpdateInvoiceItemRequest,
  signal?: AbortSignal,
): Promise<UpdateInvoiceItemResult> {
  return request(
    `/endpoint/inventory/invoices/${encodeURIComponent(invoiceId)}/items/${encodeURIComponent(itemId)}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal,
    },
    'ذخیره شماره رسید انجام نشد.',
  )
}

export function updateInvoice(
  invoiceId: string,
  payload: UpdateInvoiceRequest,
  signal?: AbortSignal,
): Promise<InvoiceDetails> {
  return request(
    `/endpoint/inventory/invoices/${encodeURIComponent(invoiceId)}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal,
    },
    'ویرایش فاکتور انجام نشد.',
  )
}

function appendTextFilter(
  query: URLSearchParams,
  key: string,
  filter?: TextFilterValue,
): void {
  if (!filter?.value.trim()) return
  query.set(`${key}.operator`, filter.operator)
  query.set(`${key}.value`, filter.value.trim())
}

async function request<T>(path: string, init: RequestInit, fallback: string): Promise<T> {
  const session = getSession()
  if (!session?.accessToken) throw new InvoicesApiError('نشست شما منقضی شده است.', 401)

  let response: Response
  try {
    response = await fetch(path, {
      ...init,
      headers: { ...init.headers, Authorization: `Bearer ${session.accessToken}` },
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new InvoicesApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) expireSession()
  let payload: ApiResult<T> | { title?: string; detail?: string; errors?: Record<string, string[]> } | null = null
  try { payload = await response.json() as typeof payload } catch { /* پاسخ بدون بدنه */ }
  const result = payload as ApiResult<T> | null
  if (response.ok && result?.isSuccess && result.data !== null) return result.data

  const problem = payload as { title?: string; detail?: string; errors?: Record<string, string[]> } | null
  const validationMessage = Object.values(problem?.errors ?? {}).flat().find(Boolean)
  throw new InvoicesApiError(
    result?.messages?.find(Boolean)
      ?? validationMessage
      ?? problem?.detail
      ?? problem?.title
      ?? (response.status === 403 ? 'شما مجوز انجام این عملیات را ندارید.' : fallback),
    response.status,
  )
}
