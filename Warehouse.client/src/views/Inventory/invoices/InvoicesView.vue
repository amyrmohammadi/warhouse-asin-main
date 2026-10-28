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
  updateInvoice,
  type InvoiceDetails,
  type InvoiceListParameters,
  type InvoiceSummary,
  type UpdateInvoiceRequest,
} from '@/services/invoices'
import { toast } from '@/services/toast'
import type { DataTableFilterDefinition } from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'
import { formatPersianDate, formatPersianNumber } from '@/utils/dateTime'
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
const invoiceDetails = ref<InvoiceDetails | null>(null)
const detailsLoading = ref(false)
const detailsError = ref<string | null>(null)
const editMode = ref(false)
const editSaving = ref(false)

const { has } = usePermissions()
const canViewMissingReceiptCount = has('InvoicesMissingReceiptCountRead')
const canEditInvoice = has('GoodsReceiptsReceiptInvoiceOperations')
const canEditReceipt = has('GoodsReceiptsReceiptRegistration')
const canEdit = canEditInvoice || canEditReceipt
const statusLabel = {
  UnderReview: 'در حال رسیدگی',
  SentToFinance: 'ارسال به واحد مالی',
  FinanceApproved: 'تأیید واحد مالی',
}

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
    { data: 'invoiceDate', name: 'invoiceDate', title: 'تاریخ فاکتور', className: 'min-tablet-l text-center' },
    { data: 'counterparty', name: 'counterparty', title: 'طرف حساب', className: 'all' },
    { data: 'processingStatus', name: 'processingStatus', title: 'وضعیت', className: 'min-tablet-l text-center' },
    { data: 'description', name: 'description', title: 'توضیحات', className: 'min-desktop', orderable: false },
    {
      data: 'receiptNumbers', name: 'receiptNumbers', title: 'شماره رسیدها',
      className: 'min-tablet-p text-center invoice-receipts-column', orderable: false, searchable: false,
    },
    ...(canViewMissingReceiptCount
      ? [{
          data: 'itemsWithoutReceiptCount', name: 'itemsWithoutReceiptCount', title: 'اقلام بدون رسید',
          className: 'min-tablet-p text-center text-bold', orderable: false, searchable: false,
        }]
      : []),
    {
      data: 'id', name: 'actions', title: 'عملیات', className: 'all text-center actions-cell',
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
  invoiceDetails.value = null
  detailsError.value = null
  editMode.value = false
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
    editMode.value = false
  }
}

async function saveInvoice(values: UpdateInvoiceRequest): Promise<void> {
  const details = invoiceDetails.value
  if (!details || editSaving.value) return
  const controller = detailsChannel.begin()
  editSaving.value = true
  detailsError.value = null
  try {
    invoiceDetails.value = await updateInvoice(details.id, values, controller.signal)
    editMode.value = false
    list.reload(false)
    toast.success('تغییرات با موفقیت ذخیره شد.')
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    detailsError.value = error instanceof Error ? error.message : 'ویرایش فاکتور انجام نشد.'
  } finally {
    if (detailsChannel.isCurrent(controller)) {
      detailsChannel.end(controller)
      editSaving.value = false
    }
  }
}

onBeforeUnmount(() => {
  if (highlightFrame !== null) cancelAnimationFrame(highlightFrame)
  detailsChannel.abort()
})
</script>

<template>
  <InvoiceItemsModal :model-value="detailsOpen" :details="invoiceDetails"
                     :loading="detailsLoading" :error="detailsError" :can-edit="canEdit"
                     :can-edit-invoice="canEditInvoice" :can-edit-receipt="canEditReceipt"
                     :edit-mode="editMode" :saving="editSaving"
                     @update:edit-mode="editMode = $event; detailsError = null"
                     @save="saveInvoice" @update:model-value="closeDetails" />

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
            <template #column-invoiceDate="{ cellData }">{{ formatPersianDate(cellData) }}</template>
            <template #column-processingStatus="{ cellData }">{{ statusLabel[cellData as keyof typeof statusLabel] ?? '—' }}</template>
            <template #column-description="{ cellData }">{{ cellData || '—' }}</template>
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
              <button class="btn users-table__role-action" type="button" title="مشاهده فاکتور"
                      aria-label="مشاهده فاکتور" @click="openItems(rowData)">
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
