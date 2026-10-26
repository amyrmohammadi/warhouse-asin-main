<script setup lang="ts">
import type { ColumnsConfig, Config } from 'datatables.net'
import DataTablesCore from 'datatables.net-bs4'
import DataTable from 'datatables.net-vue3'
import { computed, nextTick, onBeforeUnmount, ref } from 'vue'

import { highlightTableText } from '@/components/UseTableHighlight'
import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import InventoryListToolbar from '@/components/InventoryListToolbar.vue'
import { useColumnVisibility } from '@/composables/useColumnVisibility'
import { useCompactScrollbar } from '@/composables/useCompactScrollbar'
import { useDataTableList } from '@/composables/useDataTableList'
import { useFilterState } from '@/composables/useFilterState'
import { usePermissions } from '@/composables/usePermissions'
import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'
import {
  getInvoice,
  getInvoices,
  updateInvoiceItem,
  type InvoiceDetails,
  type InvoiceItem,
  type InvoiceListParameters,
  type InvoiceSummary,
} from '@/services/invoices'
import { toast } from '@/services/toast'
import type { DataTableFilterDefinition } from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'
import { formatPersianNumber } from '@/utils/dateTime'
import { createInventoryTableOptions } from '@/utils/inventoryTable'
import '@/styles/inventory-list.css'

import InvoiceItemsModal from './InvoiceItemsModal.vue'

defineOptions({ name: 'InvoicesView' })
DataTable.use(DataTablesCore)

type TableMode = 'invoices'
const VIEW_MODES = [{ value: 'invoices', label: 'فاکتورها' }]
const tableMode = ref<TableMode>('invoices')
const tableContainer = ref<HTMLElement | null>(null)
const detailsOpen = ref(false)
const selectedInvoice = ref<InvoiceSummary | null>(null)
const invoiceDetails = ref<InvoiceDetails | null>(null)
const detailsLoading = ref(false)
const detailsError = ref<string | null>(null)
const editingItem = ref<InvoiceItem | null>(null)
const editSaving = ref(false)
const editError = ref<string | null>(null)

const { has } = usePermissions()
const canEditItems = has('GoodsReceiptsReceiptInvoiceOperations')

const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/invoices')
const filters = useFilterState(() => list.reload(true))
const { search: quickSearch, applied: appliedFilters } = filters

const filterDefinitions = computed<DataTableFilterDefinition[]>(() => [
  { key: 'invoiceNumber', label: 'شماره فاکتور', type: 'text-comparison', placeholder: 'شماره فاکتور', maxlength: 100 },
  { key: 'counterparty', label: 'طرف حساب', type: 'text-comparison', placeholder: 'نام طرف حساب', maxlength: 256 },
])

function currentParameters(): InvoiceListParameters {
  return {
    search: filters.searchQuery.value,
    invoiceNumber: filters.text('invoiceNumber'),
    counterparty: filters.text('counterparty'),
  }
}

const list = useDataTableList<InvoiceSummary>({
  errorMessage: 'دریافت فهرست فاکتورها انجام نشد.',
  onUnauthorized: handleUnauthorized,
  fetchPage: query => getInvoices({
    ...currentParameters(),
    pageNumber: query.page,
    pageSize: query.pageSize,
    sortBy: query.sortBy,
    sortDescending: query.sortDescending,
    signal: query.signal,
  }),
})
const { table, loading: listLoading } = list

const columns: Record<TableMode, ColumnsConfig[]> = {
  invoices: [
    { ...createRowNumberColumn(), className: 'min-tablet-p text-center data-table-row-number' },
    { data: 'invoiceNumber', name: 'invoiceNumber', title: 'شماره فاکتور', className: 'all text-center' },
    { data: 'counterparty', name: 'counterparty', title: 'طرف حساب', className: 'all' },
    {
      data: 'receiptNumbers', name: 'receiptNumbers', title: 'شماره رسیدها',
      className: 'min-tablet-p text-center invoice-receipts-column', orderable: false, searchable: false,
    },
    {
      data: 'itemsWithoutReceiptCount', name: 'itemsWithoutReceiptCount', title: 'اقلام بدون رسید',
      className: 'min-tablet-p text-center text-bold', orderable: false, searchable: false,
    },
    {
      data: 'id', name: 'actions', title: 'کالاها', className: 'all text-center actions-cell',
      orderable: false, searchable: false,
    },
  ],
}

const compact = useCompactScrollbar(tableContainer)
const visibility = useColumnVisibility<TableMode>({
  storageKey: 'warehouse.invoices.hidden-columns.v1',
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
} = visibility

let highlightFrame: number | null = null
function refreshHighlight(): void {
  if (highlightFrame !== null) cancelAnimationFrame(highlightFrame)
  void nextTick(() => {
    highlightFrame = requestAnimationFrame(() => {
      highlightFrame = null
      highlightTableText(tableContainer.value, quickSearch.value)
    })
  })
}

const tableOptions: Config = createInventoryTableOptions({
  ajax: list.ajax,
  columns: visibility.resolvedColumns.invoices,
  order: { name: 'invoiceNumber', dir: 'desc' },
  emptyText: 'هنوز فاکتوری ثبت نشده است.',
  loadingText: 'در حال دریافت فاکتورها...',
  zeroText: 'فاکتوری با این مشخصات پیدا نشد.',
  onDraw: () => { compact.schedule(); refreshHighlight() },
  onInit: compact.schedule,
})

const detailsChannel = useRequestChannel()
async function openItems(invoice: InvoiceSummary): Promise<void> {
  const controller = detailsChannel.begin()
  selectedInvoice.value = invoice
  invoiceDetails.value = null
  detailsError.value = null
  detailsLoading.value = true
  detailsOpen.value = true
  try {
    invoiceDetails.value = await getInvoice(invoice.id, controller.signal)
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    detailsError.value = error instanceof Error ? error.message : 'دریافت کالاهای فاکتور انجام نشد.'
  } finally {
    if (detailsChannel.isCurrent(controller)) {
      detailsChannel.end(controller)
      detailsLoading.value = false
    }
  }
}

function closeDetails(value: boolean): void {
  detailsOpen.value = value
  if (!value) {
    detailsChannel.abort()
    detailsLoading.value = false
    editingItem.value = null
  }
}

function openItemEdit(item: InvoiceItem): void {
  editingItem.value = item
  editError.value = null
}

const editChannel = useRequestChannel()
async function saveItem(receiptNumber: string | null): Promise<void> {
  const invoice = selectedInvoice.value
  const item = editingItem.value
  if (!invoice || !item || editSaving.value) return

  const controller = editChannel.begin()
  editSaving.value = true
  editError.value = null
  try {
    const result = await updateInvoiceItem(invoice.id, item.id, {
      receiptNumber,
      concurrencyStamp: item.goodsReceiptConcurrencyStamp,
    }, controller.signal)

    for (const current of invoiceDetails.value?.items ?? []) {
      if (current.goodsReceiptId === item.goodsReceiptId)
        current.goodsReceiptConcurrencyStamp = result.concurrencyStamp
      if (current.id === result.id) current.receiptNumber = result.receiptNumber
    }
    editingItem.value = null
    list.reload(false)
    toast.success(receiptNumber ? 'شماره رسید با موفقیت ذخیره شد.' : 'شماره رسید حذف شد.')
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    editError.value = error instanceof Error ? error.message : 'ذخیره شماره رسید انجام نشد.'
  } finally {
    if (editChannel.isCurrent(controller)) {
      editChannel.end(controller)
      editSaving.value = false
    }
  }
}

function cancelItemEdit(): void {
  if (!editSaving.value) {
    editChannel.abort()
    editingItem.value = null
    editError.value = null
  }
}

onBeforeUnmount(() => {
  if (highlightFrame !== null) cancelAnimationFrame(highlightFrame)
  detailsChannel.abort()
  editChannel.abort()
})
</script>

<template>
  <InvoiceItemsModal :model-value="detailsOpen" :invoice="selectedInvoice" :details="invoiceDetails"
                     :loading="detailsLoading" :error="detailsError" :can-edit="canEditItems"
                     :editing-item-id="editingItem?.id ?? null" :saving="editSaving"
                     :edit-error="editError" @edit="openItemEdit" @save="saveItem"
                     @cancel-edit="cancelItemEdit" @update:model-value="closeDetails" />

  <div class="container-fluid users-page invoices-page inventory-data-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item active"><span>فاکتورها</span></li>
      </ol>
    </div>

    <div class="card">
      <div class="card-header">
        <div>
          <h4 class="card-title"><i class="las la-file-invoice mr-1"></i> فاکتورها</h4>
          <p class="page-description mb-0 mt-1 text-muted">مشاهده فاکتورها و کالاهای ثبت‌شده در هر فاکتور</p>
        </div>
        <InventoryListToolbar :mode="tableMode" :modes="VIEW_MODES" entity-label="فاکتور" create-label=""
                              :can-create="false" :columns="toggleable[tableMode]"
                              :is-column-visible="isColumnVisible" :column-menu-open="menuOpen"
                              @update:mode="tableMode = $event as TableMode"
                              @toggle-column="toggleColumn" @toggle-column-menu="toggleMenu" />
      </div>

      <div class="card-body">
        <DataTableFilters v-model="appliedFilters" :definitions="filterDefinitions" :search="quickSearch"
                          :loading="listLoading" search-placeholder="جستجو در شماره فاکتور و طرف حساب..."
                          @update:search="filters.onSearch" @apply="filters.onApply" @clear="filters.onClear"
                          @remove="filters.onRemove" />

        <div ref="tableContainer" class="users-table-container inventory-table-container inventory-table-container--invoices"
             @wheel="compact.onWheel">
          <DataTable ref="table" :options="tableOptions"
                     class="display responsive nowrap table-striped table table-sm users-table inventory-data-table">
            <template #column-invoiceNumber="{ cellData }">
              <strong class="invoice-number" dir="ltr">{{ cellData }}</strong>
            </template>
            <template #column-counterparty="{ cellData }">{{ cellData || '—' }}</template>
            <template #column-receiptNumbers="{ cellData }">
              <div v-if="(cellData as string[]).length" class="invoice-receipt-numbers">
                <span v-for="receiptNumber in (cellData as string[])" :key="receiptNumber"
                      class="badge light badge-info" dir="ltr">{{ receiptNumber }}</span>
              </div>
              <span v-else>—</span>
            </template>
            <template #column-itemsWithoutReceiptCount="{ cellData }">
              <span class="badge light" :class="cellData > 0 ? 'badge-warning' : 'badge-success'">
                {{ formatPersianNumber(cellData) }} کالا
              </span>
            </template>
            <template #column-actions="{ rowData }">
              <button class="btn users-table__role-action" type="button" title="مشاهده کالاهای فاکتور"
                      aria-label="مشاهده کالاهای فاکتور" @click="openItems(rowData)">
                <i class="las la-boxes" aria-hidden="true"></i>
              </button>
            </template>
          </DataTable>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.invoice-receipt-numbers {
  display: flex;
  flex-wrap: wrap;
  gap: .3rem;
  justify-content: center;
}
.invoice-number {
  color: var(--text-primary, #212529);
  font-weight: 700;
}
</style>
