<script setup lang="ts">
import type { ColumnsConfig, Config } from 'datatables.net'
import DataTablesCore from 'datatables.net-bs4'
import DataTable from 'datatables.net-vue3'
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'

import { highlightTableText } from '@/components/UseTableHighlight'
import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import InventoryListToolbar from '@/components/InventoryListToolbar.vue'
import GoodsIssueDestinationSelect from '@/components/selectors/GoodsIssueDestinationSelect.vue'
import PersonSelect, { type PersonSelectOption } from '@/components/selectors/PersonSelect.vue'
import ProductSelect from '@/components/selectors/ProductSelect.vue'
import { useColumnVisibility } from '@/composables/useColumnVisibility'
import { useDataTableList } from '@/composables/useDataTableList'
import { useFilterState, type SetFilterValue } from '@/composables/useFilterState'
import { usePermissions } from '@/composables/usePermissions'
import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'
import type { GoodsIssueDestinationOption } from '@/services/goodsIssueDestinations'
import {
  exportGoodsIssueItems,
  getGoodsIssues,
  getGoodsIssuePdf,
  GOODS_ISSUE_EXIT_REASON_OPTIONS,
  type GoodsIssueImportSummary,
  type GoodsIssueExitReason,
  type GoodsIssue,
  type GoodsIssueItemRow,
  type GoodsIssueListParameters,
} from '@/services/goodsIssues'
import type { Product } from '@/services/products'
import { toast } from '@/services/toast'
import type { DataTableFilterDefinition, DataTableFilterValue } from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'
import { formatPersianDate, formatPersianNumber } from '@/utils/dateTime'
import { downloadBlob } from '@/utils/download'
import { printPdf } from '@/utils/pdfPrint'
import { createInventoryTableOptions } from '@/utils/inventoryTable'
import '@/styles/inventory-list.css'

import GoodsIssueFormModal from './GoodsIssueFormModal.vue'
import GoodsIssueImportModal from './GoodsIssueImportModal.vue'
import { useGoodsIssueOptions } from './useGoodsIssueOptions'

defineOptions({ name: 'GoodsIssuesView' })
DataTable.use(DataTablesCore)

type TableMode = 'items' | 'issues'
const VIEW_MODES = [
  { value: 'items', label: 'بر اساس کالا' },
  { value: 'issues', label: 'بر اساس خروج' },
]
const exitReasonLabels = new Map<GoodsIssueExitReason, string>(
  GOODS_ISSUE_EXIT_REASON_OPTIONS.map(option => [option.value, option.label]),
)
const exitReasonBadges: Record<GoodsIssueExitReason, string> = {
  Maintenance: 'badge-warning',
  Sample: 'badge-info',
  Purchase: 'badge-success',
  Sale: 'badge-danger',
  Services: 'badge-primary',
  Consignment: 'badge-secondary',
  Repair: 'badge-warning',
  Other: 'badge-dark',
}

function exitReasonText(reason: GoodsIssueExitReason, legacyReason?: string | null): string {
  return legacyReason?.trim() || exitReasonLabels.get(reason) || 'سایر'
}

const { has } = usePermissions()
const canCreate = has('GoodsIssuesCreate')
const canUpdate = has('GoodsIssuesUpdate')
const canDelete = has('GoodsIssuesDelete')
const canChange = canUpdate || canDelete
const canExport = has('GoodsIssuesExport')
const canImport = has('GoodsIssuesImport')

const tableMode = ref<TableMode>('items')
const tableContainer = ref<HTMLElement | null>(null)
const formModal = ref<InstanceType<typeof GoodsIssueFormModal> | null>(null)
const editLoading = ref(false)
const deletingId = ref<string | null>(null)
const exporting = ref(false)
const printingId = ref<string | null>(null)
const importOpen = ref(false)
const importFile = ref<File | null>(null)
const importSummary = ref<GoodsIssueImportSummary | null>(null)

const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/goods-issues')
const options = useGoodsIssueOptions(handleUnauthorized)
const { people, units, loading: optionsLoading } = options
void options.load()

const filters = useFilterState(() => list.reload(true))
const { search: quickSearch, applied: appliedFilters } = filters

const filterDefinitions = computed<DataTableFilterDefinition[]>(() => [
  { key: 'issuedAt', label: 'تاریخ خروج', type: 'date-comparison' },
  { key: 'issueNumberFilter', label: 'کد خروج', type: 'text-comparison', placeholder: 'کد یا شماره خروج', maxlength: 100 },
  { key: 'productTitle', label: 'عنوان کالا', type: 'text-comparison', placeholder: 'جستجو و انتخاب کالا', maxlength: 256 },
  { key: 'productCode', label: 'کد کالا', type: 'text-comparison', placeholder: 'بخشی از کد کالا', maxlength: 100 },
  { key: 'quantity', label: 'تعداد', type: 'numeric-comparison', numericType: 'decimal', min: 0 },
  { key: 'assetNumber', label: 'شماره اموال', type: 'text-comparison', placeholder: 'شماره اموال', maxlength: 100 },
  {
    key: 'destinationId', label: 'مقصد', type: 'select', placeholder: 'جستجو و انتخاب مقصد',
    options: filters.selectedOption('destinationId'),
  },
  { key: 'deliveringUnit', label: 'واحد تحویل‌دهنده', type: 'text-comparison', placeholder: 'واحد تحویل‌دهنده', maxlength: 200 },
  {
    key: 'issuerId', label: 'خارج‌کننده', type: 'select', placeholder: 'همه خارج‌کنندگان',
    options: people.value.map(person => ({ value: person.id, label: person.fullName })),
  },
])

function currentFilterParameters(): GoodsIssueListParameters {
  return {
    search: filters.searchQuery.value,
    issuedAt: filters.date('issuedAt'),
    issueNumberFilter: filters.text('issueNumberFilter'),
    productTitle: filters.text('productTitle'),
    productCode: filters.text('productCode'),
    quantity: filters.numeric('quantity'),
    assetNumber: filters.text('assetNumber'),
    destinationId: filters.id('destinationId'),
    deliveringUnit: filters.text('deliveringUnit'),
    issuerId: filters.id('issuerId'),
  }
}

const list = useDataTableList<GoodsIssue | GoodsIssueItemRow>({
  errorMessage: 'دریافت اطلاعات خروج کالا انجام نشد.',
  onUnauthorized: handleUnauthorized,
  fetchPage: query => getGoodsIssues(tableMode.value, {
    ...currentFilterParameters(),
    pageNumber: query.page,
    pageSize: query.pageSize,
    sortBy: query.sortBy,
    sortDescending: query.sortDescending,
    signal: query.signal,
  }),
})
const { table, loading: listLoading } = list

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

function selectDestinationFilter(destination: GoodsIssueDestinationOption, setValue: SetFilterValue): void {
  filters.setSelection('destinationId', { id: destination.id, text: destination.title })
  setValue(destination.id)
}

const rowNumber: ColumnsConfig = {
  ...createRowNumberColumn(),
  className: 'min-tablet-p text-center data-table-row-number',
}
const actions = (data: string): ColumnsConfig => ({
  data,
  name: 'actions',
  title: 'عملیات',
  className: 'min-tablet-p text-center actions-cell',
  orderable: false,
  searchable: false,
})

const columns: Record<TableMode, ColumnsConfig[]> = {
  issues: [
    rowNumber,
    { data: 'issueNumber', name: 'issueNumber', title: 'کد خروج', className: 'all' },
    { data: 'issuedAtUtc', name: 'issuedAtUtc', title: 'تاریخ خروج', className: 'all text-bold inventory-date-column' },
    { data: 'issuerFullName', name: 'issuerFullName', title: 'خارج‌کننده', className: 'min-tablet-l' },
    { data: 'description', name: 'description', title: 'توضیحات کلی', className: 'min-tablet-p inventory-wide-column', orderable: false },
    { data: 'items', name: 'itemCount', title: 'تعداد اقلام', className: 'min-tablet-l text-bold', orderable: false },
    ...(canChange || canExport ? [actions('id')] : []),
  ],
  items: [
    rowNumber,
    { data: 'issuedAtUtc', name: 'issuedAtUtc', title: 'تاریخ خروج', className: 'all text-bold inventory-date-column' },
    { data: 'issueNumber', name: 'issueNumber', title: 'کد خروج', className: 'all' },
    { data: 'issueDescription', name: 'issueDescription', title: 'توضیحات کلی', className: 'min-tablet-p inventory-wide-column', orderable: false },
    { data: 'productCode', name: 'productCode', title: 'کد کالا', className: 'min-tablet-l' },
    { data: 'productTitle', name: 'productTitle', title: 'عنوان کالا', className: 'all inventory-product-title-column' },
    { data: 'quantity', name: 'quantity', title: 'تعداد', className: 'min-tablet-l text-bold' },
    { data: 'assetNumber', name: 'assetNumber', title: 'شماره اموال', className: 'min-tablet-l' },
    { data: 'destinationTitle', name: 'destinationTitle', title: 'مقصد', className: 'min-tablet-l' },
    { data: 'shippingLocationTitle', name: 'shippingLocationTitle', title: 'مکان ارسال', className: 'min-tablet-l' },
    { data: 'exitReason', name: 'exitReason', title: 'علت خروج', className: 'min-tablet-p inventory-wide-column', orderable: false },
    { data: 'description', name: 'description', title: 'توضیحات', className: 'min-tablet-p inventory-wide-column', orderable: false },
    { data: 'deliveringUnit', name: 'deliveringUnit', title: 'واحد تحویل‌دهنده', className: 'min-desktop' },
    { data: 'isReturnable', name: 'isReturnable', title: 'برگشت دارد', className: 'min-desktop text-center' },
    { data: 'returnAtUtc', name: 'returnAtUtc', title: 'تاریخ عودت', className: 'min-desktop' },
    { data: 'issuerFullName', name: 'issuerFullName', title: 'خارج‌کننده', className: 'min-desktop' },
    ...(canChange || canExport ? [actions('goodsIssueId')] : []),
  ],
}

const visibility = useColumnVisibility<TableMode>({
  storageKey: 'warehouse.goods-issues.hidden-columns.v2',
  mode: tableMode,
  columns,
  getApi: () => table.value?.dt,
})
const {
  menuOpen,
  toggleable,
  isVisible: isColumnVisible,
  toggle: toggleColumn,
  toggleMenu,
  closeMenu,
} = visibility

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
    order: { name: 'issueNumber', dir: 'asc' },
    emptyText: mode === 'items' ? 'هنوز کالایی از انبار خارج نشده است.' : 'هنوز خروج کالایی ثبت نشده است.',
    loadingText: 'در حال دریافت تاریخچه خروج کالا...',
    zeroText: 'خروج کالایی پیدا نشد.',
    onDraw: refreshHighlight,
  })
}

const tableOptionsByMode: Record<TableMode, Config> = {
  items: tableOptions('items'),
  issues: tableOptions('issues'),
}

watch(tableMode, () => {
  closeMenu()
})

async function openCreate(): Promise<void> {
  if (!await options.load()) return
  formModal.value?.openCreate()
}

async function openEdit(row: GoodsIssue | GoodsIssueItemRow): Promise<void> {
  if (!canUpdate || editLoading.value) return
  editLoading.value = true
  try {
    if (!await options.load()) return
    await formModal.value?.openEdit(row)
  } finally {
    editLoading.value = false
  }
}

async function removeDocument(row: GoodsIssue | GoodsIssueItemRow): Promise<void> {
  if (!canDelete || deletingId.value) return
  const id = 'goodsIssueId' in row ? row.goodsIssueId : row.id
  deletingId.value = id
  try {
    await formModal.value?.removeDocument(row)
  } finally {
    deletingId.value = null
  }
}

function onSaved(reason: 'saved' | 'deleted'): void {
  void options.reload()
  list.reload(reason === 'saved')
}

const printChannel = useRequestChannel()
async function printDocument(row: GoodsIssue | GoodsIssueItemRow): Promise<void> {
  if (!canExport || printingId.value) return
  const controller = printChannel.begin()
  const id = 'goodsIssueId' in row ? row.goodsIssueId : row.id
  printingId.value = id
  try {
    const pdf = await getGoodsIssuePdf(id, controller.signal)
    await printPdf(pdf, `چاپ سند خروج ${row.issueNumber}`, controller.signal)
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'چاپ سند خروج انجام نشد.')
  } finally {
    if (printChannel.isCurrent(controller)) {
      printChannel.end(controller)
      printingId.value = null
    }
  }
}

const exportChannel = useRequestChannel()
function onImportFile(file: File): void {
  if (!canImport) return
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

function onImported(summary: GoodsIssueImportSummary): void {
  importSummary.value = summary
  if (summary.importedIssueCount > 0) {
    void options.reload()
    list.reload(true)
    toast.success(`${summary.importedIssueCount} سند خروج و ${summary.importedItemCount} قلم کالا ثبت شد.`)
  }
}

async function exportItems(): Promise<void> {
  const controller = exportChannel.begin()
  exporting.value = true
  try {
    const file = await exportGoodsIssueItems({ ...currentFilterParameters(), signal: controller.signal })
    downloadBlob(file, `goods-issues-${new Date().toISOString().slice(0, 10)}.xlsx`)
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'دریافت خروجی Excel انجام نشد.')
  } finally {
    if (exportChannel.isCurrent(controller)) {
      exportChannel.end(controller)
      exporting.value = false
    }
  }
}

onBeforeUnmount(() => {
  if (highlightFrame !== null) cancelAnimationFrame(highlightFrame)
})
</script>

<template>
  <Teleport to="body">
    <div v-if="printingId !== null" class="goods-issue-print-loading" role="status" aria-live="polite" aria-busy="true" dir="rtl">
      <div class="goods-issue-print-loading__content">
        <span class="spinner-border text-primary" aria-hidden="true"></span>
        <span>در حال آماده‌سازی چاپ سند خروج…</span>
      </div>
    </div>
  </Teleport>
  <GoodsIssueFormModal ref="formModal" :people="people" :units="units" @saved="onSaved" />
  <GoodsIssueImportModal :model-value="importOpen" :file="importFile" @update:model-value="onImportOpenChange"
                         @imported="onImported" />

  <div class="container-fluid users-page goods-issues-page inventory-data-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item active"><span>تاریخچه خروج کالا</span></li>
      </ol>
    </div>

    <div class="card">
      <div class="card-header">
        <div>
          <h4 class="card-title"><i class="las la-dolly-flatbed mr-1"></i> تاریخچه خروج کالا</h4>
          <p class="page-description mb-0 mt-1 text-muted">ثبت و پیگیری اسناد و ردیف‌های مستقل خروج کالا</p>
        </div>
        <InventoryListToolbar :mode="tableMode" :modes="VIEW_MODES" entity-label="خروجی کالا"
                              create-label="ثبت خروج کالا" :can-create="canCreate" :can-import="canImport" :can-export="canExport"
                              :create-disabled="optionsLoading" :exporting="exporting"
                              :columns="toggleable[tableMode]" :is-column-visible="isColumnVisible"
                              :column-menu-open="menuOpen"
                              @update:mode="tableMode = $event as TableMode" @create="openCreate" @import="onImportFile" @export="exportItems"
                              @toggle-column="toggleColumn" @toggle-column-menu="toggleMenu" />
      </div>

      <div class="card-body">
        <div v-if="importSummary" class="alert" :class="importSummary.invalidRows.length ? 'alert-danger' : 'alert-success'" role="status">
          <template v-if="importSummary.invalidRows.length">
            <p>هیچ سند یا خارج‌کننده‌ای ثبت نشد. خطاها را اصلاح و فایل را دوباره ارسال کنید.</p>
            <ul class="mb-0 import-errors">
              <li v-for="row in importSummary.invalidRows" :key="`${row.sheet}-${row.rowNumber}`">
                شیت {{ row.sheet }}، ردیف {{ row.rowNumber }}: {{ row.message }}
              </li>
            </ul>
          </template>
          <template v-else>
            {{ importSummary.importedIssueCount }} سند خروج، {{ importSummary.importedItemCount }} قلم کالا و
            {{ importSummary.createdIssuerCount }} خارج‌کنندهٔ جدید ثبت شد.
          </template>
        </div>
        <DataTableFilters v-model="appliedFilters" :definitions="filterDefinitions" :search="quickSearch"
                          :loading="listLoading"
                          search-placeholder="جستجو در کد خروج، کالا، مقصد، مکان ارسال و خارج‌کننده..."
                          @update:search="filters.onSearch" @apply="filters.onApply" @clear="filters.onClear"
                          @remove="filters.onRemove">
          <template #filter-productTitle="{ setValue, error }">
            <ProductSelect :model-value="filters.selection('productTitle').id"
                           :text="filters.selection('productTitle').text" label="عنوان کالا"
                           placeholder="جستجو و انتخاب کالا" size="sm" :error="error"
                           @update:model-value="filters.setSelection('productTitle', { id: $event })"
                           @update:text="filters.setSelection('productTitle', { text: $event })"
                           @select="selectProductFilter($event, setValue)" @clear="clearProductFilter(setValue)" />
          </template>
          <template #filter-destinationId="{ value, setValue, error }">
            <GoodsIssueDestinationSelect :model-value="asId(value)"
                                         :text="filters.selection('destinationId').text" label="مقصد"
                                         placeholder="جستجو و انتخاب مقصد" size="sm" :error="error"
                                         @update:model-value="setValue($event)"
                                         @update:text="filters.setSelection('destinationId', { text: $event })"
                                         @select="selectDestinationFilter($event, setValue)"
                                         @clear="filters.setSelection('destinationId', { id: null, text: '' }); setValue(null)" />
          </template>
          <template #filter-issuerId="{ value, setValue, error }">
            <PersonSelect :model-value="asId(value)" :text="filters.selection('issuerId').text" :options="people"
                          label="خارج‌کننده" placeholder="جستجو و انتخاب خارج‌کننده" size="sm" :error="error"
                          @update:model-value="setValue($event)"
                          @update:text="filters.setSelection('issuerId', { text: $event })"
                          @select="(person: PersonSelectOption) => { filters.setSelection('issuerId', { id: person.id, text: person.fullName }); setValue(person.id) }" />
          </template>
        </DataTableFilters>

        <div ref="tableContainer" class="users-table-container inventory-table-container"
             :class="`inventory-table-container--${tableMode}`">
          <DataTable :key="tableMode" ref="table" :options="tableOptionsByMode[tableMode]"
                     class="display responsive nowrap table-striped table table-sm users-table inventory-data-table">
            <template #column-issuedAtUtc="{ cellData }"><strong class="text-black">{{ formatPersianDate(cellData) }}</strong></template>
            <template #column-issueNumber="{ cellData }"><span dir="ltr">{{ cellData }}</span></template>
            <template #column-productCode="{ cellData }"><span dir="ltr">{{ cellData }}</span></template>
            <template #column-productTitle="{ cellData }">
              <strong class="text-black" :title="String(cellData ?? '')">{{ cellData || '—' }}</strong>
            </template>
            <template #column-quantity="{ cellData }"><span class="inventory-quantity-cell">{{ formatPersianNumber(cellData) }}</span></template>
            <template #column-assetNumber="{ cellData }"><span dir="ltr">{{ cellData || '—' }}</span></template>
            <template #column-exitReason="{ cellData, rowData }">
              <span class="badge light badge-pill goods-issue-exit-reason-chip"
                    :class="exitReasonBadges[cellData as GoodsIssueExitReason] || 'badge-dark'"
                    :title="exitReasonText(cellData as GoodsIssueExitReason, (rowData as GoodsIssueItemRow).legacyExitReason)">
                {{ exitReasonText(cellData as GoodsIssueExitReason, (rowData as GoodsIssueItemRow).legacyExitReason) }}
              </span>
            </template>
            <template #column-description="{ cellData }"><span class="inventory-description-cell" :title="cellData || ''">{{ cellData || '—' }}</span></template>
            <template #column-issueDescription="{ cellData }"><span class="inventory-description-cell" :title="cellData || ''">{{ cellData || '—' }}</span></template>
            <template #column-returnAtUtc="{ cellData }">{{ formatPersianDate(cellData) }}</template>
            <template #column-isReturnable="{ cellData }">
              <span class="badge" :class="cellData ? 'light badge-success' : 'light badge-secondary'">{{ cellData ? 'بله' : 'خیر' }}</span>
            </template>
            <template #column-itemCount="{ cellData }"><span class="badge light badge-info">{{ (cellData as GoodsIssue['items']).length }} کالا</span></template>
            <template v-if="canChange || canExport" #column-actions="{ rowData }">
              <div class="d-flex justify-content-center">
                <button v-if="canExport" class="btn users-table__role-action" type="button" title="چاپ سند خروج"
                        aria-label="چاپ سند خروج" :disabled="printingId !== null"
                        @click="printDocument(rowData)">
                  <span v-if="printingId === ('goodsIssueId' in rowData ? rowData.goodsIssueId : rowData.id)"
                        class="spinner-border spinner-border-sm" aria-hidden="true"></span>
                  <i v-else class="las la-print" aria-hidden="true"></i>
                </button>
                <button v-if="canUpdate" class="btn users-table__role-action" type="button" title="ویرایش سند"
                        aria-label="ویرایش سند" :disabled="editLoading || deletingId !== null"
                        @click="openEdit(rowData)"><i class="las la-edit" aria-hidden="true"></i></button>
                <button v-if="canDelete" class="btn users-table__role-action text-danger" type="button" title="حذف سند"
                        aria-label="حذف سند" :disabled="editLoading || deletingId !== null"
                        @click="removeDocument(rowData)"><span v-if="deletingId === ('goodsIssueId' in rowData ? rowData.goodsIssueId : rowData.id)"
                        class="spinner-border spinner-border-sm" aria-hidden="true"></span><i v-else class="las la-trash" aria-hidden="true"></i></button>
              </div>
            </template>
          </DataTable>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.goods-issue-exit-reason-chip {
  max-width: 100%;
  padding: .25rem .4rem;
  font-size: .75rem;
  line-height: 1.5;
  white-space: normal;
  overflow-wrap: anywhere;
}
.goods-issue-print-loading {
  position: fixed;
  inset: 0;
  z-index: 100100;
  display: grid;
  place-items: center;
  background: rgba(0, 0, 0, .35);
  cursor: wait;
}
.goods-issue-print-loading__content {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  padding: 1.75rem 2rem;
  margin: 1rem;
  border-radius: .75rem;
  background: var(--surface, #fff);
  color: var(--text-primary, #222);
  box-shadow: 0 .5rem 2rem rgba(0, 0, 0, .15);
  text-align: center;
}
@media print {
  .goods-issue-print-loading { display: none; }
}
.goods-issues-page :deep(.inventory-table-container table.dataTable > thead > tr > th),
.goods-issues-page :deep(.inventory-table-container table.dataTable > tbody > tr > td) {
  padding-left: 2px !important;
  padding-right: 2px !important;
}
.goods-issues-page :deep(.dt-layout-table > div) {
  max-height: 50vh;
  overflow: auto !important;
  overscroll-behavior: contain;
  scrollbar-width: thin;
}
.import-errors { max-height: 20rem; overflow-y: auto; }
</style>
