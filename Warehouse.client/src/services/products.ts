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

const basePath = '/endpoint/master-data/products'

export interface Product {
  id: string
  title: string
  code: string
  unitOfMeasureId: string
  unitOfMeasureTitle: string
  description: string | null
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
