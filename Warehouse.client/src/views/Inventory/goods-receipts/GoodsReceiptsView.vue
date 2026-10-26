<script setup lang="ts">
import type { ColumnsConfig, Config } from 'datatables.net'
import DataTablesCore from 'datatables.net-bs4'
import DataTable from 'datatables.net-vue3'
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'

import { highlightTableText } from '@/components/UseTableHighlight'
import EntityHistoryModal from '@/components/EntityHistoryModal.vue'
import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import InventoryListToolbar from '@/components/InventoryListToolbar.vue'
import PersonSelect from '@/components/selectors/PersonSelect.vue'
import ProductSelect from '@/components/selectors/ProductSelect.vue'
import SupplierSelect from '@/components/selectors/SupplierSelect.vue'
import { useColumnVisibility } from '@/composables/useColumnVisibility'
import { useCompactScrollbar } from '@/composables/useCompactScrollbar'
import { useDataTableList } from '@/composables/useDataTableList'
import { useFilterState, type SetFilterValue } from '@/composables/useFilterState'
import { usePermissions } from '@/composables/usePermissions'
import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'
import {
  exportGoodsReceiptItems,
  getGoodsReceiptItemHistory,
  getGoodsReceiptItems,
  getGoodsReceipts,
  type EntityHistoryEntry,
  type GoodsReceipt,
  type GoodsReceiptImportSummary,
  type GoodsReceiptItemRow,
  type GoodsReceiptListParameters,
  type InvoiceStatusFilter,
} from '@/services/goodsReceipts'
import type { Product } from '@/services/products'
import type { SupplierOption } from '@/services/suppliers'
import { toast } from '@/services/toast'
import type { DataTableFilterDefinition, DataTableFilterValue } from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'
import { formatPersianDate, formatPersianNumber } from '@/utils/dateTime'
import { downloadBlob } from '@/utils/download'
import { createInventoryTableOptions } from '@/utils/inventoryTable'
import '@/styles/inventory-list.css'

import GoodsReceiptFormModal from './GoodsReceiptFormModal.vue'
import GoodsReceiptImportModal from './GoodsReceiptImportModal.vue'
import { useGoodsReceiptOptions } from './useGoodsReceiptOptions'

defineOptions({ name: 'GoodsReceiptsView' })
DataTable.use(DataTablesCore)

type TableMode = 'items' | 'receipts'
const VIEW_MODES = [
  { value: 'items', label: 'بر اساس کالا' },
  { value: 'receipts', label: 'بر اساس ورودی' },
]

// ── دسترسی‌ها ──────────────────────────────────────────────────────
const { has } = usePermissions()
const canInitialEntry = has('GoodsReceiptsInitialEntry')
const canManageReceiptInvoice = has('GoodsReceiptsReceiptInvoiceOperations')
const canReadItemHistory = has('GoodsReceiptHistoryRead')
const canImport = has('GoodsReceiptsImport')
const canExport = has('GoodsReceiptsExport')
const canEdit = canInitialEntry || canManageReceiptInvoice

// ── state صفحه ─────────────────────────────────────────────────────
const tableMode = ref<TableMode>('items')
const tableContainer = ref<HTMLElement | null>(null)
const formModal = ref<InstanceType<typeof GoodsReceiptFormModal> | null>(null)
const editLoading = ref(false)
const exporting = ref(false)
const importOpen = ref(false)
const importFile = ref<File | null>(null)
const importSummary = ref<GoodsReceiptImportSummary | null>(null)

const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/goods-receipts')
const options = useGoodsReceiptOptions(handleUnauthorized)
const { people, warehouses, units, loading: optionsLoading } = options
void options.load()

// ── فیلترها و لیست ─────────────────────────────────────────────────
const filters = useFilterState(() => list.reload(true))
const { search: quickSearch, applied: appliedFilters } = filters

const filterDefinitions = computed<DataTableFilterDefinition[]>(() => [
  { key: 'enteredAt', label: 'تاریخ ورود', type: 'date-comparison' },
  { key: 'productTitle', label: 'کالا', type: 'text-comparison', placeholder: 'جستجو و انتخاب کالا', maxlength: 256 },
  { key: 'productCode', label: 'کد کالا', type: 'text-comparison', placeholder: 'بخشی از کد کالا', maxlength: 100 },
  { key: 'pageNumber', label: 'شماره صفحه', type: 'text-comparison', placeholder: 'شماره صفحه', maxlength: 100 },
  { key: 'quantity', label: 'مقدار', type: 'numeric-comparison', numericType: 'decimal', min: 0 },
  {
    key: 'warehouseId', label: 'انبار', type: 'select', placeholder: 'همه انبارها',
    options: warehouses.value.map(item => ({ value: item.id, label: item.title })),
  },
  {
    key: 'delivererId', label: 'تحویل‌دهنده', type: 'select', placeholder: 'همه تحویل‌دهندگان',
    options: people.value.map(item => ({ value: item.id, label: item.fullName })),
  },
  {
    key: 'invoiceStatus', label: 'وضعیت فاکتور', type: 'select', placeholder: 'همه وضعیت‌ها',
    options: [
      { value: 'NotEntered', label: 'وارد نشده' },
      { value: 'HasInvoice', label: 'دارد' },
      { value: 'DoesNotHave', label: 'ندارد' },
    ],
  },
  { key: 'invoiceNumber', label: 'شماره فاکتور', type: 'text-comparison', placeholder: 'شماره فاکتور', maxlength: 100 },
  { key: 'receiptNumber', label: 'شماره رسید', type: 'text-comparison', placeholder: 'شماره رسید', maxlength: 100 },
  {
    key: 'supplierId', label: 'طرف حساب', type: 'select', placeholder: 'همه طرف حساب‌ها',
    options: filters.selectedOption('supplierId'),
  },
])

function currentFilterParameters(): GoodsReceiptListParameters {
  return {
    search: filters.searchQuery.value,
    enteredAt: filters.date('enteredAt'),
    productTitle: filters.text('productTitle'),
    productCode: filters.text('productCode'),
    pageNumberFilter: filters.text('pageNumber'),
    quantity: filters.numeric('quantity'),
    invoiceNumber: filters.text('invoiceNumber'),
    receiptNumber: filters.text('receiptNumber'),
    warehouseId: filters.id('warehouseId'),
    delivererId: filters.id('delivererId'),
    supplierId: filters.id('supplierId'),
    invoiceStatus: filters.id('invoiceStatus') as InvoiceStatusFilter | undefined,
  }
}

const list = useDataTableList<unknown>({
  errorMessage: 'دریافت اطلاعات ورود کالا انجام نشد.',
  onUnauthorized: handleUnauthorized,
  fetchPage: query => {
    const parameters: GoodsReceiptListParameters = {
      ...currentFilterParameters(),
      pageNumber: query.page,
      pageSize: query.pageSize,
      sortBy: query.sortBy,
      sortDescending: query.sortDescending,
      signal: query.signal,
    }
    return tableMode.value === 'items' ? getGoodsReceiptItems(parameters) : getGoodsReceipts(parameters)
  },
})
const { table, loading: listLoading } = list

// ── اسلات‌های فیلتر ────────────────────────────────────────────────
const asId = (value: DataTableFilterValue | undefined): string | null =>
  typeof value === 'string' && value ? value : null

function selectProductFilter(product: Product, setValue: SetFilterValue): void {
  filters.setSelection('productTitle', { id: product.id, text: product.title })
  setValue({ operator: 'eq', value: product.title })
}
function clearProductFilter(setValue: SetFilterValue): void {
  filters.setSelection('productTitle', { id: null, text: '' })
  setValue(null)
}

// ── ستون‌ها ────────────────────────────────────────────────────────
const rowNumber: ColumnsConfig = { ...createRowNumberColumn(), className: 'min-tablet-p text-center data-table-row-number' }
const actions = (data: string): ColumnsConfig => ({
  data, name: 'actions', title: 'عملیات', className: 'min-tablet-p text-center', orderable: false, searchable: false,
})

const columns: Record<TableMode, ColumnsConfig[]> = {
  receipts: [
    rowNumber,
    { data: 'enteredAtUtc', name: 'enteredAtUtc', title: 'تاریخ ورود', className: 'all text-bold inventory-date-column' },
    { data: 'pageNumber', name: 'pageNumber', title: 'شماره صفحه', className: 'min-tablet-l' },
    { data: 'delivererFullName', name: 'delivererFullName', title: 'تحویل‌دهنده', className: 'min-tablet-l' },
    { data: 'items', name: 'itemCount', title: 'تعداد کالا', className: 'min-tablet-l text-bold', orderable: false },
    { data: 'description', name: 'description', title: 'توضیحات ورودی', className: 'min-tablet-p', orderable: false },
    ...(canEdit ? [actions('id')] : []),
  ],
  items: [
    rowNumber,
    { data: 'pageNumber', name: 'pageNumber', title: 'شماره صفحه', className: 'min-tablet-l' },
    { data: 'enteredAtUtc', name: 'enteredAtUtc', title: 'تاریخ', className: 'all text-bold inventory-date-column' },
    { data: 'productTitle', name: 'productTitle', title: 'عنوان کالا', className: 'all inventory-product-title-column' },
    { data: 'productCode', name: 'productCode', title: 'کد کالا', className: 'min-tablet-l' },
    { data: 'quantity', name: 'quantity', title: 'تعداد', className: 'min-tablet-l text-bold' },
    { data: 'unitOfMeasureTitle', name: 'unitOfMeasureTitle', title: 'واحد', className: 'min-tablet-l text-bold' },
    { data: 'itemDescription', name: 'itemDescription', title: 'توضیحات کالا', className: 'min-tablet-p inventory-wide-column', orderable: false },
    { data: 'delivererFullName', name: 'delivererFullName', title: 'تحویل‌دهنده', className: 'min-desktop' },
    { data: 'invoiceStatus', name: 'invoiceStatus', title: 'فاکتور', className: 'min-desktop' },
    { data: 'invoiceNumber', name: 'invoiceNumber', title: 'شماره فاکتور', className: 'min-tablet-p text-nowrap', orderable: false },
    { data: 'counterparty', name: 'counterparty', title: 'طرف حساب', className: 'min-tablet-p text-nowrap', orderable: false },
    { data: 'receiptNumber', name: 'receiptNumber', title: 'شماره رسید', className: 'min-tablet-p text-nowrap', orderable: false },
    { data: 'warehouseTitle', name: 'warehouseTitle', title: 'انبار', className: 'min-desktop' },
    ...(canEdit || canReadItemHistory ? [actions('goodsReceiptId')] : []),
  ],
}

const compact = useCompactScrollbar(tableContainer)
const visibility = useColumnVisibility<TableMode>({
  storageKey: 'warehouse.goods-receipts.hidden-columns.v3',
  mode: tableMode,
  columns,
  getApi: () => table.value?.dt,
})
const { menuOpen, toggleable, isVisible: isColumnVisible, toggle: toggleColumn, toggleMenu, closeMenu } = visibility

// هایلایت متن جستجو بعد از رندر اسلات‌های Vue (دو frame بعد از draw)
let highlightFrame: number | null = null
function refreshHighlight(): void {
  if (highlightFrame !== null) cancelAnimationFrame(highlightFrame)
  void nextTick(() => {
    highlightFrame = requestAnimationFrame(() => {
      highlightFrame = requestAnimationFrame(() => {
        highlightFrame = null
        highlightTableText(tableContainer.value, quickSearch.value)
      })
    })
  })
}

function tableOptions(mode: TableMode): Config {
  return createInventoryTableOptions({
    ajax: list.ajax,
    columns: visibility.resolvedColumns[mode],
    order: { name: 'enteredAtUtc', dir: 'desc' },   
    emptyText: mode === 'items' ? 'هنوز کالایی وارد انبار نشده است.' : 'هنوز ورودی کالایی ثبت نشده است.',
    loadingText: 'در حال دریافت تاریخچه ورود کالا...',
    zeroText: 'ورودی کالایی پیدا نشد.',
    onDraw: () => { compact.schedule(); refreshHighlight() },
    onInit: compact.schedule,
  })
}

const tableOptionsByMode: Record<TableMode, Config> = 
{ items: tableOptions('items'), receipts: tableOptions('receipts') }

watch(tableMode, () => {
  closeMenu()
  compact.disconnect()
  compact.schedule()
})

// ── ثبت / ویرایش ───────────────────────────────────────────────────
async function openCreate(): Promise<void> {
  if (!await options.load()) return
  formModal.value?.openCreate()
}

async function openEdit(row: GoodsReceipt | GoodsReceiptItemRow): Promise<void> {
  if (editLoading.value || !canEdit) return
  editLoading.value = true
  try {
    if (!await options.load()) return
    await formModal.value?.openEdit(row)
  } finally {
    editLoading.value = false
  }
}

function onSaved(reason: 'saved' | 'deleted'): void {
  void options.reload()
  list.reload(reason === 'saved')
}

// ── import / export ────────────────────────────────────────────────
function onImportFile(file: File): void {
  if (!canInitialEntry) return
  if (!/\.(xlsx|xlsm)$/i.test(file.name) || file.size === 0 || file.size > 50 * 1024 * 1024) {
    toast.error('فایل xlsx یا xlsm غیرخالی با حجم حداکثر ۵۰ مگابایت انتخاب کنید.')
    return
  }
  importFile.value = file
  importOpen.value = true
}

function onImportOpenChange(open: boolean): void {
  importOpen.value = open
  if (!open) importFile.value = null
}

function onImported(summary: GoodsReceiptImportSummary): void {
  importSummary.value = summary
  if (summary.importedReceiptCount > 0) {
    void options.reload()
    list.reload(true)
    toast.success(`${summary.importedReceiptCount} سند ورود و ${summary.importedItemCount} قلم کالا ثبت شد.`)
  }
}

const exportChannel = useRequestChannel()
async function exportItems(): Promise<void> {
  const controller = exportChannel.begin()
  exporting.value = true
  try {
    const file = await exportGoodsReceiptItems({ ...currentFilterParameters(), signal: controller.signal })
    downloadBlob(file, `goods-receipts-${new Date().toISOString().slice(0, 10)}.xlsx`)
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'دریافت خروجی Excel انجام نشد.')
  } finally {
    if (exportChannel.isCurrent(controller)) { exportChannel.end(controller); exporting.value = false }
  }
}

// ── تاریخچه تغییرات کالا ───────────────────────────────────────────
const historyOpen = ref(false)
const historyLoading = ref(false)
const historyError = ref('')
const historyTitle = ref('')
const historyEntries = ref<EntityHistoryEntry[]>([])
const historyChannel = useRequestChannel()

async function openItemHistory(row: GoodsReceiptItemRow): Promise<void> {
  if (!canReadItemHistory || historyLoading.value) return
  const controller = historyChannel.begin()
  historyOpen.value = true
  historyLoading.value = true
  historyError.value = ''
  historyTitle.value = `${row.productTitle} (${row.productCode})`
  historyEntries.value = []
  try {
    const result = await getGoodsReceiptItemHistory(row.id, controller.signal)
    historyTitle.value = result.itemTitle
    historyEntries.value = result.entries
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    historyError.value = error instanceof Error ? error.message : 'دریافت تاریخچه تغییرات کالا انجام نشد.'
  } finally {
    if (historyChannel.isCurrent(controller)) { historyChannel.end(controller); historyLoading.value = false }
  }
}

function closeItemHistory(): void {
  historyChannel.abort()
  historyOpen.value = false
  historyLoading.value = false
}

onBeforeUnmount(() => {
  if (highlightFrame !== null) cancelAnimationFrame(highlightFrame)
})
</script>

<template>
  <EntityHistoryModal :open="historyOpen" :title="historyTitle" :loading="historyLoading" :error="historyError"
                      :entries="historyEntries" @close="closeItemHistory" />
  <GoodsReceiptFormModal ref="formModal" :people="people" :warehouses="warehouses" :units="units" @saved="onSaved" />
  <GoodsReceiptImportModal :model-value="importOpen" :file="importFile" @update:model-value="onImportOpenChange"
                           @imported="onImported" />

  <div class="container-fluid users-page goods-receipts-page inventory-data-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item active"><span>تاریخچه ورود کالا</span></li>
      </ol>
    </div>

    <div class="card">
      <div class="card-header">
        <div>
          <h4 class="card-title"><i class="las la-dolly-flatbed mr-1"></i> تاریخچه ورود کالا</h4>
          <p class="page-description mb-0 mt-1 text-muted">ثبت اسناد ورود و ردیف‌های مستقل کالا</p>
        </div>
        <InventoryListToolbar :mode="tableMode" :modes="VIEW_MODES" entity-label="ورودی کالا" create-label="ثبت ورود کالا"
                              :can-create="canInitialEntry" :can-import="canImport" :can-export="canExport"
                              :create-disabled="optionsLoading" :exporting="exporting"
                              :columns="toggleable[tableMode]" :is-column-visible="isColumnVisible"
                              :column-menu-open="menuOpen"
                              @update:mode="tableMode = $event as TableMode"
                              @create="openCreate" @export="exportItems" @import="onImportFile"
                              @toggle-column="toggleColumn" @toggle-column-menu="toggleMenu" />
      </div>

      <div class="card-body">
        <div v-if="importSummary" class="alert" :class="importSummary.invalidRows.length ? 'alert-danger' : 'alert-success'"
             role="status">
          <template v-if="importSummary.invalidRows.length">
            <p>هیچ ورودی یا تحویل‌دهنده‌ای ثبت نشد. خطاها را اصلاح و فایل را دوباره ارسال کنید.</p>
            <ul class="mb-0 import-errors">
              <li v-for="row in importSummary.invalidRows" :key="row.rowNumber">ردیف {{ row.rowNumber }}: {{ row.message }}</li>
            </ul>
          </template>
          <template v-else>
            {{ importSummary.importedReceiptCount }} سند ورود، {{ importSummary.importedItemCount }} قلم کالا و
            {{ importSummary.createdDelivererCount }} تحویل‌دهندهٔ جدید ثبت شد.
          </template>
        </div>

        <DataTableFilters v-model="appliedFilters" :definitions="filterDefinitions" :search="quickSearch"
                          :loading="listLoading"
                          search-placeholder="جستجو در عنوان و کد کالا، شماره صفحه، فاکتور و رسید..."
                          @update:search="filters.onSearch" @apply="filters.onApply" @clear="filters.onClear"
                          @remove="filters.onRemove">
          <template #filter-productTitle="{ setValue, error }">
            <ProductSelect :model-value="filters.selection('productTitle').id"
                           :text="filters.selection('productTitle').text" label="کالا" placeholder="جستجو و انتخاب کالا"
                           size="sm" :error="error"
                           @update:model-value="filters.setSelection('productTitle', { id: $event })"
                           @update:text="filters.setSelection('productTitle', { text: $event })"
                           @select="selectProductFilter($event, setValue)" @clear="clearProductFilter(setValue)" />
          </template>
          <template #filter-supplierId="{ value, setValue, error }">
            <SupplierSelect :model-value="asId(value)" :text="filters.selection('supplierId').text" label="طرف حساب"
                            placeholder="جستجو و انتخاب طرف حساب" size="sm" :error="error"
                            @update:model-value="setValue($event)"
                            @update:text="filters.setSelection('supplierId', { text: $event })"
                            @select="(s: SupplierOption) => { filters.setSelection('supplierId', { id: s.id, text: s.title }); setValue(s.id) }" />
          </template>
          <template #filter-delivererId="{ value, setValue, error }">
            <PersonSelect :model-value="asId(value)" :text="filters.selection('delivererId').text" :options="people"
                          label="تحویل‌دهنده" placeholder="جستجو و انتخاب تحویل‌دهنده" size="sm" :error="error"
                          @update:model-value="setValue($event)"
                          @update:text="filters.setSelection('delivererId', { text: $event })"
                          @select="p => { filters.setSelection('delivererId', { id: p.id, text: p.fullName }); setValue(p.id) }" />
          </template>
        </DataTableFilters>

        <div ref="tableContainer" class="users-table-container inventory-table-container"
             :class="`inventory-table-container--${tableMode}`" @wheel="compact.onWheel">
          <!-- thead لازم نیست: DataTables سرستون‌ها را از title ستون‌ها می‌سازد -->
          <DataTable :key="tableMode" ref="table" :options="tableOptionsByMode[tableMode]"
                     class="display responsive nowrap table-striped table table-sm users-table inventory-data-table">
            <template #column-enteredAtUtc="{ cellData }">
              <strong class="text-black">{{ formatPersianDate(cellData) }}</strong>
            </template>
            <template #column-pageNumber="{ cellData }"><span dir="ltr">{{ cellData }}</span></template>
            <template #column-itemCount="{ cellData }">
              <span class="badge light badge-info">{{ (cellData as GoodsReceipt['items']).length }} کالا</span>
            </template>
            <template #column-productTitle="{ cellData }"><strong class="text-black">{{ cellData }}</strong></template>
            <template #column-productCode="{ cellData }"><span dir="ltr">{{ cellData }}</span></template>
            <template #column-quantity="{ cellData }">
              <span class="inventory-quantity-cell">{{ formatPersianNumber(cellData) }}</span>
            </template>
            <template #column-itemDescription="{ cellData }">
              <span class="inventory-description-cell" :title="cellData || ''">{{ cellData}}</span>
            </template>
            <template #column-description="{ cellData }">
              <span class="inventory-description-cell" :title="cellData || ''">{{ cellData }}</span>
            </template>
            <template #column-invoiceNumber="{ cellData }"><span dir="ltr">{{ cellData }}</span></template>
            <template #column-counterparty="{ cellData }">{{ cellData || '' }}</template>
            <template #column-receiptNumber="{ cellData }"><span dir="ltr">{{ cellData}}</span></template>
            <template #column-invoiceStatus="{ cellData }">
              <span class="badge light" :class="cellData === 'HasInvoice' ? 'badge-success' : 'badge-secondary'">
                {{ cellData === 'HasInvoice' ? 'دارد' : cellData === 'DoesNotHave' ? 'ندارد' : '' }}
              </span>
            </template>
            <template v-if="canEdit || canReadItemHistory" #column-actions="{ rowData }">
              <div class="d-flex">
                <button v-if="canEdit" class="btn users-table__role-action" type="button" title="ویرایش سند"
                        aria-label="ویرایش سند" :disabled="editLoading" @click="openEdit(rowData)">
                  <i class="las la-edit" aria-hidden="true"></i>
                </button>
                <button v-if="canReadItemHistory && tableMode === 'items'" class="btn users-table__role-action"
                        type="button" title="مشاهده تاریخچه تغییرات" aria-label="مشاهده تاریخچه تغییرات کالا"
                        :disabled="historyLoading" @click="openItemHistory(rowData)">
                  <i class="las la-history" aria-hidden="true"></i>
                </button>
              </div>
            </template>
          </DataTable>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.import-errors { max-height: 20rem; overflow-y: auto; }
</style>
