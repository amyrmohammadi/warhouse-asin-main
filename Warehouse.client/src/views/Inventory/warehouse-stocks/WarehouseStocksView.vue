<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import DataTablesCore from 'datatables.net-bs4'
import DataTable from 'datatables.net-vue3'
import BaseModal from '@/components/BaseModal.vue'
import ProductSelect from '@/components/selectors/ProductSelect.vue'
import { useDataTableList } from '@/composables/useDataTableList'
import { usePermissions } from '@/composables/usePermissions'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'
import { isAbortError } from '@/composables/useRequestChannel'
import { getStockWarehouses, getWarehouseStocks, saveOpeningBalances, adjustWarehouseStock,
  type WarehouseStock, type StockWarehouse } from '@/services/warehouseStocks'
import type { Product } from '@/services/products'
import { toast } from '@/services/toast'
import { createRowNumberColumn } from '@/utils/dataTable'
import { createInventoryTableOptions } from '@/utils/inventoryTable'
import { formatPersianNumber } from '@/utils/dateTime'
import '@/styles/inventory-list.css'

defineOptions({ name: 'WarehouseStocksView' })
DataTable.use(DataTablesCore)
const { has } = usePermissions()
const canOpen = has('WarehouseOpeningBalanceManage')
const canAdjust = has('WarehouseInventoryAdjustmentManage')
const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/warehouses')
const warehouses = ref<StockWarehouse[]>([])
const warehousesLoading = ref(false)
const warehouseError = ref('')
const warehouseSearch = ref('')
const warehouseId = ref('')
const selectedWarehouse = computed(() => warehouses.value.find(warehouse => warehouse.id === warehouseId.value))
const visibleWarehouses = computed(() => warehouses.value.filter(warehouse =>
  warehouse.id === warehouseId.value || warehouse.title.includes(warehouseSearch.value.trim())))
const search = ref('')
const hasStock = ref('all')
const openingOpen = ref(false)
const adjustmentOpen = ref(false)
const saving = ref(false)
const modalError = ref('')
const conflict = ref(false)
const adjustment = ref<WarehouseStock | null>(null)
const newQuantity = ref<number | string>(0)
const reason = ref('')
const delta = computed(() => Number(newQuantity.value) - (adjustment.value?.currentQuantity ?? 0))
let rowKey = 0
interface OpeningRow { key: number; productId: string | null; text: string; code: string; unit: string; quantity: number | string }
function newOpeningRow(): OpeningRow { return { key: ++rowKey, productId: null, text: '', code: '', unit: '', quantity: 0 } }
const openingRows = ref<OpeningRow[]>([])
const optionsController = new AbortController()
async function loadWarehouses(): Promise<void> {
  warehousesLoading.value = true
  warehouseError.value = ''
  try { warehouses.value = await getStockWarehouses(optionsController.signal) }
  catch (error) {
    if (!isAbortError(error) && !await handleUnauthorized(error)) warehouseError.value = error instanceof Error ? error.message : 'دریافت انبارها انجام نشد.'
  } finally { warehousesLoading.value = false }
}
void loadWarehouses()

const list = useDataTableList<WarehouseStock>({
  errorMessage: 'دریافت موجودی انجام نشد.', onUnauthorized: handleUnauthorized,
  fetchPage: async query => {
    const currentWarehouse = warehouseId.value
    if (!currentWarehouse) return { items: [], totalCount: 0, filteredCount: 0 }
    const result = await getWarehouseStocks(currentWarehouse, { pageNumber: query.page, pageSize: query.pageSize,
      search: search.value.trim(), hasStock: hasStock.value === 'all' ? undefined : hasStock.value === 'true',
      sortBy: query.sortBy, sortDescending: query.sortDescending, signal: query.signal })
    if (currentWarehouse !== warehouseId.value || query.signal.aborted) throw new DOMException('Aborted', 'AbortError')
    return result
  },
})
const { table, loading } = list
const tableOptions = createInventoryTableOptions({
  ajax: list.ajax,
  columns: [{ ...createRowNumberColumn(), className: 'stock-secondary-column text-center' },
    { data: 'productCode', name: 'productCode', title: 'کد کالا', className: 'stock-secondary-column' },
    { data: 'productTitle', name: 'productTitle', title: 'کالا', className: 'all' },
    { data: 'unitOfMeasure', name: 'unitOfMeasure', title: 'واحد', orderable: false, className: 'stock-secondary-column' },
    { data: 'openingQuantity', name: 'openingQuantity', title: 'موجودی اولیه', className: 'stock-secondary-column' },
    { data: 'currentQuantity', name: 'currentQuantity', title: 'موجودی فعلی', className: 'all' },
    { data: null, name: 'actions', title: 'عملیات', orderable: false, visible: canAdjust, className: 'all' }],
  order: { name: 'productTitle', dir: 'asc' }, emptyText: 'کالایی ثبت نشده است.',
  loadingText: 'در حال دریافت موجودی…', zeroText: 'کالایی با این فیلتر پیدا نشد.',
})
tableOptions.lengthMenu = [10, 20, 50, 100]
// The compact mobile cells already include code and unit; no extra detail rows are needed.
tableOptions.responsive = false
watch(warehouseId, () => { modalError.value = ''; openingOpen.value = false; adjustmentOpen.value = false })
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(search, () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => list.reload(true), 300) })
watch(hasStock, () => list.reload(true))
onBeforeUnmount(() => { optionsController.abort(); clearTimeout(searchTimer) })

function startOpening(): void {
  openingRows.value = [newOpeningRow()]
  modalError.value = ''
  openingOpen.value = true
}
function selectProduct(row: OpeningRow, product: Product): void {
  row.productId = product.id; row.text = product.title; row.code = product.code; row.unit = product.unitOfMeasureTitle
}
function startAdjustment(stock: WarehouseStock): void {
  adjustment.value = { ...stock }; newQuantity.value = stock.currentQuantity; reason.value = ''
  modalError.value = ''; conflict.value = false; adjustmentOpen.value = true
}
function validQuantity(value: number | string): boolean {
  const quantity = Number(value)
  return value !== '' && Number.isFinite(quantity) && quantity >= 0 && quantity < 1e15
    && Math.abs(quantity * 1000 - Math.round(quantity * 1000)) < 0.001
}
async function showFailure(error: unknown): Promise<void> {
  if (await handleUnauthorized(error)) return
  modalError.value = error instanceof Error ? error.message : 'ثبت اطلاعات انجام نشد.'
}
async function saveOpening(): Promise<void> {
  if (saving.value) return
  modalError.value = ''
  if (!openingRows.value.length || openingRows.value.some(row => !row.productId || !validQuantity(row.quantity))) {
    modalError.value = 'کالا و مقدار غیرمنفی با حداکثر سه رقم اعشار را در همهٔ ردیف‌ها وارد کنید.'; return
  }
  if (new Set(openingRows.value.map(row => row.productId)).size !== openingRows.value.length) {
    modalError.value = 'هر کالا را فقط یک‌بار انتخاب کنید.'; return
  }
  saving.value = true
  try {
    toast.success(await saveOpeningBalances(warehouseId.value, openingRows.value.map(row => ({ productId: row.productId!, quantity: Number(row.quantity) }))))
    openingOpen.value = false; list.reload(false)
  } catch (error) { await showFailure(error) }
  finally { saving.value = false }
}
async function saveAdjustment(): Promise<void> {
  if (saving.value || !adjustment.value) return
  modalError.value = ''
  if (!validQuantity(newQuantity.value) || !reason.value.trim() || reason.value.length > 1000) {
    modalError.value = 'موجودی غیرمنفی با حداکثر سه رقم اعشار و علت اصلاح (تا ۱۰۰۰ نویسه) وارد کنید.'; return
  }
  saving.value = true
  try {
    toast.success(await adjustWarehouseStock(adjustment.value, Number(newQuantity.value), reason.value.trim()))
    adjustmentOpen.value = false; list.reload(false)
  } catch (error) {
    conflict.value = (error as { status?: number }).status === 409
    await showFailure(error)
    if (conflict.value) list.reload(false)
  } finally { saving.value = false }
}
async function refreshAdjustment(): Promise<void> {
  if (!adjustment.value || saving.value) return
  saving.value = true
  try {
    const page = await getWarehouseStocks(adjustment.value.warehouseId, { productId: adjustment.value.productId, pageSize: 1 })
    const stock = page.items[0]
    if (!stock) throw new Error('کالا پیدا نشد؛ فهرست را تازه‌سازی کنید.')
    adjustment.value = stock; newQuantity.value = stock.currentQuantity; conflict.value = false; modalError.value = ''
  } catch (error) { await showFailure(error) }
  finally { saving.value = false }
}
</script>

<template>
  <div class="container-fluid users-page inventory-data-page stock-page">
    <div class="page-titles"><ol class="breadcrumb"><li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li><li class="breadcrumb-item active">موجودی انبارها</li></ol></div>
    <div class="card">
      <div class="card-header stock-header">
        <div><h4 class="card-title"><i class="las la-warehouse mr-1" aria-hidden="true"></i> موجودی انبارها</h4><p class="page-description text-muted mb-0 mt-1">مشاهده موجودی کالاها، ثبت موجودی اولیه و اصلاح مغایرت‌ها</p></div>
        <button v-if="canOpen" class="btn btn-primary btn-sm" :disabled="!warehouseId || saving" @click="startOpening">ثبت موجودی اولیه</button>
      </div>
      <div class="card-body">
        <div v-if="warehouseError" class="alert alert-danger" role="alert">{{ warehouseError }} <button class="btn btn-link btn-sm" @click="loadWarehouses">تلاش دوباره</button></div>
        <div class="stock-filters">
          <label>جستجوی انبار<input v-model="warehouseSearch" type="search" class="form-control" placeholder="نام انبار" :disabled="warehousesLoading || saving || openingOpen || adjustmentOpen"></label>
          <label>انبار<select v-model="warehouseId" class="form-control" :disabled="warehousesLoading || saving || openingOpen || adjustmentOpen"><option value="">{{ warehousesLoading ? 'در حال دریافت انبارها…' : 'انتخاب انبار' }}</option><option v-for="warehouse in visibleWarehouses" :key="warehouse.id" :value="warehouse.id">{{ warehouse.title }}</option></select></label>
          <label>جستجوی کالا<input v-model="search" type="search" class="form-control" maxlength="200" placeholder="کد یا عنوان کالا" :disabled="!warehouseId"></label>
          <label>موجودی<select v-model="hasStock" class="form-control" :disabled="!warehouseId"><option value="all">همه کالاها</option><option value="true">دارای موجودی</option><option value="false">بدون موجودی</option></select></label>
          <button class="btn btn-outline-primary btn-sm" :disabled="!warehouseId || loading" @click="list.reload(false)"><i class="las la-sync" aria-hidden="true"></i> تازه‌سازی</button>
        </div>
        <p v-if="!warehouseId" class="stock-empty text-muted" role="status">برای مشاهده موجودی، یک انبار انتخاب کنید.</p>
        <div v-else :key="warehouseId" class="users-table-container inventory-table-container" :aria-busy="loading">
          <DataTable ref="table" :options="tableOptions" class="display responsive nowrap table table-sm table-striped users-table inventory-data-table">
            <template #column-productCode="{ cellData }"><span dir="ltr">{{ cellData }}</span></template>
            <template #column-productTitle="{ cellData, rowData }"><strong class="text-black">{{ cellData }}</strong><small class="stock-mobile-code text-muted" dir="ltr">{{ (rowData as WarehouseStock).productCode }}</small></template>
            <template #column-openingQuantity="{ cellData }">{{ cellData === null ? '—' : formatPersianNumber(cellData) }}</template>
            <template #column-currentQuantity="{ cellData, rowData }"><strong>{{ formatPersianNumber(cellData) }}</strong><small class="stock-mobile-code text-muted">{{ (rowData as WarehouseStock).unitOfMeasure }}</small></template>
            <template #column-actions="{ rowData }"><button v-if="canAdjust" type="button" class="btn btn-outline-primary btn-sm" title="ثبت سند اصلاح موجودی" @click="startAdjustment(rowData as WarehouseStock)">اصلاح موجودی</button></template>
          </DataTable>
        </div>
      </div>
    </div>
    <BaseModal v-model="openingOpen" title="ثبت موجودی اولیه" size="lg" :busy="saving">
      <p>انبار: <strong>{{ selectedWarehouse?.title }}</strong></p>
      <p class="text-muted">فقط کالاهای بدون گردش قبلی مجازند. مقدار صفر نادیده گرفته می‌شود.</p>
      <div v-if="modalError" class="alert alert-danger" role="alert">{{ modalError }}</div>
      <div v-for="(row, index) in openingRows" :key="row.key" class="stock-opening-row">
        <ProductSelect :model-value="row.productId" :text="row.text" :label="`کالای ${formatPersianNumber(index + 1)}`" :disabled="saving"
          @update:model-value="row.productId = $event; if (!$event) { row.code = ''; row.unit = '' }" @update:text="row.text = $event" @select="selectProduct(row, $event)" />
        <div><small class="text-muted">کد / واحد</small><div><span dir="ltr">{{ row.code || '—' }}</span> / {{ row.unit || '—' }}</div></div>
        <label>موجودی اولیه<input v-model="row.quantity" type="number" min="0" max="999999999999999.999" step="0.001" inputmode="decimal" class="form-control" :disabled="saving"></label>
        <button type="button" class="btn btn-outline-danger btn-sm" :aria-label="`حذف ردیف ${index + 1}`" :disabled="saving" @click="openingRows.splice(index, 1)"><i class="las la-trash" aria-hidden="true"></i></button>
      </div>
      <button class="btn btn-outline-primary btn-sm" :disabled="saving || openingRows.length >= 100" @click="openingRows.push(newOpeningRow())">افزودن کالا</button>
      <template #footer><button class="btn btn-light" :disabled="saving" @click="openingOpen = false">انصراف</button><button class="btn btn-primary" :disabled="saving || !openingRows.length" @click="saveOpening">{{ saving ? 'در حال ثبت…' : 'ثبت موجودی اولیه' }}</button></template>
    </BaseModal>
    <BaseModal v-model="adjustmentOpen" title="ثبت سند اصلاح موجودی" :busy="saving">
      <template v-if="adjustment">
        <p><strong>{{ adjustment.productTitle }}</strong> — <span dir="ltr">{{ adjustment.productCode }}</span></p>
        <p class="text-muted">انبار {{ selectedWarehouse?.title }} / واحد: {{ adjustment.unitOfMeasure }}</p>
        <p>موجودی فعلی: <strong>{{ formatPersianNumber(adjustment.currentQuantity) }}</strong></p>
        <div v-if="modalError" class="alert alert-danger" role="alert">{{ modalError }}</div>
        <button v-if="conflict" class="btn btn-outline-primary mb-3" :disabled="saving" @click="refreshAdjustment">دریافت موجودی جدید و بررسی دوباره</button>
        <label class="stock-field">موجودی جدید<input v-model="newQuantity" type="number" min="0" max="999999999999999.999" step="0.001" inputmode="decimal" class="form-control" :disabled="saving || conflict"></label>
        <p aria-live="polite">اختلاف: <strong dir="ltr" :class="delta > 0 ? 'text-success' : delta < 0 ? 'text-danger' : 'text-muted'">{{ delta > 0 ? '+' : '' }}{{ formatPersianNumber(delta) }}</strong></p>
        <label class="stock-field">علت اصلاح <span class="text-danger">*</span><textarea v-model="reason" class="form-control" maxlength="1000" rows="3" :disabled="saving" placeholder="علت مغایرت یا اصلاح را وارد کنید"></textarea></label>
      </template>
      <template #footer><button class="btn btn-light" :disabled="saving" @click="adjustmentOpen = false">انصراف</button><button class="btn btn-primary" :disabled="saving || conflict" @click="saveAdjustment">{{ saving ? 'در حال ثبت…' : 'ثبت اصلاح موجودی' }}</button></template>
    </BaseModal>
  </div>
</template>

<style scoped>
.stock-header { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }
.stock-filters { display: flex; flex-wrap: wrap; align-items: end; gap: .75rem; margin-bottom: 1rem; }
.stock-filters label { flex: 1 1 150px; margin: 0; min-width: 0; color: var(--text-primary); }
.stock-empty { padding: 3rem 1rem; text-align: center; border: 1px dashed var(--border-color); border-radius: .5rem; }
.stock-opening-row { display: grid; grid-template-columns: minmax(0, 2fr) minmax(0, 1fr) minmax(0, 1fr) auto; gap: .75rem; align-items: end; margin-bottom: 1rem; padding-bottom: 1rem; border-bottom: 1px solid var(--border-color); }
.stock-opening-row label { margin: 0; }
.stock-field { display: block; width: 100%; color: var(--text-primary); }
.stock-mobile-code { display: none; }
@media (max-width: 767px) {
  .stock-page :deep(.stock-secondary-column) { display: none !important; }
  .stock-page :deep(.inventory-data-table) { table-layout: auto !important; }
  .stock-page :deep(.dt-layout-table > div) { max-width: 100%; overflow-x: hidden !important; }
  .stock-page :deep(.dt-layout-table) { margin-left: 0; margin-right: 0; }
  .stock-opening-row { grid-template-columns: minmax(0, 1fr) auto; }
  .stock-opening-row > :first-child { grid-column: 1 / -1; }
  .stock-opening-row label { grid-column: 1; }
  .stock-mobile-code { display: block; margin-top: .25rem; white-space: normal; }
  .stock-page :deep(.inventory-data-table td), .stock-page :deep(.inventory-data-table th) { white-space: normal !important; }
  .stock-page :deep(.inventory-data-table) { width: 100% !important; }
  .stock-page :deep(.inventory-data-table .btn) { padding: .25rem; font-size: .7rem; }
}
</style>
