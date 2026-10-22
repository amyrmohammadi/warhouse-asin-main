import {
  importMasterDataFile,
  createMasterDataItem,
  deleteMasterDataItem,
  getMasterDataList,
  getMasterDataValidationRules,
  type MasterDataListParameters,
  type MasterDataValidationRules,
  type SaveMasterDataRequest,
  updateMasterDataItem,
} from '@/services/masterData'
import type { ApiResult } from '@/services/auth'
import { clearSession, getSession } from '@/services/auth'

const basePath = '/endpoint/master-data/products'

export interface Product {
  id: string
  title: string
  code: string
  unitOfMeasureId: string
  unitOfMeasureTitle: string
  description: string | null
}

export interface ProductSearchParameters {
  search: string
  limit?: number
  signal?: AbortSignal
}

/** حداقل تعداد نویسه لازم برای جستجوی کالا */
export const PRODUCT_SEARCH_MIN_LENGTH = 2

/** حداکثر تعداد ردیف بازگشتی از جستجوی کالا */
export const PRODUCT_SEARCH_MAX_RESULTS = 30

export class ProductsApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message)
    this.name = 'ProductsApiError'
  }
}

export interface ProductListResponse {
  items: Product[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
  paginationEnabled: boolean
}

export interface ProductListParameters extends MasterDataListParameters {
  title?: string
  code?: string
  unitOfMeasureTitle?: string
  description?: string
}

export interface SaveProductRequest extends SaveMasterDataRequest {
  title: string
  code: string
  unitOfMeasureTitle: string
  description: string | null
}

export type ProductValidationRules = MasterDataValidationRules

export async function getProducts(
  parameters: ProductListParameters = {},
): Promise<ProductListResponse> {
  const result = await getMasterDataList(basePath, {
    ...parameters,
    filters: {
      ...parameters.filters,
      title: parameters.title,
      code: parameters.code,
      unitOfMeasureTitle: parameters.unitOfMeasureTitle,
      description: parameters.description,
    },
  })

  return {
    ...result,
    items: result.items as unknown as Product[],
  }
}

export function getProductValidationRules(
  signal?: AbortSignal,
): Promise<ProductValidationRules> {
  return getMasterDataValidationRules(basePath, signal)
}

export async function createProduct(
  payload: SaveProductRequest,
  signal?: AbortSignal,
): Promise<Product> {
  return (await createMasterDataItem(basePath, payload, signal)) as unknown as Product
}

export async function updateProduct(
  id: string,
  payload: SaveProductRequest,
  signal?: AbortSignal,
): Promise<Product> {
  return (await updateMasterDataItem(basePath, id, payload, signal)) as unknown as Product
}

export function deleteProduct(id: string, signal?: AbortSignal): Promise<void> {
  return deleteMasterDataItem(basePath, id, signal)
}

export function importProducts(file: File, checkOnlyProductCode = false, signal?: AbortSignal) {
  return importMasterDataFile(basePath, file, signal, {
    CheckOnlyProductCode: String(checkOnlyProductCode),
  })
}

/**
 * جستجوی کالا بر اساس عنوان، کد یا واحد اندازه‌گیری. حداقل دو نویسه لازم است و
 * حداکثر ۳۰ ردیف برمی‌گردد تا کل فهرست از سرور دریافت نشود.
 */
export function searchProducts(
  parameters: ProductSearchParameters,
): Promise<Product[]> {
  const query = new URLSearchParams({ search: parameters.search.trim() })
  if (parameters.limit) query.set('limit', String(parameters.limit))

  return request<Product[]>(
    `${basePath}/lookup?${query}`,
    { signal: parameters.signal },
    'جستجوی کالاها انجام نشد.',
  )
}

export function getProductById(
  id: string,
  signal?: AbortSignal,
): Promise<Product> {
  return request<Product>(
    `${basePath}/${encodeURIComponent(id)}`,
    { signal },
    'دریافت کالا انجام نشد.',
  )
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
    throw new ProductsApiError('نشست شما منقضی شده است. لطفاً دوباره وارد شوید.', 401)
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
    throw new ProductsApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) clearSession()

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

  throw new ProductsApiError(message, response.status)
}
