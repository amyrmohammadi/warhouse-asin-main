import { expireSession, getSession, type ApiResult } from '@/services/auth'

export interface StockWarehouse { id: string; title: string }
export interface WarehouseStock {
  productId: string
  productCode: string
  productTitle: string
  unitOfMeasure: string
  warehouseId: string
  openingQuantity: number | null
  currentQuantity: number
  rowVersion: string | null
}
export interface WarehouseStockPage { items: WarehouseStock[]; totalCount: number; filteredCount: number }
export interface StockListParameters {
  pageNumber?: number
  pageSize?: number
  search?: string
  hasStock?: boolean
  sortBy?: string
  sortDescending?: boolean
  productId?: string
  signal?: AbortSignal
}
export class WarehouseStocksApiError extends Error {
  constructor(message: string, public readonly status: number) { super(message) }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<{ data: T; messages: string[] }> {
  const session = getSession()
  if (!session?.accessToken) throw new WarehouseStocksApiError('نشست شما منقضی شده است.', 401)
  let response: Response
  try { response = await fetch(path, { ...init, headers: { ...init.headers, Authorization: `Bearer ${session.accessToken}` } }) }
  catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new WarehouseStocksApiError('ارتباط با سرویس موجودی برقرار نشد؛ دوباره تلاش کنید.', 0)
  }
  if (response.status === 401) expireSession()
  let payload: ApiResult<T> & { detail?: string; title?: string; errors?: Record<string, string[]> }
  try { payload = await response.json() }
  catch { throw new WarehouseStocksApiError('پاسخ سرویس موجودی معتبر نیست؛ دوباره تلاش کنید.', response.status) }
  if (!response.ok || !payload.isSuccess) {
    const validation = Object.values(payload.errors ?? {}).flat().find(Boolean)
    throw new WarehouseStocksApiError(response.status === 409
      ? 'موجودی این کالا در زمان ویرایش تغییر کرده است. لطفاً اطلاعات را مجدداً بررسی کنید.'
      : payload.messages?.find(Boolean) ?? validation ?? payload.detail ?? payload.title
        ?? (response.status === 403 ? 'شما مجوز انجام این عملیات را ندارید.' : 'ارتباط با سرویس موجودی برقرار نشد.'), response.status)
  }
  return { data: payload.data as T, messages: payload.messages ?? [] }
}

export async function getStockWarehouses(signal?: AbortSignal): Promise<StockWarehouse[]> {
  return (await request<StockWarehouse[]>('/endpoint/master-data/warehouses/options', { signal })).data
}
export async function getWarehouseStocks(warehouseId: string, parameters: StockListParameters = {}): Promise<WarehouseStockPage> {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(parameters))
    if (key !== 'signal' && value !== undefined && value !== '') query.set(key, String(value))
  return (await request<WarehouseStockPage>(`/endpoint/inventory/warehouses/${encodeURIComponent(warehouseId)}/stocks?${query}`, { signal: parameters.signal })).data
}
export async function saveOpeningBalances(warehouseId: string, items: Array<{ productId: string; quantity: number }>): Promise<string> {
  const result = await request<null>('/endpoint/inventory/opening-balances', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ warehouseId, items }),
  })
  return result.messages[0] ?? 'موجودی اولیه با موفقیت ثبت شد.'
}
export async function adjustWarehouseStock(stock: WarehouseStock, newQuantity: number, reason: string): Promise<string> {
  const result = await request<null>('/endpoint/inventory/adjustments', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ warehouseId: stock.warehouseId, productId: stock.productId, newQuantity,
      expectedQuantity: stock.currentQuantity, rowVersion: stock.rowVersion, reason }),
  })
  return result.messages[0] ?? 'موجودی کالا با موفقیت اصلاح شد.'
}
