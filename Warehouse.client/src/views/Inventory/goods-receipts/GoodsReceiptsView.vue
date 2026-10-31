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
  getGoodsReceiptMetrics,
  getGoodsReceipts,
  type EntityHistoryEntry,
  type GoodsReceipt,
  type GoodsReceiptImportSummary,
  type GoodsReceiptItemRow,
  type GoodsReceiptListParameters,
  type GoodsReceiptMetrics,
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
import { useGoodsReceiptColumnPermissions } from './useGoodsReceiptColumnPermissions'

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
const { canManageInvoice, canManageReceipt, canEditWarehouse, canReadColumn } = useGoodsReceiptColumnPermissions()
const canReadItemHistory = has('GoodsReceiptHistoryRead')
const canImport = has('GoodsReceiptsImport')
const canExport = has('GoodsReceiptsExport')
const canEdit = canInitialEntry || canManageInvoice || canManageReceipt || canEditWarehouse

const metricCards = [
  { key: 'itemsWithoutReceiptCount', permission: 'GoodsReceiptsMissingReceiptCountRead', title: 'کالاهای بدون رسید', tone: 'blue', mode: 'items' },
  { key: 'itemsWithoutInvoiceCount', permission: 'GoodsReceiptsMissingInvoiceCountRead', title: 'فاکتور بلاتکلیف', tone: 'amber', mode: 'items' },
  { key: 'todayItemCount', permission: 'GoodsReceiptsTodayItemCountRead', title: 'تعداد اقلام ورودی امروز', tone: 'teal', mode: 'items' },
  { key: 'todayDocumentCount', permission: 'GoodsReceiptsTodayDocumentCountRead', title: 'تعداد اسناد ورودی امروز', tone: 'violet', mode: 'receipts' }  
] as const
type MetricCard = (typeof metricCards)[number]
const visibleMetricCards = metricCards.filter(card => has(card.permission))
const metrics = ref<GoodsReceiptMetrics | null>(null)
const metricsLoading = ref(false)
const metricsChannel = useRequestChannel()

// ── state صفحه ─────────────────────────────────────────────────────
const tableMode = ref<TableMode>('items')
const selectedMetric = ref<MetricCard | null>(null)
const tableContainer = ref<HTMLElement | null>(null)
const formModal = ref<InstanceType<typeof GoodsReceiptFormModal> | null>(null)
const editLoading = ref(false)
const exporting = ref(false)
const importOpen = ref(false)
const importFile = ref<File | null>(null)
const importSummary = ref<GoodsReceiptImportSummary | null>(null)

const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/goods-receipts')

async function loadMetrics(): Promise<void> {
  if (!visibleMetricCards.length) return
  const controller = metricsChannel.begin()
  metricsLoading.value = true
  try {
    const result = await getGoodsReceiptMetrics(controller.signal)
    if (metricsChannel.isCurrent(controller)) metrics.value = result
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'دریافت آمار ورود کالا انجام نشد.')
  } finally {
    if (metricsChannel.isCurrent(controller)) {
      metricsChannel.end(controller)
      metricsLoading.value = false
    }
  }
}
void loadMetrics()
const options = useGoodsReceiptOptions(handleUnauthorized)
const { people, warehouses, units, loading: optionsLoading } = options
void options.load()

// ── فیلترها و لیست ─────────────────────────────────────────────────
const filters = useFilterState(() => list.reload(true))
const { search: quickSearch, applied: appliedFilters } = filters

function toggleMetric(card: MetricCard): void {
  selectedMetric.value = selectedMetric.value?.key === card.key ? null : card
  quickSearch.value = ''
  appliedFilters.value = selectedMetric.value?.key === 'itemsWithoutInvoiceCount'
    ? { invoiceStatus: 'NotEntered' }
    : {}
  if (selectedMetric.value) {
    const mode = card.mode as TableMode
    if (tableMode.value !== mode) {
      tableMode.value = mode
      return
    }
  }
  list.reload(true)
}

function changeTableMode(mode: TableMode): void {
  if (selectedMetric.value?.key === 'itemsWithoutInvoiceCount') {
    const next = { ...appliedFilters.value }
    delete next.invoiceStatus
    appliedFilters.value = next
  }
  selectedMetric.value = null
  if (tableMode.value === mode) list.reload(true)
  else tableMode.value = mode
}

function clearAllFilters(): void {
  selectedMetric.value = null
  filters.onClear()
}

watch(appliedFilters, value => {
  if (selectedMetric.value?.key === 'itemsWithoutInvoiceCount' && value.invoiceStatus !== 'NotEntered') {
    selectedMetric.value = null
  }
}, { deep: true })

const filterDefinitions = computed<DataTableFilterDefinition[]>(() => {
  const definitions: DataTableFilterDefinition[] = [
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
  ]
  return definitions.filter(definition => canReadColumn(definition.key))
})

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
    receiptNumberIsEmpty: selectedMetric.value?.key === 'itemsWithoutReceiptCount' || undefined,
    quickFilter: selectedMetric.value?.key === 'todayItemCount' || selectedMetric.value?.key === 'todayDocumentCount'
      ? 'Today' : undefined,
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

function receiptItemLabel(item: GoodsReceipt['items'][number]): string {
  return `${item.productTitle} (${formatPersianNumber(item.quantity)} ${item.unitOfMeasureTitle})`
}

function receiptItemsText(items: GoodsReceipt['items']): string {
  return items.map(item => item.productTitle).join('، ')
}

function receiptItemsTooltip(items: GoodsReceipt['items']): string {
  return items.map((item, index) => `${formatPersianNumber(index + 1)}. ${receiptItemLabel(item)}`).join('\n')
}

const columns: Record<TableMode, ColumnsConfig[]> = {
  receipts: [
    rowNumber,
    { data: 'enteredAtUtc', name: 'enteredAtUtc', title: 'تاریخ ورود', className: 'all text-bold inventory-date-column' },
    { data: 'pageNumber', name: 'pageNumber', title: 'شماره صفحه', className: 'min-tablet-l' },
    { data: 'delivererFullName', name: 'delivererFullName', title: 'تحویل‌دهنده', className: 'min-tablet-l' },
    { data: 'items', name: 'itemCount', title: 'تعداد کالا', className: 'min-tablet-l text-bold', orderable: false },
    { data: 'items', name: 'itemTitles', title: 'اقلام', className: 'all inventory-receipt-items-column', orderable: false, searchable: false },
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
    { data: 'issueNumber', name: 'issueNumber', title: 'شماره خروج', className: 'min-tablet-p text-nowrap', orderable: false },
    { data: 'warehouseTitle', name: 'warehouseTitle', title: 'انبار', className: 'min-desktop' },
    ...(canEdit || canReadItemHistory ? [actions('goodsReceiptId')] : []),
  ],
}
columns.items = columns.items.filter(column => canReadColumn(String(column.name)))

const compact = useCompactScrollbar(tableContainer)
function onTableWheel(event: WheelEvent): void {
  if (event.ctrlKey) { compact.onWheel(event); return }
  if (Math.abs(event.deltaY) <= Math.abs(event.deltaX)) return

  const scrollBody = tableContainer.value?.querySelector<HTMLElement>('.dt-layout-table > div')
  if (!scrollBody || scrollBody.scrollHeight <= scrollBody.clientHeight) return

  event.preventDefault()
  const delta = event.deltaMode === WheelEvent.DOM_DELTA_LINE
    ? event.deltaY * 16
    : event.deltaMode === WheelEvent.DOM_DELTA_PAGE
      ? event.deltaY * scrollBody.clientHeight
      : event.deltaY
  scrollBody.scrollTop += delta
}
const visibility = useColumnVisibility<TableMode>({
  storageKey: 'warehouse.goods-receipts.hidden-columns.v4',
  mode: tableMode,
  columns,
  getApi: () => table.value?.dt,
  defaultHidden: { items: ['issueNumber'] },
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
    order: { name: 'pageNumber', dir: mode === 'items' ? 'desc' : 'asc' },
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
  void loadMetrics()
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
    void loadMetrics()
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
  metricsChannel.abort()
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
                              @update:mode="changeTableMode($event as TableMode)"
                              @create="openCreate" @export="exportItems" @import="onImportFile"
                              @toggle-column="toggleColumn" @toggle-column-menu="toggleMenu" />
      </div>

      <div class="card-body">
        <div v-if="visibleMetricCards.length" class="row receipt-metrics" aria-label="آمار ورود کالا">
          <div v-for="card in visibleMetricCards" :key="card.key" class="receipt-metrics__item">
            <button type="button" class="receipt-metric-card"
                    :class="[`receipt-metric-card--${card.tone}`, { 'receipt-metric-card--active': selectedMetric?.key === card.key }]"
                    :aria-pressed="selectedMetric?.key === card.key" :title="card.title" @click="toggleMetric(card)">
              <div class="receipt-metric-card__content">
                <span class="receipt-metric-card__title">{{ card.title }}</span>
                <strong class="receipt-metric-card__value" :aria-busy="metricsLoading">
                  {{ metrics?.[card.key] == null ? '—' : formatPersianNumber(metrics[card.key]!) }}
                </strong>
              </div>
            </button>
          </div>
        </div>
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

        <DataTableFilters :key="selectedMetric?.key ?? 'all'" v-model="appliedFilters"
                          :definitions="filterDefinitions" :search="quickSearch"
                          :loading="listLoading"
                          search-placeholder="جستجو در کالا، توضیحات، طرف حساب، شماره صفحه، فاکتور و رسید..."
                          @update:search="filters.onSearch" @apply="filters.onApply" @clear="clearAllFilters"
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
             :class="`inventory-table-container--${tableMode}`" @wheel="onTableWheel">
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
            <template #column-itemTitles="{ cellData }">
              <span class="receipt-item-titles" tabindex="0"
                    :title="receiptItemsTooltip(cellData as GoodsReceipt['items'])">
                {{ receiptItemsText(cellData as GoodsReceipt['items']) || '—' }}
              </span>
            </template>
            <template #column-productTitle="{ cellData }">
              <strong class="text-black" :title="String(cellData ?? '')">{{ cellData }}</strong>
            </template>
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
              <span v-if="cellData === 'HasInvoice' || cellData === 'DoesNotHave'"
                    class="badge light goods-receipts-invoice-status"
                    :class="cellData === 'HasInvoice' ? 'badge-success' : 'badge-secondary'">
                {{ cellData === 'HasInvoice' ? 'دارد' : cellData === 'DoesNotHave' ? 'ندارد' : '' }}
              </span>
            </template>
            <template v-if="canEdit || canReadItemHistory" #column-actions="{ rowData }">
              <div class="d-flex">
                <button v-if="canEdit" class="btn users-table__role-action" type="button"
                        :title="rowData.isLocked ? 'ورودی به‌دلیل ارسال یا تأیید فاکتور در مالی قفل است' : 'ویرایش سند'"
                        aria-label="ویرایش سند" :disabled="editLoading || rowData.isLocked" @click="openEdit(rowData)">
                  <i class="las" :class="rowData.isLocked ? 'la-lock' : 'la-edit'" aria-hidden="true"></i>
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
.goods-receipts-page :deep(.inventory-receipt-items-column) { max-width: 30rem; min-width: 10rem; width: 28%; }
.receipt-item-titles {
  display: block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.receipt-metrics { margin: 0 -.25rem .25rem; }
.receipt-metrics__item { display: flex; flex: 0 0 50%; max-width: 50%; padding: 0 .25rem .5rem; }
@media (min-width: 992px) {
  .receipt-metrics__item { flex-basis: 25%; max-width: 25%; }
}
@media (min-width: 1200px) {
  .receipt-metrics__item { flex-basis: 12.5%; max-width: 12.5%; }
}
.receipt-metric-card {
  align-items: center;
  background: var(--metric-bg);
  border: 1px solid var(--metric-border);
  border-radius: .5rem;
  color: var(--metric-ink);
  display: flex;
  flex: 1;
  font-family: "iransans-medium", sans-serif;
  gap: .35rem;
  min-height: calc(3.25rem - 10px);
  padding: .25rem .45rem;
  text-align: right;
  width: 100%;
  cursor: pointer;
  transition: transform .2s ease, box-shadow .2s ease, border-color .2s ease;
}
.receipt-metric-card:hover, .receipt-metric-card--active { border-color: var(--metric-ink); }
.receipt-metric-card:hover { box-shadow: 0 4px 12px rgba(20, 30, 50, .13); transform: translateY(-2px); }
.receipt-metric-card--active { box-shadow: inset 0 0 0 1px var(--metric-ink); }
.receipt-metric-card--active:hover { box-shadow: inset 0 0 0 1px var(--metric-ink), 0 4px 12px rgba(20, 30, 50, .13); }
.receipt-metric-card:focus-visible { outline: 2px solid var(--metric-ink); outline-offset: 2px; }
@media (prefers-reduced-motion: reduce) {
  .receipt-metric-card { transition: none; }
  .receipt-metric-card:hover { transform: none; }
}
.receipt-metric-card__content {
  align-items: center;
  display: flex;
  gap: .35rem;
  justify-content: space-between;
  width: 100%;
}
.receipt-metric-card__title { flex: 1; font-family: "iransans-bold", sans-serif; font-size: .68rem; line-height: 1.5; }
.receipt-metric-card__value { flex: 0 0 auto; font-family: "iransans-bold", sans-serif; font-size: 1.05rem; line-height: 1.5; }
.receipt-metric-card--blue { --metric-bg: #e8efff; --metric-border: #c8d8ff; --metric-ink: #1d4ed8; }
.receipt-metric-card--amber { --metric-bg: #fff3df; --metric-border: #f7dcad; --metric-ink: #9a5805; }
.receipt-metric-card--teal { --metric-bg: #e4f6f4; --metric-border: #b9e5df; --metric-ink: #0f766e; }
.receipt-metric-card--violet { --metric-bg: #f0eaff; --metric-border: #d9c9ff; --metric-ink: #6d28d9; }
:global(body[data-app-theme='dark'] .receipt-metric-card--blue) { --metric-bg: #172c50; --metric-border: #29518a; --metric-ink: #93b4ff; }
:global(body[data-app-theme='dark'] .receipt-metric-card--amber) { --metric-bg: #3b2b1c; --metric-border: #735029; --metric-ink: #f6c778; }
:global(body[data-app-theme='dark'] .receipt-metric-card--teal) { --metric-bg: #173b3d; --metric-border: #286a67; --metric-ink: #80d9ca; }
:global(body[data-app-theme='dark'] .receipt-metric-card--violet) { --metric-bg: #33234f; --metric-border: #5d3a85; --metric-ink: #c5a8ff; }
.goods-receipts-page :deep(.dt-layout-table > div) {
  max-height: 50vh;
  overflow: auto !important;
  overscroll-behavior: contain;
  scrollbar-width: thin;
}
.import-errors { max-height: 20rem; overflow-y: auto; }
.goods-receipts-invoice-status { font-size: 0.75rem; font-weight: 700; padding: 0.2em 0.45em; }
</style>
