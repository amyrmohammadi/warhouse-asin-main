<script setup lang="ts">
  import {
    highlightTableText,
    clearTableHighlight
  } from '@/components/UseTableHighlight'

  import type { AjaxCallback, AjaxData, Api, ColumnsConfig, Config } from 'datatables.net'
  import DataTablesCore from 'datatables.net-bs4'
  import DataTable from 'datatables.net-vue3'
  import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, shallowRef, watch } from 'vue'
  import { useRouter } from 'vue-router'

  import ConfirmDialog from '@/components/ConfirmDialog.vue'
  import EntityHistoryModal from '@/components/EntityHistoryModal.vue'
  import DataTableFilters from '@/components/filters/DataTableFilters.vue'
  import PersonSelect, { type PersonSelectOption } from '@/components/selectors/PersonSelect.vue'
  import ProductSelect from '@/components/selectors/ProductSelect.vue'
  import SupplierSelect from '@/components/selectors/SupplierSelect.vue'
  import { getSession } from '@/services/auth'
  import {
    importGoodsReceipts,
    type GoodsReceiptImportSummary,
    createGoodsReceipt,
    getGoodsReceipt,
    getGoodsReceiptItemHistory,
    updateGoodsReceipt,
    type CreateGoodsReceiptRequest,
    deleteGoodsReceipt,
    exportGoodsReceiptItems,
    getGoodsReceiptItems,
    getGoodsReceiptOptions,
    getGoodsReceipts,
    GoodsReceiptsApiError,
    type GoodsReceipt,
    type GoodsReceiptItemRow,
    type EntityHistoryEntry,
    type GoodsReceiptListParameters,
    type InvoiceStatus,
    type InvoiceStatusFilter,
  } from '@/services/goodsReceipts'
  import type { Product } from '@/services/products'
  import { MasterDataApiError } from '@/services/masterData'
  import type { SupplierOption } from '@/services/suppliers'
  import { toast } from '@/services/toast'
  import type { UnitOfMeasure } from '@/services/unitsOfMeasure'
  import type {
    DataTableFilterDefinition,
    DataTableFilterValue,
    DataTableFilterValues,
    DateFilterValue,
    NumericFilterValue,
    TextFilterValue,
  } from '@/types/dataTableFilters'
  import { createRowNumberColumn } from '@/utils/dataTable'
  import DatePicker from 'vue3-persian-datetime-picker'

  defineOptions({ name: 'GoodsReceiptsView' })
  DataTable.use(DataTablesCore)

  type PersonOption = PersonSelectOption
  interface WarehouseOption { id: string; title: string }
  interface FormItem {
    key: number
    productText: string
    productId: string | null
    productCode: string
    unitOfMeasureId: string | null
    unitOfMeasureTitle: string
    warehouseId: string
    invoiceStatus: InvoiceStatus | null
    invoiceNumber: string
    supplierId: string
    supplierText: string
    receiptNumber: string
    quantity: number | null
    requestNumber: string
    description: string
  }

  let highlightFrame: number | null = null

  function refreshTableHighlight(): void {
    if (highlightFrame !== null) {
      cancelAnimationFrame(highlightFrame)
    }

    void nextTick(() => {
      highlightFrame = requestAnimationFrame(() => {
        highlightFrame = requestAnimationFrame(() => {
          highlightFrame = null

          highlightTableText(
            tableContainer.value,
            quickSearch.value
          )
        })
      })
    })
  }


  type ItemFocusField = 'code' | 'unit' | 'quantity'
  type ProductField = 'title' | 'code'
  type GoodsEntryMode = 'cards' | 'list'
  type TableMode = 'items' | 'receipts'
  type RtlScrollType = 'default' | 'negative' | 'reverse'
  interface ColumnSetting { key: string; label: string; index: number }

  interface ConfirmDialogInstance {
    ask: (options: {
      title: string
      text?: string
      icon?: 'warning'
      confirmButtonText?: string
      cancelButtonText?: string
      confirmButtonColor?: string
    }) => Promise<boolean>
  }

  const router = useRouter()
  const table = shallowRef<{ dt: Api } | null>(null)
  const tableContainer = ref<HTMLElement | null>(null)
  const confirmDialog = ref<ConfirmDialogInstance | null>(null)
  const importInput = ref<HTMLInputElement | null>(null)
  const importModalOpen = ref(false)
  const importFile = ref<File | null>(null)
  const importing = ref(false)
  const exporting = ref(false)
  const importSummary = ref<GoodsReceiptImportSummary | null>(null)
  let importRequest: AbortController | null = null
  let exportRequest: AbortController | null = null
  let itemHistoryRequest: AbortController | null = null
  const modalOpen = ref(false)
  const receiptInvoiceModalOpen = ref(false)
  const editingReceiptId = ref<string | null>(null)
  const editingConcurrencyStamp = ref<string | null>(null)
  const editLoading = ref(false)
  const saving = ref(false)
  const referenceLoading = ref(false)
  const formError = ref('')
  const validationErrors = reactive<Record<string, string>>({})
  const activeUnitMenu = ref<number | null>(null)
  const goodsEntryMode = ref<GoodsEntryMode>('list')
  const tableMode = ref<TableMode>('items')
  const quickSearch = ref('')
  const appliedFilters = ref<DataTableFilterValues>({})
  const productFilterId = ref<string | null>(null)
  const productFilterText = ref('')
  const supplierFilterText = ref('')
  const delivererFilterText = ref('')
  const filtersLoading = ref(false)
  const itemHistoryOpen = ref(false)
  const itemHistoryLoading = ref(false)
  const itemHistoryError = ref('')
  const itemHistoryTitle = ref('')
  const itemHistoryEntries = ref<EntityHistoryEntry[]>([])
  const columnSettingsOpen = ref(false)
  const people = ref<PersonOption[]>([])
  const warehouses = ref<WarehouseOption[]>([])
  const units = ref<UnitOfMeasure[]>([])
  const sessionPermissions = new Set(getSession()?.permissions ?? [])
  const canInitialEntry = sessionPermissions.has('GoodsReceiptsInitialEntry')
  const canReadItemHistory = sessionPermissions.has('GoodsReceiptHistoryRead')
  const canManageReceiptInvoice = sessionPermissions.has('GoodsReceiptsReceiptInvoiceOperations')
  const canManageMasterData = sessionPermissions.has('MasterDataManage')
  const canCreateProducts = sessionPermissions.has('ProductsCreate')
  const canImportGoodsReceipts = sessionPermissions.has('GoodsReceiptsImport')
  const canExportGoodsReceipts = sessionPermissions.has('GoodsReceiptsExport')
  const referenceDataLoaded = ref(false)
  let rowKey = 0
  let listRequest: AbortController | null = null
  let operationRequest: AbortController | null = null
  let referenceRequest: AbortController | null = null
  let listDescriptionResizeFrame: number | null = null
  let listDescriptionResizeScheduled = false
  let tableScrollBody: HTMLElement | null = null
  let compactHorizontalScrollbar: HTMLDivElement | null = null
  let compactHorizontalScrollbarContent: HTMLDivElement | null = null
  let tableScrollResizeObserver: ResizeObserver | null = null
  let scrollSyncInProgress = false
  let rtlScrollType: RtlScrollType | null = null
  const listModeMinimumRows = 8

  const form = reactive({
    enteredAtLocal: '',
    description: '',
    delivererId: '',
    delivererText: '',
    warehouseId: '',
    invoiceStatus: null as InvoiceStatus | null,
    invoiceNumber: '',
    supplierId: '',
    supplierText: '',
    receiptNumber: '',
    pageNumber: '',
    items: [] as FormItem[],
  })

  const filterDefinitions = computed<DataTableFilterDefinition[]>(() => [
    { key: 'enteredAt', label: 'تاریخ ورود', type: 'date-comparison' },
    {
      key: 'productTitle',
      label: 'کالا',
      type: 'text-comparison',
      placeholder: 'جستجو و انتخاب کالا',
      maxlength: 256,
    },
    {
      key: 'productCode',
      label: 'کد کالا',
      type: 'text-comparison',
      placeholder: 'بخشی از کد کالا',
      maxlength: 100,
    },
    {
      key: 'pageNumber',
      label: 'شماره صفحه',
      type: 'text-comparison',
      placeholder: 'شماره صفحه',
      maxlength: 100,
    },
    {
      key: 'quantity',
      label: 'مقدار',
      type: 'numeric-comparison',
      numericType: 'decimal',
      min: 0,
    },
    {
      key: 'warehouseId',
      label: 'انبار',
      type: 'select',
      placeholder: 'همه انبارها',
      options: warehouses.value.map(item => ({ value: item.id, label: item.title })),
    },
    {
      key: 'delivererId',
      label: 'تحویل‌دهنده',
      type: 'select',
      placeholder: 'همه تحویل‌دهندگان',
      options: people.value.map(item => ({ value: item.id, label: item.fullName })),
    },
    {
      key: 'invoiceStatus',
      label: 'وضعیت فاکتور',
      type: 'select',
      placeholder: 'همه وضعیت‌ها',
      options: [
        { value: 'NotEntered', label: 'وارد نشده' },
        { value: 'HasInvoice', label: 'دارد' },
        { value: 'DoesNotHave', label: 'ندارد' },
      ],
    },
    {
      key: 'invoiceNumber',
      label: 'شماره فاکتور',
      type: 'text-comparison',
      placeholder: 'شماره فاکتور',
      maxlength: 100,
    },
    {
      key: 'receiptNumber',
      label: 'شماره رسید',
      type: 'text-comparison',
      placeholder: 'شماره رسید',
      maxlength: 100,
    },
    {
      key: 'supplierId',
      label: 'طرف حساب',
      type: 'select',
      placeholder: 'همه طرف حساب‌ها',
      options: filterId(appliedFilters.value.supplierId) && supplierFilterText.value
        ? [{ value: filterId(appliedFilters.value.supplierId)!, label: supplierFilterText.value }]
        : [],
    },
  ])



  const receiptColumns: ColumnsConfig[] = [
    { ...createRowNumberColumn(), className: 'min-tablet-p text-center data-table-row-number' },
    { data: 'enteredAtUtc', name: 'enteredAtUtc', title: 'تاریخ ورود', className: 'all text-bold goods-receipts-date-column' },
    { data: 'pageNumber', name: 'pageNumber', title: 'شماره صفحه', className: 'min-tablet-l' },
    { data: 'delivererFullName', name: 'delivererFullName', title: 'تحویل‌دهنده', className: 'min-tablet-l' },
    { data: 'items', name: 'itemCount', title: 'تعداد کالا', className: 'min-tablet-l text-bold', orderable: false },
    { data: 'description', name: 'description', title: 'توضیحات ورودی', className: 'min-tablet-p', orderable: false },
  ]

  const itemColumns: ColumnsConfig[] = [
    { ...createRowNumberColumn(), className: 'min-tablet-p text-center data-table-row-number' },
    { data: 'pageNumber', name: 'pageNumber', title: 'شماره صفحه', className: 'min-tablet-l' },
    { data: 'enteredAtUtc', name: 'enteredAtUtc', title: 'تاریخ', className: 'all text-bold goods-receipts-date-column' },
    { data: 'productTitle', name: 'productTitle', title: 'عنوان کالا', className: 'all goods-receipts-product-title-column' },
    { data: 'productCode', name: 'productCode', title: 'کد کالا', className: 'min-tablet-l' },
    { data: 'quantity', name: 'quantity', title: 'تعداد', className: 'min-tablet-l goods-receipts-quantity-column text-bold' },
    { data: 'unitOfMeasureTitle', name: 'unitOfMeasureTitle', title: 'واحد', className: 'min-tablet-l text-bold' },
    {
      data: 'itemDescription',
      name: 'itemDescription',
      title: 'توضیحات کالا',
      className: 'min-tablet-p goods-receipts-item-description-column',
      orderable: false,
    },
    { data: 'delivererFullName', name: 'delivererFullName', title: 'تحویل‌دهنده', className: 'min-desktop' },
    { data: 'invoiceStatus', name: 'invoiceStatus', title: 'فاکتور', className: 'min-desktop' },
    { data: 'invoiceNumber', name: 'invoiceNumber', title: 'شماره فاکتور', className: 'min-tablet-p text-nowrap', orderable: false },
    { data: 'counterparty', name: 'counterparty', title: 'طرف حساب', className: 'min-tablet-p text-nowrap', orderable: false },
    { data: 'receiptNumber', name: 'receiptNumber', title: 'شماره رسید', className: 'min-tablet-p text-nowrap', orderable: false },
    { data: 'warehouseTitle', name: 'warehouseTitle', title: 'انبار', className: 'min-desktop' },
  ]

  const receiptColumnSettings: ColumnSetting[] = [
    { key: 'enteredAtUtc', label: 'تاریخ ورود', index: 1 },
    { key: 'pageNumber', label: 'شماره صفحه', index: 2 },
    { key: 'delivererFullName', label: 'تحویل‌دهنده', index: 3 },
    { key: 'itemCount', label: 'تعداد کالا', index: 4 },
    { key: 'description', label: 'توضیحات ورودی', index: 5 },
  ]

  const itemColumnSettings: ColumnSetting[] = [
    { key: 'pageNumber', label: 'شماره صفحه', index: 1 },
    { key: 'enteredAtUtc', label: 'تاریخ', index: 2 },
    { key: 'productTitle', label: 'عنوان کالا', index: 3 },
    { key: 'productCode', label: 'کد کالا', index: 4 },
    { key: 'quantity', label: 'تعداد', index: 5 },
    { key: 'unitOfMeasureTitle', label: 'واحد', index: 6 },
    { key: 'itemDescription', label: 'توضیحات کالا', index: 7 },
    { key: 'delivererFullName', label: 'تحویل‌دهنده', index: 8 },
    { key: 'invoiceStatus', label: 'وضعیت فاکتور', index: 9 },
    { key: 'invoiceNumber', label: 'شماره فاکتور', index: 10 },
    { key: 'counterparty', label: 'طرف حساب', index: 11 },
    { key: 'receiptNumber', label: 'شماره رسید', index: 12 },
    { key: 'warehouseTitle', label: 'انبار', index: 13 },
  ]

  const columnVisibilityStorageKey = 'warehouse.goods-receipts.visible-columns.v2'

  function loadVisibleColumns(): Record<TableMode, string[]> {
    const defaults: Record<TableMode, string[]> = {
      items: itemColumnSettings.map(column => column.key),
      receipts: receiptColumnSettings.map(column => column.key),
    }

    try {
      const saved = window.localStorage.getItem(columnVisibilityStorageKey)
      if (!saved) return defaults
      const parsed = JSON.parse(saved) as Partial<Record<TableMode, unknown>>
      const savedItems = Array.isArray(parsed.items)
        ? parsed.items.filter((key): key is string => typeof key === 'string')
        : null
      const savedReceipts = Array.isArray(parsed.receipts)
        ? parsed.receipts.filter((key): key is string => typeof key === 'string')
        : null

      return {
        items: savedItems
          ? defaults.items.filter(key => savedItems.includes(key))
          : defaults.items,
        receipts: savedReceipts
          ? defaults.receipts.filter(key => savedReceipts.includes(key))
          : defaults.receipts,
      }
    } catch {
      return defaults
    }
  }

  const visibleColumns = reactive<Record<TableMode, string[]>>(loadVisibleColumns())

  itemColumnSettings.forEach(column => {
    if (itemColumns[column.index]) {
      itemColumns[column.index].visible = visibleColumns.items.includes(column.key)
    }
  })
  receiptColumnSettings.forEach(column => {
    if (receiptColumns[column.index]) {
      receiptColumns[column.index].visible = visibleColumns.receipts.includes(column.key)
    }
  })

  const activeColumnSettings = computed(() =>
    tableMode.value === 'items' ? itemColumnSettings : receiptColumnSettings,
  )

  if (canInitialEntry || canManageReceiptInvoice) {
    receiptColumns.push({
      data: 'id',
      name: 'actions',
      title: 'عملیات',
      className: 'min-tablet-p text-center',
      orderable: false,
      searchable: false,
    })
  }
  if (canInitialEntry || canManageReceiptInvoice || canReadItemHistory) {
    itemColumns.push({
      data: 'goodsReceiptId',
      name: 'actions',
      title: 'عملیات',
      className: 'min-tablet-p text-center',
      orderable: false,
      searchable: false,
    })
  }

  function getRtlScrollType(): RtlScrollType {
    if (rtlScrollType) return rtlScrollType

    const outer = document.createElement('div')
    const inner = document.createElement('div')
    outer.dir = 'rtl'
    outer.style.cssText = 'height:1px;overflow:scroll;position:absolute;top:-1000px;width:4px;'
    inner.style.width = '8px'
    outer.append(inner)
    document.body.append(outer)

    if (outer.scrollLeft > 0) {
      rtlScrollType = 'default'
    } else {
      outer.scrollLeft = 1
      rtlScrollType = outer.scrollLeft === 0 ? 'negative' : 'reverse'
    }

    outer.remove()
    return rtlScrollType
  }

  function tableNormalizedScrollLeft(element: HTMLElement): number {
    if (window.getComputedStyle(element).direction !== 'rtl') return element.scrollLeft

    const maximum = element.scrollWidth - element.clientWidth
    switch (getRtlScrollType()) {
      case 'negative': return maximum + element.scrollLeft
      case 'reverse': return maximum - element.scrollLeft
      default: return element.scrollLeft
    }
  }

  function setTableNormalizedScrollLeft(element: HTMLElement, value: number): void {
    if (window.getComputedStyle(element).direction !== 'rtl') {
      element.scrollLeft = value
      return
    }

    const maximum = element.scrollWidth - element.clientWidth
    switch (getRtlScrollType()) {
      case 'negative':
        element.scrollLeft = value - maximum
        break
      case 'reverse':
        element.scrollLeft = maximum - value
        break
      default:
        element.scrollLeft = value
    }
  }

  function updateCompactHorizontalScrollbar(): void {
    if (!tableScrollBody || !compactHorizontalScrollbar || !compactHorizontalScrollbarContent) return

    const tableMaximum = Math.max(0, tableScrollBody.scrollWidth - tableScrollBody.clientWidth)
    compactHorizontalScrollbar.hidden = tableMaximum === 0
    if (tableMaximum === 0) return

    const scrollbarViewport = compactHorizontalScrollbar.clientWidth || 300
    const contentWidth = scrollbarViewport * (tableScrollBody.scrollWidth / tableScrollBody.clientWidth)
    compactHorizontalScrollbarContent.style.width = `${Math.max(scrollbarViewport + 1, contentWidth)}px`

    const scrollbarMaximum = compactHorizontalScrollbar.scrollWidth - compactHorizontalScrollbar.clientWidth
    const tablePosition = tableNormalizedScrollLeft(tableScrollBody)
    compactHorizontalScrollbar.scrollLeft = scrollbarMaximum * (tablePosition / tableMaximum)
  }

  function syncCompactScrollbarFromTable(): void {
    if (scrollSyncInProgress || !tableScrollBody || !compactHorizontalScrollbar) return

    const tableMaximum = tableScrollBody.scrollWidth - tableScrollBody.clientWidth
    const scrollbarMaximum = compactHorizontalScrollbar.scrollWidth - compactHorizontalScrollbar.clientWidth
    if (tableMaximum <= 0 || scrollbarMaximum <= 0) return

    scrollSyncInProgress = true
    compactHorizontalScrollbar.scrollLeft = scrollbarMaximum * (tableNormalizedScrollLeft(tableScrollBody) / tableMaximum)
    scrollSyncInProgress = false
  }

  function syncTableFromCompactScrollbar(): void {
    if (scrollSyncInProgress || !tableScrollBody || !compactHorizontalScrollbar) return

    const tableMaximum = tableScrollBody.scrollWidth - tableScrollBody.clientWidth
    const scrollbarMaximum = compactHorizontalScrollbar.scrollWidth - compactHorizontalScrollbar.clientWidth
    if (tableMaximum <= 0 || scrollbarMaximum <= 0) return

    scrollSyncInProgress = true
    const nextPosition = tableMaximum * (compactHorizontalScrollbar.scrollLeft / scrollbarMaximum)
    setTableNormalizedScrollLeft(tableScrollBody, nextPosition)
    scrollSyncInProgress = false
  }

  function disconnectCompactHorizontalScrollbar(): void {
    tableScrollResizeObserver?.disconnect()
    tableScrollResizeObserver = null
    tableScrollBody?.removeEventListener('scroll', syncCompactScrollbarFromTable)
    compactHorizontalScrollbar?.removeEventListener('scroll', syncTableFromCompactScrollbar)
    compactHorizontalScrollbar?.remove()
    tableScrollBody = null
    compactHorizontalScrollbar = null
    compactHorizontalScrollbarContent = null
  }

  function connectCompactHorizontalScrollbar(): void {
    const nextScrollBody = tableContainer.value?.querySelector<HTMLElement>('.dt-layout-table > div') ?? null
    const scrollWrapper = tableContainer.value?.querySelector<HTMLElement>('.dt-layout-table') ?? null
    if (!nextScrollBody || !scrollWrapper) return
    if (nextScrollBody === tableScrollBody && compactHorizontalScrollbar?.isConnected) {
      updateCompactHorizontalScrollbar()
      return
    }

    disconnectCompactHorizontalScrollbar()
    tableScrollBody = nextScrollBody
    compactHorizontalScrollbar = document.createElement('div')
    compactHorizontalScrollbar.className = 'data-table-horizontal-scrollbar'
    compactHorizontalScrollbar.setAttribute('aria-label', 'اسکرول افقی جدول')
    compactHorizontalScrollbar.setAttribute('role', 'scrollbar')
    compactHorizontalScrollbarContent = document.createElement('div')
    compactHorizontalScrollbarContent.className = 'data-table-horizontal-scrollbar__content'
    compactHorizontalScrollbar.append(compactHorizontalScrollbarContent)
    scrollWrapper.insertAdjacentElement('afterend', compactHorizontalScrollbar)

    tableScrollBody.addEventListener('scroll', syncCompactScrollbarFromTable, { passive: true })
    compactHorizontalScrollbar.addEventListener('scroll', syncTableFromCompactScrollbar, { passive: true })
    tableScrollResizeObserver = new ResizeObserver(updateCompactHorizontalScrollbar)
    tableScrollResizeObserver.observe(tableScrollBody)
    updateCompactHorizontalScrollbar()
  }

  function scheduleCompactHorizontalScrollbar(): void {
    window.requestAnimationFrame(connectCompactHorizontalScrollbar)
  }

  function createTableOptions(
    columns: ColumnsConfig[],
    orderColumn: number,
    emptyTable: string,
  ): Config {
    return {
      ajax: loadTableRows,
      columns,
      serverSide: true,
      processing: true,
      searching: false,
      pageLength: 10,
      lengthMenu: [5, 10, 25, 50],
      order: [[orderColumn, 'desc']],
      autoWidth: false,
      pagingType: 'full_numbers',
      responsive: {
        details: {
          type: 'inline',
          renderer: DataTablesCore.Responsive.renderer.listHiddenNodes(),
        },
      },
      drawCallback: () => {
        scheduleCompactHorizontalScrollbar()
        refreshTableHighlight()
      },
      initComplete: scheduleCompactHorizontalScrollbar,
      layout: {
        topStart: null,
        topEnd: null,
        bottomStart: { className: 'users-table-summary', features: ['pageLength', 'info'] },
        bottomEnd: { className: 'users-table-pagination', features: ['paging'] },
      },
      language: {
        emptyTable,
        info: '_START_–_END_ از _TOTAL_',
        infoEmpty: '۰ از ۰',
        infoFiltered: '',
        lengthMenu: '_MENU_',
        loadingRecords: 'در حال دریافت تاریخچه ورود کالا...',
        processing: 'در حال پردازش...',
        zeroRecords: 'ورودی کالایی پیدا نشد.',
        paginate: { first: '<<', last: '>>', next: '>', previous: '<' },
      },

    }
  }

  const receiptTableOptions = createTableOptions(receiptColumns, 1, 'هنوز ورودی کالایی ثبت نشده است.')
  const itemTableOptions = createTableOptions(itemColumns, 2, 'هنوز کالایی وارد انبار نشده است.')

  const invoiceHasNumber = computed(() => form.invoiceStatus === 'HasInvoice')

  const productNotFoundText = computed(() => canCreateProducts
    ? 'کالایی پیدا نشد؛ اطلاعات کالای جدید را تکمیل کنید.'
    : 'کالایی پیدا نشد؛ مجوز افزودن کالای جدید را ندارید.')

  type SetFilterValue = (value: DataTableFilterValue) => void

  function filterId(value: DataTableFilterValue | undefined): string | null {
    return typeof value === 'string' && value ? value : null
  }

  function selectProductFilter(product: Product, setValue: SetFilterValue): void {
    productFilterId.value = product.id
    productFilterText.value = product.title
    setValue({ operator: 'eq', value: product.title })
  }

  function clearProductFilter(setValue: SetFilterValue): void {
    productFilterId.value = null
    productFilterText.value = ''
    setValue(null)
  }

  function selectSupplierFilter(supplier: SupplierOption, setValue: SetFilterValue): void {
    supplierFilterText.value = supplier.title
    setValue(supplier.id)
  }

  function selectDelivererFilter(person: PersonOption, setValue: SetFilterValue): void {
    delivererFilterText.value = person.fullName
    setValue(person.id)
  }

  function columnIsVisible(key: string): boolean {
    return visibleColumns[tableMode.value].includes(key)
  }

  function toggleColumnVisibility(column: ColumnSetting): void {
    const mode = tableMode.value
    const visible = visibleColumns[mode]
    const show = !visible.includes(column.key)
    if (!show && visible.length === 1) return

    visibleColumns[mode] = show
      ? [...visible, column.key]
      : visible.filter(key => key !== column.key)

    try {
      window.localStorage.setItem(columnVisibilityStorageKey, JSON.stringify({
        items: visibleColumns.items,
        receipts: visibleColumns.receipts,
      }))
    } catch {
      // Column visibility still works for the current session when storage is unavailable.
    }

    const columns = mode === 'items' ? itemColumns : receiptColumns
    if (columns[column.index]) columns[column.index].visible = show

    const api = table.value?.dt
    if (!api) return
    api.column(column.index).visible(show, false)
    api.columns.adjust().draw(false)
  }

  function toggleColumnSettings(): void {
    columnSettingsOpen.value = !columnSettingsOpen.value
  }

  function closeColumnSettings(): void {
    columnSettingsOpen.value = false
  }

  function handleTableWheel(event: WheelEvent): void {
    if (!event.ctrlKey || Math.abs(event.deltaY) < Math.abs(event.deltaX)) return

    const scrollBody = tableContainer.value?.querySelector<HTMLElement>('.dt-layout-table > div')
    if (!scrollBody || scrollBody.scrollWidth <= scrollBody.clientWidth) return

    event.preventDefault()
    const maximum = scrollBody.scrollWidth - scrollBody.clientWidth
    const direction = window.getComputedStyle(scrollBody).direction === 'rtl' ? -1 : 1
    const nextPosition = Math.min(maximum, Math.max(0, tableNormalizedScrollLeft(scrollBody) + (event.deltaY * direction)))
    setTableNormalizedScrollLeft(scrollBody, nextPosition)
  }

  function stringFilter(key: string): string | undefined {
    const value = appliedFilters.value[key]
    return typeof value === 'string' && value.trim() ? value.trim() : undefined
  }

  function textComparisonFilter(key: string): TextFilterValue | undefined {
    const value = appliedFilters.value[key]
    return typeof value === 'object' && value !== null && 'operator' in value
      && ['contains', 'eq'].includes(String(value.operator))
      ? value as TextFilterValue
      : undefined
  }

  function dateComparisonFilter(key: string): DateFilterValue | undefined {
    const value = appliedFilters.value[key]
    return typeof value === 'object' && value !== null && 'operator' in value
      && ['before', 'after', 'eq', 'between'].includes(String(value.operator))
      ? value as DateFilterValue
      : undefined
  }

  function numericFilter(key: string): NumericFilterValue | undefined {
    const value = appliedFilters.value[key]
    return typeof value === 'object' && value !== null && 'operator' in value
      && ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between'].includes(String(value.operator))
      ? value as NumericFilterValue
      : undefined
  }

  function reloadTable(resetPaging: boolean): void {
    table.value?.dt.ajax.reload(undefined, resetPaging)
  }

  function handleFilterSearch(value: string): void {
    quickSearch.value = value
    reloadTable(true)

  }

  function handleFilterApply(filters: DataTableFilterValues): void {
    appliedFilters.value = filters
    reloadTable(true)
  }

  function handleFilterClear(): void {
    quickSearch.value = ''
    appliedFilters.value = {}
    reloadTable(true)
  }

  function handleFilterRemove(filters: DataTableFilterValues): void {
    appliedFilters.value = filters
    reloadTable(true)
  }

  function currentFilterParameters(): GoodsReceiptListParameters {
    return {
      search: quickSearch.value || undefined,
      enteredAt: dateComparisonFilter('enteredAt'),
      productTitle: textComparisonFilter('productTitle'),
      productCode: textComparisonFilter('productCode'),
      pageNumberFilter: textComparisonFilter('pageNumber'),
      quantity: numericFilter('quantity'),
      invoiceNumber: textComparisonFilter('invoiceNumber'),
      receiptNumber: textComparisonFilter('receiptNumber'),
      warehouseId: stringFilter('warehouseId'),
      delivererId: stringFilter('delivererId'),
      supplierId: stringFilter('supplierId'),
      invoiceStatus: stringFilter('invoiceStatus') as InvoiceStatusFilter | undefined,
    }
  }

  async function exportItems(): Promise<void> {
    exportRequest?.abort()
    const controller = new AbortController()
    exportRequest = controller
    exporting.value = true
    try {
      const file = await exportGoodsReceiptItems({
        ...currentFilterParameters(),
        signal: controller.signal,
      })
      const url = URL.createObjectURL(file)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = `goods-receipts-${new Date().toISOString().slice(0, 10)}.xlsx`
      document.body.append(anchor)
      anchor.click()
      anchor.remove()
      window.setTimeout(() => URL.revokeObjectURL(url), 0)
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      if (await redirectWhenUnauthorized(error)) return
      toast.error(error instanceof Error ? error.message : 'دریافت خروجی Excel انجام نشد.')
    } finally {
      if (exportRequest === controller) {
        exportRequest = null
        exporting.value = false
      }
    }
  }

  async function loadTableRows(request: AjaxData, callback: AjaxCallback): Promise<void> {
    listRequest?.abort()
    const controller = new AbortController()
    listRequest = controller
    filtersLoading.value = true
    const start = request.start ?? 0
    const pageSize = request.length && request.length > 0 ? request.length : 10
    const orderedColumn = request.order?.[0]
    const columnName = orderedColumn ? request.columns?.[orderedColumn.column]?.name : undefined

    try {
      const parameters: GoodsReceiptListParameters = {
        ...currentFilterParameters(),
        pageNumber: Math.floor(start / pageSize) + 1,
        pageSize,
        sortBy: typeof columnName === 'string' ? columnName : undefined,
        sortDescending: orderedColumn?.dir === 'desc',
        signal: controller.signal,
      }
      const result = tableMode.value === 'items'
        ? await getGoodsReceiptItems(parameters)
        : await getGoodsReceipts(parameters)
      callback({
        draw: request.draw,
        recordsTotal: result.totalCount,
        recordsFiltered: result.filteredCount,
        data: result.items,
      })
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      if (await redirectWhenUnauthorized(error)) return
      toast.error(error instanceof Error ? error.message : 'دریافت اطلاعات ورود کالا انجام نشد.')
      callback({ draw: request.draw, recordsTotal: 0, recordsFiltered: 0, data: [] })
    } finally {
      if (listRequest === controller) filtersLoading.value = false
    }
  }

  function selectImportFile(event: Event): void {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file || importing.value || !canInitialEntry) return
    if (!/\.(xlsx|xlsm)$/i.test(file.name) || file.size === 0 || file.size > 50 * 1024 * 1024) {
      toast.error('فایل xlsx یا xlsm غیرخالی با حجم حداکثر ۵۰ مگابایت انتخاب کنید.')
      return
    }
    importFile.value = file
    importModalOpen.value = true
  }

  function closeImportModal(): void {
    if (importing.value) return
    importModalOpen.value = false
    importFile.value = null
  }

  async function startImport(): Promise<void> {
    if (!importFile.value || importing.value || !canInitialEntry) return
    const controller = new AbortController()
    importRequest = controller
    importing.value = true
    importSummary.value = null
    try {
      const result = await importGoodsReceipts(importFile.value, controller.signal)
      importSummary.value = result
      importModalOpen.value = false
      importFile.value = null
      if (result.importedReceiptCount > 0) {
        referenceDataLoaded.value = false
        table.value?.dt.ajax.reload(undefined, true)
        toast.success(`${result.importedReceiptCount} سند ورود و ${result.importedItemCount} قلم کالا ثبت شد.`)
      }
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      if (await redirectWhenUnauthorized(error)) return
      toast.error(error instanceof Error ? error.message : 'ایمپورت ورود کالا انجام نشد.')
    } finally {
      importing.value = false
      importRequest = null
    }
  }

  function localDateTimeNow(): string {
    const now = new Date()
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset())
    return now.toISOString().slice(0, 16)
  }

  function newItem(): FormItem {
    return {
      key: ++rowKey,
      productText: '',
      productId: null,
      productCode: '',
      unitOfMeasureId: null,
      unitOfMeasureTitle: '',
      warehouseId: '',
      invoiceStatus: null,
      invoiceNumber: '',
      supplierId: '',
      supplierText: '',
      receiptNumber: '',
      quantity: null,
      requestNumber: '',
      description: '',
    }
  }

  function resetForm(): void {
    activeUnitMenu.value = null
    goodsEntryMode.value = 'list'
    Object.assign(form, {
      enteredAtLocal: localDateTimeNow(),
      description: '',
      delivererId: '',
      delivererText: '',
      warehouseId: '',
      invoiceStatus: null as InvoiceStatus | null,
      invoiceNumber: '',
      supplierId: '',
      supplierText: '',
      receiptNumber: '',
      pageNumber: '',
      items: [newItem()],
    })
    ensureListRows()
    clearValidationErrors()
    formError.value = ''
  }

  async function loadReferenceData(): Promise<boolean> {
    if (referenceDataLoaded.value) return true

    referenceRequest?.abort()
    const controller = new AbortController()
    referenceRequest = controller
    referenceLoading.value = true
    try {
      const result = await getGoodsReceiptOptions(controller.signal)
      people.value = result.people
      warehouses.value = result.warehouses
      units.value = result.unitsOfMeasure
      referenceDataLoaded.value = true
      return true
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return false
      if (await redirectWhenUnauthorized(error)) return false
      toast.error(error instanceof Error ? error.message : 'دریافت اطلاعات فرم انجام نشد.')
      return false
    } finally {
      if (referenceRequest === controller) referenceLoading.value = false
    }
  }

  async function openCreateModal(): Promise<void> {
    if (!await loadReferenceData()) return
    resetForm()
    editingReceiptId.value = null
    editingConcurrencyStamp.value = null
    modalOpen.value = true
  }

  async function openEditModal(row: GoodsReceipt | GoodsReceiptItemRow): Promise<void> {
    if (editLoading.value || saving.value || (!canInitialEntry && !canManageReceiptInvoice)) return
    editLoading.value = true
    try {
      if (!await loadReferenceData()) return
      const id = 'goodsReceiptId' in row ? row.goodsReceiptId : row.id
      const receipt = await getGoodsReceipt(id)
      resetForm()
      editingReceiptId.value = id
      editingConcurrencyStamp.value = receipt.concurrencyStamp
      const date = new Date(receipt.enteredAtUtc)
      date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
      Object.assign(form, {
        enteredAtLocal: date.toISOString().slice(0, 16), description: receipt.description ?? '',
        delivererId: receipt.delivererId, delivererText: receipt.delivererFullName,
        pageNumber: receipt.pageNumber,
        items: receipt.items.map(item => ({
          ...newItem(), productId: item.productId, productText: item.productTitle,
          productCode: item.productCode, unitOfMeasureTitle: item.unitOfMeasureTitle,
          unitOfMeasureId: item.unitOfMeasureId,
          warehouseId: item.warehouseId, invoiceStatus: item.invoiceStatus,
          invoiceNumber: item.invoiceNumber ?? '', supplierId: item.supplierId ?? '',
          supplierText: item.counterparty ?? '', receiptNumber: item.receiptNumber ?? '',
          quantity: item.quantity, requestNumber: item.requestNumber ?? '', description: item.description ?? '',
        })),
      })
      ensureListRows()
      modalOpen.value = true
    } catch (error) {
      if (await redirectWhenUnauthorized(error)) return
      toast.error(error instanceof Error ? error.message : 'دریافت سند انجام نشد.')
    } finally {
      editLoading.value = false
    }
  }

  async function openItemHistory(row: GoodsReceiptItemRow): Promise<void> {
    if (!canReadItemHistory || itemHistoryLoading.value) return
    itemHistoryRequest?.abort()
    const controller = new AbortController()
    itemHistoryRequest = controller
    itemHistoryOpen.value = true
    itemHistoryLoading.value = true
    itemHistoryError.value = ''
    itemHistoryTitle.value = `${row.productTitle} (${row.productCode})`
    itemHistoryEntries.value = []
    try {
      const result = await getGoodsReceiptItemHistory(row.id, controller.signal)
      itemHistoryTitle.value = result.itemTitle
      itemHistoryEntries.value = result.entries
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      if (await redirectWhenUnauthorized(error)) return
      itemHistoryError.value = error instanceof Error
        ? error.message
        : 'دریافت تاریخچه تغییرات کالا انجام نشد.'
    } finally {
      if (itemHistoryRequest === controller) {
        itemHistoryRequest = null
        itemHistoryLoading.value = false
      }
    }
  }

  function closeItemHistory(): void {
    itemHistoryRequest?.abort()
    itemHistoryRequest = null
    itemHistoryOpen.value = false
    itemHistoryLoading.value = false
  }

  function closeReceiptInvoiceModal(): void {
    if (saving.value) return
    receiptInvoiceModalOpen.value = false
    editingReceiptId.value = null
    editingConcurrencyStamp.value = null
    formError.value = ''
  }

  async function saveReceiptInvoiceOperation(): Promise<void> {
    await saveReceipt()
  }

  function closeModal(): void {
    if (saving.value) return
    operationRequest?.abort()
    modalOpen.value = false
    formError.value = ''
  }

  function addItem(): void {
    form.items.push(newItem())
  }

  function removeItem(index: number): void {
    if (goodsEntryMode.value === 'cards' && form.items.length === 1) return
    form.items.splice(index, 1)
    if (goodsEntryMode.value === 'list') ensureListRows()
  }

  function itemHasContent(item: FormItem): boolean {
    return Boolean(
      item.productId ||
      item.productText.trim() ||
      item.productCode.trim() ||
      item.unitOfMeasureId ||
      item.unitOfMeasureTitle.trim() ||
      item.quantity !== null ||
      item.requestNumber.trim() ||
      item.description.trim(),
    )
  }

  function clearValidationErrors(): void {
    Object.keys(validationErrors).forEach(key => delete validationErrors[key])
  }

  function fieldError(key: string): string {
    return validationErrors[key] ?? ''
  }

  function clearFieldError(key: string): void {
    delete validationErrors[key]
  }

  type ItemValidationField = 'product' | 'code' | 'unit' | 'warehouse' | 'invoiceStatus' |
    'invoiceNumber' | 'supplier' | 'receiptNumber' | 'quantity' | 'requestNumber' | 'description'

  function itemFieldKey(item: FormItem, field: ItemValidationField): string {
    return `item.${item.key}.${field}`
  }

  function itemFieldError(item: FormItem, field: ItemValidationField): string {
    return fieldError(itemFieldKey(item, field))
  }

  function clearItemFieldError(item: FormItem, field: ItemValidationField): void {
    clearFieldError(itemFieldKey(item, field))
    clearFieldError('items')
  }

  function itemsForSubmission(): FormItem[] {
    return goodsEntryMode.value === 'list'
      ? form.items.filter(itemHasContent)
      : form.items
  }

  function ensureListRows(): void {
    if (goodsEntryMode.value !== 'list') return

    const previousLength = form.items.length
    while (form.items.length < listModeMinimumRows)
      form.items.push(newItem())
    const lastItem = form.items.at(-1)
    if (lastItem && itemHasContent(lastItem)) form.items.push(newItem())
    if (form.items.length !== previousLength) scheduleListDescriptionResize()
  }

  function setGoodsEntryMode(mode: GoodsEntryMode): void {
    if (goodsEntryMode.value === mode) return

    activeUnitMenu.value = null
    const populatedItems = form.items.filter(itemHasContent)
    goodsEntryMode.value = mode
    form.items.splice(
      0,
      form.items.length,
      ...(populatedItems.length ? populatedItems : [newItem()]),
    )
    if (mode === 'list') ensureListRows()
  }

  function handleListCellInput(item?: FormItem, field?: ItemValidationField): void {
    if (item && field) clearItemFieldError(item, field)
    ensureListRows()
  }

  function handleQuantityInput(item: FormItem): void {
    clearItemFieldError(item, 'quantity')
    ensureListRows()
  }

  function resizeListDescription(itemKey: number): void {
    const field = document.querySelector<HTMLTextAreaElement>(
      `textarea[data-list-description="${itemKey}"]`,
    )
    if (!field) return

    resizeListDescriptionField(field)
  }

  function resizeListDescriptionField(field: HTMLTextAreaElement): void {
    field.style.height = '0px'
    const borderHeight = field.offsetHeight - field.clientHeight
    const contentHeight = field.scrollHeight + borderHeight
    const cellHeight = field.closest<HTMLTableCellElement>('td')?.clientHeight ?? 0
    field.style.height = `${Math.max(contentHeight, cellHeight)}px`
  }

  function resizeAllListDescriptions(): void {
    document
      .querySelectorAll<HTMLTextAreaElement>('textarea[data-list-description]')
      .forEach(resizeListDescriptionField)
  }

  function scheduleListDescriptionResize(): void {
    if (listDescriptionResizeScheduled) return
    listDescriptionResizeScheduled = true
    void nextTick(() => {
      listDescriptionResizeFrame = window.requestAnimationFrame(() => {
        resizeAllListDescriptions()
        listDescriptionResizeFrame = null
        listDescriptionResizeScheduled = false
      })
    })
  }

  function handleListDescriptionInput(item: FormItem): void {
    clearItemFieldError(item, 'description')
    const itemKey = item.key
    resizeListDescription(itemKey)
    ensureListRows()
  }

  function productComparisonKey(value: string): string {
    return value
      .replace(/[\s\u200C\u2212\p{Dash_Punctuation}]+/gu, '')
      .toLocaleUpperCase('en-US')
  }

  function supplierComparisonKey(value: string): string {
    return value
      .replace(/\u200C/gu, ' ')
      .trim()
      .replace(/\s+/gu, ' ')
      .toLocaleUpperCase('fa-IR')
  }

  function selectProduct(item: FormItem, product: Product): void {
    inheritItemDetails(item)
    item.productId = product.id
    item.productText = product.title
    item.productCode = product.code
    item.unitOfMeasureId = product.unitOfMeasureId
    item.unitOfMeasureTitle = product.unitOfMeasureTitle
    item.description = product.description ?? ''
    clearItemFieldError(item, 'product')
    clearItemFieldError(item, 'code')
    clearItemFieldError(item, 'unit')
    ensureListRows()
    void nextTick(() => resizeListDescription(item.key))
    focusItemField(item, 'quantity')
  }

  function handleProductTextInput(item: FormItem, field: ProductField, value: string): void {
    if (field === 'title') item.productText = value
    else item.productCode = value

    inheritItemDetails(item)
    clearItemFieldError(item, field === 'title' ? 'product' : 'code')
    ensureListRows()
  }

  /** وقتی کاربر متن کالا را تغییر می‌دهد، انتخاب قبلی بی‌اعتبار می‌شود. */
  function clearProductSelection(item: FormItem, field: ProductField): void {
    item.productId = null
    item.unitOfMeasureId = null
    item.unitOfMeasureTitle = ''
    item.description = ''
    if (field === 'title') item.productCode = ''
    else item.productText = ''
  }

  function isNewDeliverer(): boolean {
    return !form.delivererId && Boolean(form.delivererText.trim())
  }

  function isNewSupplier(): boolean {
    return !form.supplierId && Boolean(form.supplierText.trim())
  }

  function isNewItemSupplier(item: FormItem): boolean {
    return !item.supplierId && Boolean(item.supplierText.trim())
  }

  function itemInvoiceHasNumber(item: FormItem): boolean {
    return item.invoiceStatus === 'HasInvoice'
  }

  function isNewProduct(item: FormItem): boolean {
    return !item.productId && Boolean(item.productText.trim() || item.productCode.trim())
  }

  function unitMenuIsOpen(item: FormItem): boolean {
    return activeUnitMenu.value === item.key
  }

  function openUnitMenu(item: FormItem): void {
    if (!item.productId) activeUnitMenu.value = item.key
  }

  function closeUnitMenu(item: FormItem): void {
    if (unitMenuIsOpen(item)) activeUnitMenu.value = null
  }

  function filteredUnitOptions(item: FormItem): UnitOfMeasure[] {
    const query = supplierComparisonKey(item.unitOfMeasureTitle)
    if (!query) return units.value.slice(0, 8)

    return units.value
      .filter(unit => supplierComparisonKey(unit.title).includes(query))
      .slice(0, 8)
  }

  function resolveUnit(item: FormItem): void {
    if (item.productId) return

    const match = units.value.find(
      unit => supplierComparisonKey(unit.title) === supplierComparisonKey(item.unitOfMeasureTitle),
    )
    if (match) {
      item.unitOfMeasureId = match.id
      item.unitOfMeasureTitle = match.title
      return
    }

    item.unitOfMeasureId = null
  }

  function handleUnitInput(item: FormItem): void {
    clearItemFieldError(item, 'unit')
    openUnitMenu(item)
    resolveUnit(item)
    ensureListRows()
  }

  function handleUnitBlur(item: FormItem): void {
    resolveUnit(item)
    window.setTimeout(() => closeUnitMenu(item), 120)
  }

  function chooseUnit(item: FormItem, unit: UnitOfMeasure): void {
    item.unitOfMeasureId = unit.id
    item.unitOfMeasureTitle = unit.title
    clearItemFieldError(item, 'unit')
    closeUnitMenu(item)
    ensureListRows()
    focusItemField(item, 'quantity')
  }

  function inheritItemDetails(item: FormItem): void {
    if (item.warehouseId || item.invoiceStatus || item.invoiceNumber ||
      item.supplierId || item.supplierText || item.receiptNumber) return

    const index = form.items.findIndex(candidate => candidate.key === item.key)
    if (index <= 0) return

    const previous = form.items[index - 1]
    if (!previous || !itemHasContent(previous)) return

    item.warehouseId = previous.warehouseId
    item.invoiceStatus = previous.invoiceStatus
    item.invoiceNumber = previous.invoiceNumber
    item.supplierId = previous.supplierId
    item.supplierText = previous.supplierText
    item.receiptNumber = previous.receiptNumber
  }

  function focusItemField(item: FormItem, field: ItemFocusField): void {
    void nextTick(() => {
      const holder = document.querySelector<HTMLElement>(
        `[data-receipt-item-key="${item.key}"][data-receipt-field="${field}"]`,
      )
      const input = holder?.matches('input')
        ? holder as HTMLInputElement
        : holder?.querySelector('input')
      input?.focus()
      input?.select()
    })
  }

  function advanceFromProductTitle(item: FormItem): void {
    if (item.productId) focusItemField(item, 'quantity')
    else if (item.productText.trim()) focusItemField(item, 'code')
  }

  function advanceFromProductCode(item: FormItem): void {
    focusItemField(item, item.productId ? 'quantity' : 'unit')
  }

  function advanceFromUnit(item: FormItem): void {
    resolveUnit(item)
    closeUnitMenu(item)
    focusItemField(item, 'quantity')
  }

  function normalizedRowText(value: string): string {
    return value.trim().replace(/\s+/g, ' ').toLocaleUpperCase('fa')
  }

  function itemDuplicateKey(item: FormItem): string {
    const product = item.productId
      ?? `new:${productComparisonKey(item.productText)}:${productComparisonKey(item.productCode)}:${item.unitOfMeasureId ?? ''}`
    const supplier = item.supplierId ?? `new:${normalizedRowText(item.supplierText)}`
    return JSON.stringify([
      product,
      item.warehouseId,
      item.invoiceStatus,
      normalizedRowText(item.invoiceNumber),
      supplier,
      normalizedRowText(item.receiptNumber),
      Number(item.quantity),
      normalizedRowText(item.requestNumber),
      normalizedRowText(item.description),
    ])
  }

  function hasSameNewProductDefinition(first: FormItem, second: FormItem): boolean {
    return productComparisonKey(first.productText) === productComparisonKey(second.productText)
      && productComparisonKey(first.productCode) === productComparisonKey(second.productCode)
      && first.unitOfMeasureId === second.unitOfMeasureId
  }

  function validateForm(): boolean {
    clearValidationErrors()
    if (!form.enteredAtLocal) validationErrors.enteredAtLocal = 'تاریخ و ساعت ورود کالا را وارد کنید.'
    if (!form.delivererText.trim()) {
      validationErrors.deliverer = 'تحویل‌دهنده را وارد یا انتخاب کنید.'
    } else if (isNewDeliverer() && !canManageMasterData) {
      validationErrors.deliverer = 'مجوز افزودن تحویل‌دهنده جدید را ندارید؛ از فهرست انتخاب کنید.'
    }
    if (!form.pageNumber.trim()) validationErrors.pageNumber = 'شماره صفحه را وارد کنید.'
    const items = itemsForSubmission()
    if (!items.length) validationErrors.items = 'حداقل یک کالا وارد کنید.'

    items.forEach(item => {
      if (!item.warehouseId) {
        validationErrors[itemFieldKey(item, 'warehouse')] = 'انبار را انتخاب کنید.'
      }
      if (canManageReceiptInvoice && itemInvoiceHasNumber(item) && !item.invoiceNumber.trim()) {
        validationErrors[itemFieldKey(item, 'invoiceNumber')] = 'شماره فاکتور را وارد کنید.'
      }
      if (canManageReceiptInvoice && isNewItemSupplier(item) && !canManageMasterData) {
        validationErrors[itemFieldKey(item, 'supplier')] =
          'مجوز افزودن تأمین‌کننده جدید را ندارید؛ از فهرست انتخاب کنید.'
      }
      if (!item.productText.trim()) {
        validationErrors[itemFieldKey(item, 'product')] = 'عنوان کالا را وارد کنید.'
      } else if (!item.productId && !canCreateProducts) {
        validationErrors[itemFieldKey(item, 'product')] = 'کالا را از فهرست انتخاب کنید؛ مجوز افزودن کالای جدید را ندارید.'
      }
      if (!item.quantity || item.quantity <= 0) {
        validationErrors[itemFieldKey(item, 'quantity')] = 'مقدار باید بزرگ‌تر از صفر باشد.'
      }
      if (!item.productId && !item.productCode.trim()) {
        validationErrors[itemFieldKey(item, 'code')] = 'کد کالای جدید را وارد کنید.'
      }
      if (!item.productId && !item.unitOfMeasureId) {
        validationErrors[itemFieldKey(item, 'unit')] = 'واحد را از فهرست موجود انتخاب کنید.'
      }
    })

    const itemRows = new Map<string, FormItem>()
    items.forEach(item => {
      const key = itemDuplicateKey(item)
      const previous = itemRows.get(key)
      if (previous) {
        const message = 'این ردیف کاملاً تکراری است؛ حداقل یکی از مشخصات آن را تغییر دهید.'
        validationErrors[itemFieldKey(previous, 'product')] = message
        validationErrors[itemFieldKey(item, 'product')] = message
      } else {
        itemRows.set(key, item)
      }
    })

    const newTitles = new Map<string, FormItem>()
    const newCodes = new Map<string, FormItem>()
    items.filter(item => !item.productId).forEach(item => {
      const titleKey = productComparisonKey(item.productText)
      const codeKey = productComparisonKey(item.productCode)
      if (titleKey) {
        const previousTitle = newTitles.get(titleKey)
        if (previousTitle && !hasSameNewProductDefinition(previousTitle, item)) {
          const message = 'کالاهای جدید با این عنوان باید کد و واحد یکسانی داشته باشند.'
          validationErrors[itemFieldKey(previousTitle, 'product')] = message
          validationErrors[itemFieldKey(item, 'product')] = message
        } else newTitles.set(titleKey, item)
      }
      if (codeKey) {
        const previousCode = newCodes.get(codeKey)
        if (previousCode && !hasSameNewProductDefinition(previousCode, item)) {
          const message = 'کالاهای جدید با این کد باید عنوان و واحد یکسانی داشته باشند.'
          validationErrors[itemFieldKey(previousCode, 'code')] = message
          validationErrors[itemFieldKey(item, 'code')] = message
        } else newCodes.set(codeKey, item)
      }
    })

    return Object.keys(validationErrors).length === 0
  }

  function applyApiFieldErrors(error: GoodsReceiptsApiError): boolean {
    let applied = false
    const items = itemsForSubmission()
    Object.entries(error.fieldErrors).forEach(([path, messages]) => {
      const message = messages.find(Boolean)
      if (!message) return
      const normalizedPath = path.toLowerCase()
      let key = ''
      const itemMatch = normalizedPath.match(/items\[(\d+)]\.(.+)$/)
      if (itemMatch) {
        const item = items[Number(itemMatch[1])]
        const property = itemMatch[2] ?? ''
        if (item) {
          if (property.includes('producttitle') || property.includes('productid')) key = itemFieldKey(item, 'product')
          else if (property.includes('productcode')) key = itemFieldKey(item, 'code')
          else if (property.includes('unitofmeasure')) key = itemFieldKey(item, 'unit')
          else if (property.includes('warehouse')) key = itemFieldKey(item, 'warehouse')
          else if (property.includes('invoicenumber')) key = itemFieldKey(item, 'invoiceNumber')
          else if (property.includes('invoicestatus')) key = itemFieldKey(item, 'invoiceStatus')
          else if (property.includes('supplier')) key = itemFieldKey(item, 'supplier')
          else if (property.includes('receiptnumber')) key = itemFieldKey(item, 'receiptNumber')
          else if (property.includes('quantity')) key = itemFieldKey(item, 'quantity')
          else if (property.includes('requestnumber')) key = itemFieldKey(item, 'requestNumber')
          else if (property.includes('description')) key = itemFieldKey(item, 'description')
        }
      }
      else if (normalizedPath.includes('enteredatutc')) key = 'enteredAtLocal'
      else if (normalizedPath.includes('deliverer')) key = 'deliverer'
      else if (normalizedPath.includes('warehouse')) key = 'warehouse'
      else if (normalizedPath.includes('pagenumber')) key = 'pageNumber'
      else if (normalizedPath.includes('invoicenumber')) key = 'invoiceNumber'
      else if (normalizedPath.includes('invoicestatus')) key = 'invoiceStatus'
      else if (normalizedPath.includes('supplier')) key = 'supplier'
      else if (normalizedPath.includes('receiptnumber')) key = 'receiptNumber'
      else if (normalizedPath.includes('description')) key = 'description'
      else if (normalizedPath.endsWith('items')) key = 'items'
      if (key) {
        validationErrors[key] = message
        applied = true
      }
    })
    return applied
  }

  async function saveReceipt(): Promise<void> {
    if (!validateForm()) {
      formError.value = ''
      return
    }

    operationRequest?.abort()
    const controller = new AbortController()
    operationRequest = controller
    saving.value = true
    formError.value = ''
    try {
      const items = itemsForSubmission()
      const payload: CreateGoodsReceiptRequest = {
        enteredAtUtc: new Date(form.enteredAtLocal).toISOString(),
        description: form.description.trim() || null,
        delivererId: form.delivererId || null,
        delivererFullName: form.delivererId ? null : form.delivererText.trim(),
        pageNumber: form.pageNumber.trim(),
        concurrencyStamp: editingReceiptId.value ? editingConcurrencyStamp.value : null,
        items: items.map(item => ({
          productId: item.productId,
          productTitle: item.productId ? null : item.productText.trim(),
          productCode: item.productId ? null : item.productCode.trim(),
          unitOfMeasureId: item.productId ? null : item.unitOfMeasureId,
          warehouseId: item.warehouseId,
          invoiceStatus: canManageReceiptInvoice ? item.invoiceStatus : null,
          invoiceNumber: canManageReceiptInvoice && itemInvoiceHasNumber(item)
            ? item.invoiceNumber.trim()
            : null,
          supplierId: canManageReceiptInvoice ? (item.supplierId || null) : null,
          supplierTitle: canManageReceiptInvoice && !item.supplierId
            ? (item.supplierText.trim() || null)
            : null,
          receiptNumber: canManageReceiptInvoice ? (item.receiptNumber.trim() || null) : null,
          quantity: Number(item.quantity),
          requestNumber: item.requestNumber.trim() || null,
          description: item.description.trim() || null,
        })),
      }
      if (editingReceiptId.value)
        await updateGoodsReceipt(editingReceiptId.value, payload, controller.signal)
      else await createGoodsReceipt(payload, controller.signal)
      receiptInvoiceModalOpen.value = false
      modalOpen.value = false
      referenceDataLoaded.value = false
      people.value = []
      units.value = []
      toast.success(editingReceiptId.value ? 'سند ورود کالا با موفقیت ویرایش شد.' : 'ورود کالا با موفقیت ثبت شد.')
      table.value?.dt.ajax.reload(undefined, true)
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      if (await redirectWhenUnauthorized(error)) return
      if (error instanceof GoodsReceiptsApiError && applyApiFieldErrors(error)) {
        formError.value = ''
      } else {
        formError.value = error instanceof Error ? error.message : 'ثبت ورود کالا انجام نشد.'
      }
    } finally {
      if (operationRequest === controller) saving.value = false
    }
  }

  async function removeReceipt(): Promise<void> {
    const receiptId = editingReceiptId.value
    if (!confirmDialog.value || !receiptId || !canInitialEntry || saving.value) return
    saving.value = true
    try {
      const confirmed = await confirmDialog.value.ask({
        title: 'حذف سند ورود کالا',
        text: 'با حذف این ورودی، سند ورود و اطلاعات تمام کالاهای ثبت‌شده در این ورودی حذف می‌شوند. آیا از حذف مطمئن هستید؟',
        icon: 'warning',
        confirmButtonText: 'حذف',
        cancelButtonText: 'انصراف',
        confirmButtonColor: '#dc3545',
      })
      if (!confirmed) return

      await deleteGoodsReceipt(receiptId)
      modalOpen.value = false
      editingReceiptId.value = null
      toast.success('سند ورود کالا حذف شد.')
      table.value?.dt.ajax.reload(undefined, false)
    } catch (error) {
      if (await redirectWhenUnauthorized(error)) return
      toast.error(error instanceof Error ? error.message : 'حذف سند انجام نشد.')
    } finally {
      saving.value = false
    }
  }

  async function redirectWhenUnauthorized(error: unknown): Promise<boolean> {
    const status = error instanceof GoodsReceiptsApiError || error instanceof MasterDataApiError
      ? error.status
      : 0
    if (status !== 401) return false
    await router.replace({ name: 'login', query: { redirect: '/dashboard/inventory/goods-receipts' } })
    return true
  }

  function formatDate(value: string): string {
    return new Intl.DateTimeFormat('fa-IR', {
      year: 'numeric', month: '2-digit', day: '2-digit',
    }).format(new Date(value))
  }

  function formatQuantity(value: number): string {
    return new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 3 }).format(value)
  }

  watch([modalOpen, receiptInvoiceModalOpen, importModalOpen], ([entryOpen, invoiceOpen, importOpen]) => document.body.classList.toggle('modal-open', entryOpen || invoiceOpen || importOpen))
  watch([modalOpen, goodsEntryMode], ([entryOpen, mode]) => {
    if (entryOpen && mode === 'list') scheduleListDescriptionResize()
  })
  watch(validationErrors, () => {
    if (modalOpen.value && goodsEntryMode.value === 'list') scheduleListDescriptionResize()
  }, { deep: true })
  watch(appliedFilters, filters => {
    if (!filters.productTitle) {
      productFilterId.value = null
      productFilterText.value = ''
    }
    if (!filters.supplierId) supplierFilterText.value = ''
    if (!filters.delivererId) delivererFilterText.value = ''
  }, { deep: true })
  watch(tableMode, () => {
    closeColumnSettings()
    disconnectCompactHorizontalScrollbar()
    scheduleCompactHorizontalScrollbar()
  })
  watch(() => form.invoiceStatus, status => {
    if (status === 'DoesNotHave') form.invoiceNumber = ''
  })

  onMounted(() => {
    void loadReferenceData()
    document.addEventListener('click', closeColumnSettings)
  })


  onBeforeUnmount(() => {
    exportRequest?.abort()
    itemHistoryRequest?.abort()
    importRequest?.abort()
    listRequest?.abort()
    operationRequest?.abort()
    referenceRequest?.abort()
    if (listDescriptionResizeFrame !== null) window.cancelAnimationFrame(listDescriptionResizeFrame)
    disconnectCompactHorizontalScrollbar()
    document.removeEventListener('click', closeColumnSettings)
    document.body.classList.remove('modal-open')
  })
</script>

<template>
  <ConfirmDialog ref="confirmDialog" />
  <EntityHistoryModal :open="itemHistoryOpen"
                      :title="itemHistoryTitle"
                      :loading="itemHistoryLoading"
                      :error="itemHistoryError"
                      :entries="itemHistoryEntries"
                      @close="closeItemHistory" />

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
        <div class="goods-receipts-toolbar">
          <label class="goods-receipts-view-mode mb-0">
            <span>نوع نمایش</span>
            <select v-model="tableMode" class="form-control form-control-sm" aria-label="نوع نمایش جدول">
              <option value="items">بر اساس کالا</option>
              <option value="receipts">بر اساس ورودی</option>
            </select>
          </label>
          <input v-if="canImportGoodsReceipts"
                 ref="importInput"
                 type="file"
                 accept=".xlsx,.xlsm"
                 hidden
                 @change="selectImportFile" />
          <div class="btn-group goods-receipts-column-settings goods-receipts-action-group"
               role="group"
               aria-label="ابزارهای لیست"
               @click.stop>
            <button v-if="canImportGoodsReceipts"
                    class="btn btn-outline-primary btn-sm goods-receipts-toolbar-icon goods-receipts-import-button"
                    type="button"
                    title="ایمپورت ورودی کالا"
                    aria-label="ایمپورت ورودی کالا"
                    :disabled="importing"
                    @click="importInput?.click()">
              <span v-if="importing" class="spinner-border spinner-border-sm" aria-hidden="true"></span>
              <i v-else class="las la-file-import" aria-hidden="true"></i>
            </button>
            <button v-if="canExportGoodsReceipts"
                    class="btn btn-outline-success btn-sm goods-receipts-toolbar-icon goods-receipts-desktop-export"
                    type="button"
                    title="اکسپورت ورودی کالا"
                    aria-label="اکسپورت ورودی کالا"
                    :disabled="exporting"
                    @click="exportItems">
              <span v-if="exporting" class="spinner-border spinner-border-sm" aria-hidden="true"></span>
              <i v-else class="las la-file-excel" aria-hidden="true"></i>
            </button>
            <button class="btn btn-outline-primary btn-sm goods-receipts-toolbar-icon goods-receipts-column-button"
                    type="button"
                    title="تنظیم ستون‌ها"
                    aria-label="تنظیم ستون‌ها"
                    :aria-expanded="columnSettingsOpen"
                    @click="toggleColumnSettings">
              <i class="las la-columns" aria-hidden="true"></i>
            </button>
            <div v-if="columnSettingsOpen" class="goods-receipts-column-menu">
              <div class="goods-receipts-column-menu__title">ستون‌های قابل نمایش</div>
              <label v-for="column in activeColumnSettings" :key="column.key" class="goods-receipts-column-option">
                <input type="checkbox"
                       :checked="columnIsVisible(column.key)"
                       @change="toggleColumnVisibility(column)" />
                <span>{{ column.label }}</span>
              </label>
            </div>
          </div>
          <button v-if="canInitialEntry"
                  class="btn btn-primary btn-sm mobile-icon-button goods-receipts-desktop-add"
                  type="button"
                  title="ثبت ورود کالا"
                  :disabled="referenceLoading"
                  @click="openCreateModal">
            <i class="las la-plus ml-1"></i>
            <span class="mobile-button-label">ثبت ورود کالا</span>
          </button>
        </div>
      </div>

      <div class="card-body">
        <div v-if="importSummary" class="alert" :class="importSummary.invalidRows.length ? 'alert-danger' : 'alert-success'" role="status">
          <template v-if="importSummary.invalidRows.length">
            <p>هیچ ورودی یا تحویل‌دهنده‌ای ثبت نشد. خطاها را اصلاح و فایل را دوباره ارسال کنید.</p>
            <ul class="mb-0" style="max-height: 20rem; overflow-y: auto">
              <li v-for="row in importSummary.invalidRows" :key="row.rowNumber">ردیف {{ row.rowNumber }}: {{ row.message }}</li>
            </ul>
          </template>
          <template v-else>
            {{ importSummary.importedReceiptCount }} سند ورود،{{ importSummary.importedItemCount }} قلم کالا و
            {{ importSummary.createdDelivererCount }} تحویل‌دهندهٔ جدید ثبت شد.
          </template>
        </div>
        <DataTableFilters v-model="appliedFilters"
                          :definitions="filterDefinitions"
                          :search="quickSearch"
                          :loading="filtersLoading"
                          search-placeholder="جستجو در عنوان و کد کالا، شماره صفحه، فاکتور و رسید..."
                          @update:search="handleFilterSearch"
                          @apply="handleFilterApply"
                          @clear="handleFilterClear"
                          @remove="handleFilterRemove">
          <template #filter-productTitle="{ setValue, error }">
            <ProductSelect :model-value="productFilterId"
                           :text="productFilterText"
                           label="کالا"
                           placeholder="جستجو و انتخاب کالا"
                           size="sm"
                           :error="error"
                           @update:model-value="productFilterId = $event"
                           @update:text="productFilterText = $event"
                           @select="selectProductFilter($event, setValue)"
                           @clear="clearProductFilter(setValue)" />
          </template>
          <template #filter-supplierId="{ value, setValue, error }">
            <SupplierSelect :model-value="filterId(value)"
                            :text="supplierFilterText"
                            label="طرف حساب"
                            placeholder="جستجو و انتخاب طرف حساب"
                            size="sm"
                            :error="error"
                            @update:model-value="setValue($event)"
                            @update:text="supplierFilterText = $event"
                            @select="selectSupplierFilter($event, setValue)" />
          </template>
          <template #filter-delivererId="{ value, setValue, error }">
            <PersonSelect :model-value="filterId(value)"
                          :text="delivererFilterText"
                          :options="people"
                          label="تحویل‌دهنده"
                          placeholder="جستجو و انتخاب تحویل‌دهنده"
                          size="sm"
                          :error="error"
                          @update:model-value="setValue($event)"
                          @update:text="delivererFilterText = $event"
                          @select="selectDelivererFilter($event, setValue)" />
          </template>
        </DataTableFilters>
        <div ref="tableContainer"
             :class="['users-table-container', 'goods-receipts-table-container', 'inventory-table-container', `goods-receipts-table-container--${tableMode}`]"
             @wheel="handleTableWheel">
          <DataTable :key="tableMode"
                     ref="table"
                     :options="tableMode === 'items' ? itemTableOptions : receiptTableOptions"
                     class="display responsive nowrap table-striped table table-sm users-table inventory-data-table">
            <template #column-enteredAtUtc="{ cellData }">
              <strong class="text-black">{{ formatDate(cellData) }}</strong>
            </template>
            <template #column-pageNumber="{ cellData }">
              <span dir="ltr">{{ cellData }}</span>
            </template>
            <template #column-warehouseTitle="{ cellData }">
              {{ cellData }}
            </template>
            <template #column-itemCount="{ cellData }">
              <span class="badge light badge-info">{{ (cellData as GoodsReceipt['items']).length }} کالا</span>
            </template>
            <template #column-productTitle="{ cellData }">
              <strong class="text-black">{{ cellData }}</strong>
            </template>
            <template #column-productCode="{ cellData }">
              <span dir="ltr">{{ cellData }}</span>
            </template>
            <template #column-quantity="{ cellData }">
              <span class="goods-receipts-quantity-cell">{{ formatQuantity(cellData) }}</span>
            </template>
            <template #column-unitOfMeasureTitle="{ cellData }">
              {{ cellData }}
            </template>
            <template #column-requestNumber="{ cellData }">
              <span dir="ltr">{{ cellData || '—' }}</span>
            </template>
            <template #column-itemDescription="{ cellData }">
              <span class="goods-receipts-description-cell" :title="cellData || ''">{{ cellData || '—' }}</span>
            </template>
            <template #column-invoiceNumber="{ cellData }">
              <span dir="ltr">{{ cellData || '—' }}</span>
            </template>
            <template #column-counterparty="{ cellData }">
              {{ cellData || '—' }}
            </template>
            <template #column-receiptNumber="{ cellData }">
              <span dir="ltr">{{ cellData || '—' }}</span>
            </template>
            <template #column-description="{ cellData }">
              <span class="goods-receipts-description-cell" :title="cellData || ''">{{ cellData || '—' }}</span>
            </template>
            <template #column-receiptDescription="{ cellData }">
              <span class="goods-receipts-description-cell" :title="cellData || ''">{{ cellData || '—' }}</span>
            </template>
            <template #column-invoiceStatus="{ cellData }">
              <span class="badge light" :class="cellData === 'HasInvoice' ? 'badge-success' : 'badge-secondary'">
                {{ cellData === 'HasInvoice' ? 'دارد' : cellData === 'DoesNotHave' ? 'ندارد' : '—' }}
              </span>
            </template>
            <template v-if="canInitialEntry || canManageReceiptInvoice || canReadItemHistory" #column-actions="{ rowData }">
              <div class="d-flex">
                <button v-if="canInitialEntry || canManageReceiptInvoice"
                        class="btn   users-table__role-action"
                        type="button" title="ویرایش سند" aria-label="ویرایش سند"
                        :disabled="editLoading" @click="openEditModal(rowData)">
                  <i class="las la-edit" aria-hidden="true"></i>
                </button>
                <button v-if="canReadItemHistory && tableMode === 'items'"
                        class="btn  users-table__role-action"
                        type="button" title="مشاهده تاریخچه تغییرات"
                        aria-label="مشاهده تاریخچه تغییرات کالا"
                        :disabled="itemHistoryLoading"
                        @click="openItemHistory(rowData)">
                  <i class="las la-history" aria-hidden="true"></i>
                </button>
              </div>
            </template>
            <thead>
              <tr v-if="tableMode === 'items'">
                <th>ردیف</th>
                <th>شماره صفحه</th>
                <th>تاریخ</th>
                <th>عنوان کالا</th>
                <th>کد کالا</th>
                <th>تعداد</th>
                <th>واحد</th>
                <th>توضیحات کالا</th>
                <th>تحویل‌دهنده</th>
                <th>فاکتور</th>
                <th>شماره فاکتور</th>
                <th>طرف حساب</th>
                <th>شماره رسید</th>
                <th>انبار</th>
                <th v-if="canInitialEntry || canManageReceiptInvoice || canReadItemHistory">عملیات</th>
              </tr>
              <tr v-else>

                <th>ردیف</th>
                <th>تاریخ ورود</th>
                <th>شماره صفحه</th>
                <th>تحویل‌دهنده</th>
                <th>تعداد کالا</th>
                <th>توضیحات ورودی</th>
                <th v-if="canInitialEntry || canManageReceiptInvoice">
                  عملیات
                </th>

              </tr>
            </thead>
          </DataTable>
        </div>
      </div>
    </div>

    <div v-if="canInitialEntry || canExportGoodsReceipts || canImportGoodsReceipts"
         class="goods-receipts-mobile-actions"
         role="toolbar"
         aria-label="عملیات ورود کالا">
      <button v-if="canInitialEntry"
              class="btn btn-primary"
              type="button"
              :disabled="referenceLoading"
              @click="openCreateModal">
        <i class="las la-plus" aria-hidden="true"></i>
        <span>افزودن ورودی</span>
      </button>
      <button v-if="canExportGoodsReceipts"
              class="btn btn-outline-success"
              type="button"
              :disabled="exporting"
              @click="exportItems">
        <span v-if="exporting" class="spinner-border spinner-border-sm" aria-hidden="true"></span>
        <i v-else class="las la-file-excel" aria-hidden="true"></i>
        <span>خروجی Excel</span>
      </button>
      <button v-if="canImportGoodsReceipts"
              class="btn btn-outline-primary"
              type="button"
              :disabled="importing"
              @click="importInput?.click()">
        <span v-if="importing" class="spinner-border spinner-border-sm" aria-hidden="true"></span>
        <i v-else class="las la-file-import" aria-hidden="true"></i>
        <span>ایمپورت</span>
      </button>
    </div>
  </div>

  <Teleport to="body">
    <template v-if="importModalOpen">
      <div class="modal show d-block" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="receipt-import-title" @click.self="closeImportModal" @keydown.esc="closeImportModal">
        <div class="modal-dialog modal-dialog-centered" role="document">
          <form class="modal-content" @submit.prevent="startImport">
            <div class="modal-header">
              <h5 id="receipt-import-title" class="modal-title">ایمپورت ورود کالا</h5>
              <button type="button" class="close" aria-label="بستن" :disabled="importing" @click="closeImportModal"><span aria-hidden="true">&times;</span></button>
            </div>
            <div class="modal-body">
              <p>فایل انتخاب‌شده: <bdi>{{ importFile?.name }}</bdi></p>
              <p>ردیف‌های دارای انبار و شمارهٔ صفحهٔ یکسان، یک سند می‌شوند. تاریخ، تحویل‌دهنده و اطلاعات رسید و فاکتور آن‌ها باید یکسان باشد.</p>
              <p class="mb-0">فقط تحویل‌دهندهٔ جدید ساخته می‌شود؛ سایر مفروضات باید از قبل ثبت شده باشند. در صورت وجود خطا، کل فایل ثبت نمی‌شود.</p>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-light" :disabled="importing" @click="closeImportModal">انصراف</button>
              <button type="submit" class="btn btn-primary" :disabled="importing">
                <span v-if="importing" class="spinner-border spinner-border-sm ml-1" aria-hidden="true"></span>
                {{ importing ? 'در حال بررسی و ثبت...' : 'شروع ایمپورت' }}
              </button>
            </div>
          </form>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>

  <Teleport to="body">
    <template v-if="modalOpen">
      <div class="modal show d-block goods-receipt-modal" tabindex="-1" role="dialog" aria-modal="true" @click.self="closeModal">
        <div class="modal-dialog modal-xl modal-dialog-centered modal-dialog-scrollable" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title"><i class="las la-dolly-flatbed ml-1"></i> {{ editingReceiptId ? 'ویرایش ورود کالا' : 'ثبت ورود کالا' }}</h5>
              <button type="button" class="close" aria-label="بستن" :disabled="saving" @click="closeModal"><span>&times;</span></button>
            </div>

            <form novalidate @submit.prevent="saveReceipt">
              <div class="modal-body">
                <div v-if="formError" class="alert alert-danger py-2">{{ formError }}</div>
                <div class="row">
                  <div class="col-md-3 form-group">
                    <label>تاریخ و ساعت ورود <span class="text-danger">*</span></label>

                    <DatePicker v-model="form.enteredAtLocal"
                                @input="clearFieldError('enteredAtLocal')"
                                type="date"
                                format="YYYY-MM-DDTHH:mm:ss"
                                display-format="jYYYY/jMM/jDD HH:mm"
                                placeholder="تاریخ و ساعت را انتخاب کنید"
                                :editable="true"
                                color="#2563eb"
                                :class="{ 'is-invalid': fieldError('enteredAtLocal') }"
                                :clearable="true"
                                :disabled="saving" />

                    <!--<input v-model="form.enteredAtLocal" class="form-control"
                           :class="{ 'is-invalid': fieldError('enteredAtLocal') }"
                           type="datetime-local" :disabled="saving"
                           @input="clearFieldError('enteredAtLocal')" />-->

                    <small v-if="fieldError('enteredAtLocal')" class="invalid-feedback d-block">{{ fieldError('enteredAtLocal') }}</small>
                  </div>
                  <div class="col-md-3 form-group">
                    <label>شماره صفحه <span class="text-danger">*</span></label>
                    <input v-model="form.pageNumber" class="form-control" :class="{ 'is-invalid': fieldError('pageNumber') }" type="text" maxlength="100" :disabled="saving" @input="clearFieldError('pageNumber')" />
                    <small v-if="fieldError('pageNumber')" class="invalid-feedback d-block">{{ fieldError('pageNumber') }}</small>
                  </div>
                  <div class="col-md-3 form-group">
                    <PersonSelect :model-value="form.delivererId || null"
                                  :text="form.delivererText"
                                  :options="people"
                                  label="تحویل‌دهنده"
                                  placeholder="تایپ یا انتخاب تحویل‌دهنده"
                                  :disabled="saving"
                                  :allow-create="canManageMasterData"
                                  :error="fieldError('deliverer')"
                                  required
                                  @update:model-value="form.delivererId = $event ?? ''"
                                  @update:text="form.delivererText = $event; clearFieldError('deliverer')"
                                  @select="clearFieldError('deliverer')"
                                  @clear="clearFieldError('deliverer')" />
                    <small v-if="isNewDeliverer()"
                           class="receipt-new-master-data"
                           :class="{ 'receipt-new-master-data--denied': !canManageMasterData }">
                      <i :class="canManageMasterData ? 'las la-plus-circle' : 'las la-exclamation-circle'" aria-hidden="true"></i>
                      {{
 canManageMasterData
                        ? 'تحویل‌دهنده جدید هنگام ثبت اضافه می‌شود.'
                        : 'مجوز افزودن تحویل‌دهنده جدید را ندارید؛ از فهرست انتخاب کنید.'
                      }}
                    </small>
                  </div>
                  <div class="col-md-6 form-group">
                    <label>توضیحات</label>
                    <textarea v-model="form.description" class="form-control" :class="{ 'is-invalid': fieldError('description') }" rows="2" maxlength="1000" :disabled="saving" @input="clearFieldError('description')"></textarea>
                    <small v-if="fieldError('description')" class="invalid-feedback d-block">{{ fieldError('description') }}</small>
                  </div>
                </div>

                <div class="receipt-items-header mb-2">
                  <div>
                    <h6 class="mb-0">کالاها و مقدار</h6>
                  </div>
                  <div class="receipt-entry-actions">
                    <div class="btn-group btn-group-sm receipt-entry-mode" role="group" aria-label="روش ثبت کالا">
                      <button class="btn"
                              :class="goodsEntryMode === 'cards' ? 'btn-primary' : 'btn-outline-primary'"
                              type="button"
                              :disabled="saving"
                              @click="setGoodsEntryMode('cards')">
                        <i class="las la-stream ml-1" aria-hidden="true"></i>

                      </button>
                      <button class="btn"
                              :class="goodsEntryMode === 'list' ? 'btn-primary' : 'btn-outline-primary'"
                              type="button"
                              :disabled="saving"
                              @click="setGoodsEntryMode('list')">
                        <i class="las la-table ml-1" aria-hidden="true"></i>

                      </button>
                    </div>
                    <button class="btn btn-outline-primary btn-sm" type="button" :disabled="saving" @click="addItem"><i class="las la-plus ml-1"></i> افزودن ردیف</button>
                  </div>
                </div>
                <small v-if="fieldError('items')" class="invalid-feedback d-block mb-2">{{ fieldError('items') }}</small>
                <div v-if="goodsEntryMode === 'list'" class="receipt-list-entry">
                  <div class="receipt-list-table-scroll">
                    <table class="receipt-list-table table-striped" role="grid" aria-label="ثبت لیستی کالاها">
                      <thead>
                        <tr>
                          <th class="receipt-list-row-number">#</th>
                          <th class="receipt-list-product-cell">عنوان کالا <span class="text-danger">*</span></th>
                          <th class="receipt-list-code-cell">کد کالا <span class="text-danger">*</span></th>
                          <th class="receipt-list-unit-cell">واحد <span class="text-danger">*</span></th>
                          <th class="receipt-list-quantity-cell">مقدار <span class="text-danger">*</span></th>
                          <th class="receipt-list-warehouse-cell">انبار <span class="text-danger">*</span></th>
                          <th class="receipt-list-invoice-status-cell">فاکتور</th>
                          <th class="receipt-list-invoice-number-cell">شماره فاکتور</th>
                          <th class="receipt-list-supplier-cell">طرف حساب</th>
                          <th class="receipt-list-receipt-number-cell">شماره رسید</th>
                          <th class="receipt-list-request-cell">شماره درخواست</th>
                          <th class="receipt-list-description-cell">توضیحات کالا</th>
                          <th class="receipt-list-action-column" aria-label="عملیات"></th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="(item, index) in form.items"
                            :key="item.key"
                            :class="{
                            'receipt-list-row--empty': !itemHasContent(item),
                            'receipt-list-row--new-product': isNewProduct(item),
                          }">
                          <td class="receipt-list-row-number">{{ index + 1 }}</td>
                          <td class="receipt-list-product-cell">
                            <ProductSelect :model-value="item.productId"
                                           :text="item.productText"
                                           size="sm"
                                           search-by="title"
                                           placeholder=""
                                           aria-label="عنوان کالا"
                                           :not-found-text="productNotFoundText"
                                           :error="itemFieldError(item, 'product')"
                                           error-class="receipt-list-cell-error"
                                           :disabled="saving"
                                           @update:model-value="item.productId = $event"
                                           @update:text="handleProductTextInput(item, 'title', $event)"
                                           @select="selectProduct(item, $event)"
                                           @clear="clearProductSelection(item, 'title')"
                                           @enter="advanceFromProductTitle(item)" />
                            <small v-if="isNewProduct(item) && !canCreateProducts && !itemFieldError(item, 'product')"
                                   class="receipt-list-cell-note receipt-new-master-data--denied">
                              بدون مجوز افزودن
                            </small>
                          </td>
                          <td class="receipt-list-code-cell">
                            <ProductSelect :model-value="item.productId"
                                           :text="item.productCode"
                                           size="sm"
                                           search-by="code"
                                           placeholder=""
                                           aria-label="کد کالا"
                                           :not-found-text="productNotFoundText"
                                           :error="itemFieldError(item, 'code')"
                                           error-class="receipt-list-cell-error"
                                           :disabled="saving"
                                           :data-receipt-item-key="item.key"
                                           data-receipt-field="code"
                                           @update:model-value="item.productId = $event"
                                           @update:text="handleProductTextInput(item, 'code', $event)"
                                           @select="selectProduct(item, $event)"
                                           @clear="clearProductSelection(item, 'code')"
                                           @enter="advanceFromProductCode(item)" />
                          </td>
                          <td class="receipt-list-unit-cell">
                            <div class="receipt-product-combobox">
                              <input v-model="item.unitOfMeasureTitle"
                                     class="form-control form-control-sm"
                                     :class="{ 'is-invalid': itemFieldError(item, 'unit') }"
                                     type="text"
                                     role="combobox"
                                     aria-autocomplete="list"
                                     :aria-expanded="unitMenuIsOpen(item)"
                                     aria-label="واحد اندازه‌گیری"
                                     autocomplete="off"
                                     maxlength="200"
                                     :data-receipt-item-key="item.key"
                                     data-receipt-field="unit"
                                     :readonly="Boolean(item.productId)"
                                     :disabled="saving"
                                     @focus="openUnitMenu(item)"
                                     @input="handleUnitInput(item)"
                                     @blur="handleUnitBlur(item)"
                                     @keydown.enter.prevent="advanceFromUnit(item)"
                                     @keydown.esc="closeUnitMenu(item)" />
                              <i class="las la-angle-down" aria-hidden="true"></i>
                              <div v-if="unitMenuIsOpen(item)" class="receipt-product-options" role="listbox">
                                <button v-for="unit in filteredUnitOptions(item)"
                                        :key="unit.id"
                                        class="receipt-product-option"
                                        type="button"
                                        role="option"
                                        @mousedown.prevent="chooseUnit(item, unit)">
                                  <span class="receipt-product-option__title">{{ unit.title }}</span>
                                </button>
                                <div v-if="!filteredUnitOptions(item).length" class="receipt-product-empty">فقط واحدهای موجود قابل انتخاب‌اند.</div>
                              </div>
                            </div>
                            <small v-if="itemFieldError(item, 'unit')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'unit') }}
                            </small>
                            <small v-else-if="!item.productId && item.unitOfMeasureTitle.trim() && !item.unitOfMeasureId" class="receipt-list-cell-note receipt-new-master-data--denied">واحد نامعتبر</small>
                          </td>
                          <td class="receipt-list-quantity-cell">
                            <input v-model.number="item.quantity" class="form-control form-control-sm" :class="{ 'is-invalid': itemFieldError(item, 'quantity') }" type="number" step="any" inputmode="decimal" aria-label="مقدار" :data-receipt-item-key="item.key" data-receipt-field="quantity" :disabled="saving" @input="handleQuantityInput(item)" />
                            <small v-if="itemFieldError(item, 'quantity')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'quantity') }}
                            </small>
                          </td>
                          <td class="receipt-list-warehouse-cell">
                            <select v-model="item.warehouseId"
                                    class="form-control form-control-sm"
                                    :class="{ 'is-invalid': itemFieldError(item, 'warehouse') }"
                                    aria-label="انبار"
                                    :disabled="saving || !canInitialEntry"
                                    @change="handleListCellInput(item, 'warehouse')">
                              <option value="" aria-label="بدون انتخاب"></option>
                              <option v-for="warehouse in warehouses" :key="warehouse.id" :value="warehouse.id">{{ warehouse.title }}</option>
                            </select>
                            <small v-if="itemFieldError(item, 'warehouse')" class="invalid-feedback d-block receipt-list-cell-error">{{ itemFieldError(item, 'warehouse') }}</small>
                          </td>
                          <td class="receipt-list-invoice-status-cell">
                            <select v-model="item.invoiceStatus"
                                    class="form-control form-control-sm"
                                    aria-label="وضعیت فاکتور"
                                    :disabled="saving || !canManageReceiptInvoice"
                                    @change="item.invoiceNumber = itemInvoiceHasNumber(item) ? item.invoiceNumber : ''; handleListCellInput(item, 'invoiceStatus')">
                              <option :value="null" aria-label="بدون انتخاب"></option>
                              <option value="DoesNotHave">ندارد</option>
                              <option value="HasInvoice">دارد</option>
                            </select>
                          </td>
                          <td class="receipt-list-invoice-number-cell">
                            <input v-model="item.invoiceNumber"
                                   class="form-control form-control-sm"
                                   :class="{ 'is-invalid': itemFieldError(item, 'invoiceNumber') }"
                                   type="text"
                                   maxlength="100"
                                   aria-label="شماره فاکتور"
                                   :disabled="saving || !canManageReceiptInvoice || !itemInvoiceHasNumber(item)"
                                   @input="handleListCellInput(item, 'invoiceNumber')" />
                            <small v-if="itemFieldError(item, 'invoiceNumber')" class="invalid-feedback d-block receipt-list-cell-error">{{ itemFieldError(item, 'invoiceNumber') }}</small>
                          </td>
                          <td class="receipt-list-supplier-cell">
                            <SupplierSelect :model-value="item.supplierId || null"
                                            :text="item.supplierText"
                                            size="sm"
                                            placeholder="‌"
                                            :error="itemFieldError(item, 'supplier')"
                                            error-class="receipt-list-cell-error"
                                            :disabled="saving || !canManageReceiptInvoice"
                                            allow-create
                                            @update:model-value="item.supplierId = $event ?? ''"
                                            @update:text="item.supplierText = $event; handleListCellInput(item, 'supplier')"
                                            @clear="clearItemFieldError(item, 'supplier')" />
                            <small v-if="isNewItemSupplier(item) && !canManageMasterData"
                                   class="receipt-list-cell-note receipt-new-master-data--denied">
                              بدون مجوز افزودن
                            </small>
                          </td>
                          <td class="receipt-list-receipt-number-cell">
                            <input v-model="item.receiptNumber"
                                   class="form-control form-control-sm"
                                   type="text"
                                   maxlength="100"
                                   aria-label="شماره رسید"
                                   :disabled="saving || !canManageReceiptInvoice"
                                   @input="handleListCellInput(item, 'receiptNumber')" />
                          </td>
                          <td class="receipt-list-request-cell">
                            <input v-model="item.requestNumber" class="form-control form-control-sm" :class="{ 'is-invalid': itemFieldError(item, 'requestNumber') }" type="text" maxlength="100" :disabled="saving" @input="handleListCellInput(item, 'requestNumber')" />
                            <small v-if="itemFieldError(item, 'requestNumber')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'requestNumber') }}
                            </small>
                          </td>
                          <td class="receipt-list-description-cell">
                            <textarea v-model="item.description"
                                      class="form-control form-control-sm receipt-list-description-input"
                                      :class="{ 'is-invalid': itemFieldError(item, 'description') }"
                                      rows="1"
                                      maxlength="1000"
                                      :data-list-description="item.key"
                                      :disabled="saving"
                                      @input="handleListDescriptionInput(item)"></textarea>
                            <small v-if="itemFieldError(item, 'description')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'description') }}
                            </small>
                          </td>
                          <td class="receipt-list-action-column">
                            <button class="master-data-row-action receipt-remove-item" type="button" title="پاک‌کردن ردیف" aria-label="پاک‌کردن ردیف" :disabled="saving" @click="removeItem(index)"><i class="las la-trash"></i></button>
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </div>
                <template v-if="goodsEntryMode === 'cards'">
                  <div v-for="(item, index) in form.items"
                       :key="item.key"
                       class="receipt-item-row"
                       :class="{ 'receipt-item-row--new-product': isNewProduct(item) }">
                    <div class="receipt-item-primary-row">
                      <div class="form-group mb-0 receipt-product-field">
                        <label>عنوان کالا <span class="text-danger">*</span></label>
                        <ProductSelect :model-value="item.productId"
                                       :text="item.productText"
                                       search-by="title"
                                       placeholder="تایپ یا انتخاب کالا"
                                       aria-label="عنوان کالا"
                                       :not-found-text="productNotFoundText"
                                       :error="itemFieldError(item, 'product')"
                                       :disabled="saving"
                                       @update:model-value="item.productId = $event"
                                       @update:text="handleProductTextInput(item, 'title', $event)"
                                       @select="selectProduct(item, $event)"
                                       @clear="clearProductSelection(item, 'title')"
                                       @enter="advanceFromProductTitle(item)" />
                      </div>
                      <div class="form-group mb-0 receipt-code-field">
                        <label>کد کالا <span v-if="!item.productId" class="text-danger">*</span></label>
                        <ProductSelect :model-value="item.productId"
                                       :text="item.productCode"
                                       search-by="code"
                                       placeholder="انتخاب کد"
                                       aria-label="کد کالا"
                                       :not-found-text="productNotFoundText"
                                       :error="itemFieldError(item, 'code')"
                                       :disabled="saving"
                                       :data-receipt-item-key="item.key"
                                       data-receipt-field="code"
                                       @update:model-value="item.productId = $event"
                                       @update:text="handleProductTextInput(item, 'code', $event)"
                                       @select="selectProduct(item, $event)"
                                       @clear="clearProductSelection(item, 'code')"
                                       @enter="advanceFromProductCode(item)" />
                      </div>
                      <div class="form-group mb-0 receipt-unit-field">
                        <label>واحد <span v-if="!item.productId" class="text-danger">*</span></label>
                        <div class="receipt-product-combobox">
                          <input v-model="item.unitOfMeasureTitle"
                                 class="form-control"
                                 :class="{ 'is-invalid': itemFieldError(item, 'unit') }"
                                 type="text"
                                 role="combobox"
                                 aria-autocomplete="list"
                                 :aria-expanded="unitMenuIsOpen(item)"
                                 autocomplete="off"
                                 maxlength="200"
                                 placeholder="انتخاب واحد"
                                 :data-receipt-item-key="item.key"
                                 data-receipt-field="unit"
                                 :readonly="Boolean(item.productId)"
                                 :disabled="saving"
                                 @focus="openUnitMenu(item)"
                                 @input="handleUnitInput(item)"
                                 @blur="handleUnitBlur(item)"
                                 @keydown.enter.prevent="advanceFromUnit(item)"
                                 @keydown.esc="closeUnitMenu(item)" />
                          <i class="las la-angle-down" aria-hidden="true"></i>
                          <div v-if="unitMenuIsOpen(item)" class="receipt-product-options" role="listbox">
                            <button v-for="unit in filteredUnitOptions(item)"
                                    :key="unit.id"
                                    class="receipt-product-option"
                                    type="button"
                                    role="option"
                                    @mousedown.prevent="chooseUnit(item, unit)">
                              <span class="receipt-product-option__title">{{ unit.title }}</span>
                            </button>
                            <div v-if="!filteredUnitOptions(item).length" class="receipt-product-empty">
                              واحدی پیدا نشد؛ یکی از واحدهای موجود را انتخاب کنید.
                            </div>
                          </div>
                        </div>
                        <small v-if="itemFieldError(item, 'unit')" class="invalid-feedback d-block">
                          {{ itemFieldError(item, 'unit') }}
                        </small>
                        <small v-else-if="!item.productId && item.unitOfMeasureTitle.trim() && !item.unitOfMeasureId" class="receipt-new-master-data receipt-new-master-data--denied">
                          واحد جدید قابل افزودن نیست؛ از فهرست واحدهای موجود انتخاب کنید.
                        </small>
                      </div>
                      <div class="form-group mb-0 receipt-quantity-field">
                        <label>مقدار <span class="text-danger">*</span></label>
                        <input v-model.number="item.quantity" class="form-control" :class="{ 'is-invalid': itemFieldError(item, 'quantity') }" type="number" step="any" inputmode="decimal" :data-receipt-item-key="item.key" data-receipt-field="quantity" :disabled="saving" @input="handleQuantityInput(item)" />
                        <small v-if="itemFieldError(item, 'quantity')" class="invalid-feedback d-block">
                          {{ itemFieldError(item, 'quantity') }}
                        </small>
                      </div>
                      <div class="form-group mb-0 receipt-contract-field">
                        <label>شماره درخواست</label>
                        <input v-model="item.requestNumber" class="form-control" :class="{ 'is-invalid': itemFieldError(item, 'requestNumber') }" type="text" maxlength="100" :disabled="saving" @input="clearItemFieldError(item, 'requestNumber')" />
                        <small v-if="itemFieldError(item, 'requestNumber')" class="invalid-feedback d-block">
                          {{ itemFieldError(item, 'requestNumber') }}
                        </small>
                      </div>
                    </div>

                    <div class="receipt-item-details-row">
                      <div class="form-group mb-0">
                        <label>انبار <span class="text-danger">*</span></label>
                        <select v-model="item.warehouseId" class="form-control" :class="{ 'is-invalid': itemFieldError(item, 'warehouse') }" :disabled="saving || !canInitialEntry" @change="clearItemFieldError(item, 'warehouse')">
                          <option value="" aria-label="بدون انتخاب"></option>
                          <option v-for="warehouse in warehouses" :key="warehouse.id" :value="warehouse.id">{{ warehouse.title }}</option>
                        </select>
                        <small v-if="itemFieldError(item, 'warehouse')" class="invalid-feedback d-block">{{ itemFieldError(item, 'warehouse') }}</small>
                      </div>
                      <div class="form-group mb-0">
                        <label>وضعیت فاکتور</label>
                        <select v-model="item.invoiceStatus" class="form-control" :disabled="saving || !canManageReceiptInvoice" @change="item.invoiceNumber = itemInvoiceHasNumber(item) ? item.invoiceNumber : ''">
                          <option :value="null" aria-label="بدون انتخاب"></option>
                          <option value="DoesNotHave">ندارد</option>
                          <option value="HasInvoice">دارد</option>
                        </select>
                      </div>
                      <div class="form-group mb-0">
                        <label>شماره فاکتور <span v-if="itemInvoiceHasNumber(item)" class="text-danger">*</span></label>
                        <input v-model="item.invoiceNumber" class="form-control" :class="{ 'is-invalid': itemFieldError(item, 'invoiceNumber') }" type="text" maxlength="100" :disabled="saving || !canManageReceiptInvoice || !itemInvoiceHasNumber(item)" @input="clearItemFieldError(item, 'invoiceNumber')" />
                        <small v-if="itemFieldError(item, 'invoiceNumber')" class="invalid-feedback d-block">{{ itemFieldError(item, 'invoiceNumber') }}</small>
                      </div>
                      <div class="form-group mb-0">
                        <label>طرف حساب</label>
                        <SupplierSelect :model-value="item.supplierId || null"
                                        :text="item.supplierText"
                                        placeholder="تایپ یا انتخاب تأمین‌کننده"
                                        :error="itemFieldError(item, 'supplier')"
                                        :disabled="saving || !canManageReceiptInvoice"
                                        allow-create
                                        @update:model-value="item.supplierId = $event ?? ''"
                                        @update:text="item.supplierText = $event"
                                        @clear="clearItemFieldError(item, 'supplier')" />
                      </div>
                      <div class="form-group mb-0">
                        <label>شماره رسید</label>
                        <input v-model="item.receiptNumber" class="form-control" type="text" maxlength="100" :disabled="saving || !canManageReceiptInvoice" />
                      </div>
                    </div>

                    <small v-if="isNewProduct(item) && !canCreateProducts"
                           class="receipt-new-master-data receipt-new-master-data--denied receipt-new-product-message">
                      <i class="las la-exclamation-circle" aria-hidden="true"></i>
                      مجوز افزودن کالای جدید را ندارید؛ از فهرست انتخاب کنید.
                    </small>

                    <div class="receipt-item-secondary-row">
                      <div class="form-group mb-0 receipt-item-description">
                        <label>توضیحات کالا</label>
                        <input v-model="item.description" class="form-control" :class="{ 'is-invalid': itemFieldError(item, 'description') }" type="text" maxlength="1000" :disabled="saving" @input="clearItemFieldError(item, 'description')" />
                        <small v-if="itemFieldError(item, 'description')" class="invalid-feedback d-block">
                          {{ itemFieldError(item, 'description') }}
                        </small>
                      </div>
                      <div class="receipt-item-actions">
                        <button class="master-data-row-action receipt-remove-item" type="button" title="حذف کالا" aria-label="حذف کالا" :disabled="saving || form.items.length === 1" @click="removeItem(index)"><i class="las la-trash"></i></button>
                      </div>
                    </div>
                  </div>
                </template>
              </div>

              <div class="modal-footer">
                <button v-if="editingReceiptId && canInitialEntry" class="btn btn-outline-danger px-4" type="button" :disabled="saving" @click="removeReceipt">
                  <i class="las la-trash ml-1" aria-hidden="true"></i>حذف ورودی
                </button>
                <button class="btn btn-light px-4" type="button" :disabled="saving" @click="closeModal">انصراف</button>
                <button class="btn btn-primary px-4" type="submit" :disabled="saving"><span v-if="saving" class="spinner-border spinner-border-sm ml-1"></span>{{ editingReceiptId ? 'ذخیره تغییرات' : 'ثبت ورود کالا' }}</button>
              </div>
            </form>
          </div>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>

  <Teleport to="body">
    <template v-if="receiptInvoiceModalOpen">
      <div class="modal show d-block" tabindex="-1" role="dialog" aria-modal="true" @click.self="closeReceiptInvoiceModal">
        <div class="modal-dialog modal-lg modal-dialog-centered" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title"><i class="las la-file-invoice ml-1" aria-hidden="true"></i> عملیات رسید و فاکتور</h5>
              <button type="button" class="close" aria-label="بستن" :disabled="saving" @click="closeReceiptInvoiceModal"><span>&times;</span></button>
            </div>

            <form novalidate @submit.prevent="saveReceiptInvoiceOperation">
              <div class="modal-body">
                <div v-if="formError" class="alert alert-danger py-2">{{ formError }}</div>
                <div class="row">
                  <div class="col-md-6 form-group">
                    <label>وضعیت فاکتور</label>
                    <select v-model="form.invoiceStatus" class="form-control" :class="{ 'is-invalid': fieldError('invoiceStatus') }" :disabled="saving" @change="clearFieldError('invoiceStatus'); clearFieldError('invoiceNumber')">
                      <option :value="null">نامشخص</option>
                      <option value="DoesNotHave">ندارد</option>
                      <option value="HasInvoice">دارد</option>
                    </select>
                    <small v-if="fieldError('invoiceStatus')" class="invalid-feedback d-block">{{ fieldError('invoiceStatus') }}</small>
                  </div>
                  <div class="col-md-6 form-group">
                    <label>شماره فاکتور <span v-if="invoiceHasNumber" class="text-danger">*</span></label>
                    <input v-model="form.invoiceNumber" class="form-control" :class="{ 'is-invalid': fieldError('invoiceNumber') }" type="text" maxlength="100" :disabled="saving || !invoiceHasNumber" @input="clearFieldError('invoiceNumber')" />
                    <small v-if="fieldError('invoiceNumber')" class="invalid-feedback d-block">{{ fieldError('invoiceNumber') }}</small>
                  </div>
                  <div class="col-md-6 form-group">
                    <label>طرف حساب</label>
                    <SupplierSelect :model-value="form.supplierId || null"
                                    :text="form.supplierText"
                                    placeholder="تایپ یا انتخاب تأمین‌کننده"
                                    :error="fieldError('supplier')"
                                    :disabled="saving"
                                    allow-create
                                    @update:model-value="form.supplierId = $event ?? ''"
                                    @update:text="form.supplierText = $event; clearFieldError('supplier')"
                                    @clear="clearFieldError('supplier')" />
                    <small v-if="isNewSupplier()"
                           class="receipt-new-master-data"
                           :class="{ 'receipt-new-master-data--denied': !canManageMasterData }">
                      <i :class="canManageMasterData ? 'las la-plus-circle' : 'las la-exclamation-circle'" aria-hidden="true"></i>
                      {{canManageMasterData? 'تأمین‌کننده جدید هنگام ثبت اضافه می‌شود.': 'مجوز افزودن تأمین‌کننده جدید را ندارید؛ از فهرست انتخاب کنید.'
                      }}
                    </small>
                  </div>
                  <div class="col-md-6 form-group">
                    <label>شماره رسید</label>
                    <input v-model="form.receiptNumber" class="form-control" :class="{ 'is-invalid': fieldError('receiptNumber') }" type="text" maxlength="100" :disabled="saving" @input="clearFieldError('receiptNumber')" />
                    <small v-if="fieldError('receiptNumber')" class="invalid-feedback d-block">{{ fieldError('receiptNumber') }}</small>
                  </div>
                </div>
              </div>
              <div class="modal-footer">
                <button class="btn btn-light px-4" type="button" :disabled="saving" @click="closeReceiptInvoiceModal">انصراف</button>
                <button class="btn btn-primary px-4" type="submit" :disabled="saving">
                  <span v-if="saving" class="spinner-border spinner-border-sm ml-1"></span>
                  ثبت اطلاعات
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>
</template>

<style scoped>
  .goods-receipt-modal .modal-dialog {
    margin: 1.5rem auto;
    max-height: 85vh;
    max-width: 1450px;
    width: calc(100vw - 3rem);
  }

  .goods-receipt-modal .modal-content {
    max-height: 85vh;
  }

    .goods-receipt-modal .modal-content > form {
      display: flex;
      flex: 1 1 auto;
      flex-direction: column;
      min-height: 0;
    }

  .goods-receipt-modal .modal-body {
    flex: 1 1 auto;
    min-height: 0;
  }

  .goods-receipts-toolbar {
    --goods-receipts-toolbar-button-height: 2.1rem;
    align-items: center;
    display: flex;
    gap: .65rem;
  }

  .goods-receipts-view-mode {
    align-items: center;
    background: var(--body-bg);
    border: 1px solid var(--border-color);
    border-radius: .55rem;
    box-sizing: border-box;
    display: flex;
    gap: .5rem;
    padding: .25rem .35rem .25rem .6rem;
  }

    .goods-receipts-view-mode > span {
      color: var(--text-secondary);
      font-size: .76rem;
      white-space: nowrap;
    }

    .goods-receipts-view-mode .form-control {
      background-color: var(--surface);
      border: 0;
      box-shadow: none;
      min-width: 9rem;
    }

  .goods-receipts-column-settings {
    position: relative;
  }

  .goods-receipts-action-group {
    height: var(--goods-receipts-toolbar-button-height);
  }

    .goods-receipts-action-group > .btn:first-of-type {
      border-bottom-left-radius: 0 !important;
      border-bottom-right-radius: .35rem !important;
      border-top-left-radius: 0 !important;
      border-top-right-radius: .35rem !important;
    }

    .goods-receipts-action-group > .btn:last-of-type {
      border-bottom-left-radius: .35rem !important;
      border-bottom-right-radius: 0 !important;
      border-top-left-radius: .35rem !important;
      border-top-right-radius: 0 !important;
    }

    .goods-receipts-action-group > .btn:only-of-type {
      border-radius: .35rem !important;
    }

  .goods-receipts-toolbar-icon {
    align-items: center;
    display: inline-flex;
    height: 100%;
    justify-content: center;
    padding: 0;
    width: 2.1rem;
  }

  .goods-receipts-toolbar > .mobile-icon-button {
    align-items: center;
    display: inline-flex;
    height: var(--goods-receipts-toolbar-button-height);
    justify-content: center;
  }

  .goods-receipts-mobile-actions {
    display: none;
  }

    .goods-receipts-toolbar-icon i {
      font-size: 1.15rem;
      margin: 0;
    }

  .goods-receipts-column-menu {
    background: var(--surface);
    border: 1px solid var(--border-color);
    border-radius: .65rem;
    box-shadow: 0 .75rem 1.75rem rgba(15, 23, 42, .18);
    left: 0;
    max-height: 22rem;
    min-width: 14rem;
    overflow-y: auto;
    padding: .4rem;
    position: absolute;
    top: calc(100% + .4rem);
    z-index: 1080;
  }

  .goods-receipts-column-menu__title {
    border-bottom: 1px solid var(--border-color);
    color: var(--text-secondary);
    font-size: .75rem;
    font-weight: 600;
    margin-bottom: .25rem;
    padding: .4rem .5rem .55rem;
  }

  .goods-receipts-column-option {
    align-items: center;
    border-radius: .4rem;
    color: var(--text-primary);
    cursor: pointer;
    display: flex;
    font-size: .82rem;
    gap: .55rem;
    margin: 0;
    padding: .45rem .5rem;
    white-space: nowrap;
  }

    .goods-receipts-column-option:hover {
      background: var(--primary-light);
      color: var(--primary-dark);
    }

    .goods-receipts-column-option input {
      accent-color: var(--primary);
      flex: 0 0 auto;
    }

  .goods-receipts-description-cell {
    display: block;
    font-size: .75rem;
    line-height: 1.5;
    margin-inline: 0;
    max-width: none;
    overflow: visible;
    overflow-wrap: anywhere;
    text-overflow: clip;
    white-space: pre-wrap;
  }

  :deep(.goods-receipts-item-description-column) {
    min-width: 19rem !important;
    white-space: normal !important;
    width: 19rem !important;
  }

  .goods-receipts-quantity-cell {
    color: var(--text-primary);
    font-weight: 400;
  }

  .receipt-items-header {
    align-items: center;
    display: flex;
    gap: 1rem;
    justify-content: space-between;
  }

  .receipt-entry-actions {
    align-items: center;
    display: flex;
    flex-wrap: wrap;
    gap: .55rem;
  }

  .receipt-entry-mode .btn {
    align-items: center;
    display: inline-flex;
    justify-content: center;
  }

  .receipt-list-entry {
    background: var(--surface);
    border: 1px solid var(--border-color);
    border-radius: .65rem;
    margin-bottom: .75rem;
  }

  .receipt-list-table-scroll {
    overflow-x: auto;
    overflow-y: visible;
    width: 100%;
  }

  .receipt-list-table {
    --receipt-row-column-width: 2rem;
    --receipt-product-column-width: 14rem;
    --receipt-code-column-width: 100px;
    --receipt-unit-column-width: 70px;
    --receipt-quantity-column-width: 70px;
    --receipt-warehouse-column-width: 80px;
    --receipt-invoice-status-column-width: 70px;
    --receipt-invoice-number-column-width: 100px;
    --receipt-supplier-column-width: 11rem;
    --receipt-number-column-width: 90px;
    --receipt-request-column-width: 80px;
    --receipt-description-column-width: 10rem;
    --receipt-action-column-width: var(--receipt-row-column-width);
    border-collapse: separate;
    border-spacing: 0;
    font-size: .74rem;
    table-layout: fixed;
    min-width: calc( var(--receipt-row-column-width) + var(--receipt-product-column-width) + var(--receipt-code-column-width) + var(--receipt-unit-column-width) + var(--receipt-quantity-column-width) + var(--receipt-warehouse-column-width) + var(--receipt-invoice-status-column-width) + var(--receipt-invoice-number-column-width) + var(--receipt-supplier-column-width) + var(--receipt-number-column-width) + var(--receipt-request-column-width) + var(--receipt-description-column-width) + var(--receipt-action-column-width) );
    width: 100%;
  }

    .receipt-list-table th {
      background: var(--body-bg);
      border-bottom: 1px solid var(--border-color);
      border-left: 1px solid var(--border-color);
      color: var(--text-secondary);
      font-size: .69rem;
      font-weight: 700;
      padding: .4rem 0;
      position: sticky;
      text-align: center;
      top: 0;
      white-space: nowrap;
      z-index: 4;
    }

      .receipt-list-table th:last-child {
        border-left: 0;
      }

    .receipt-list-table td {
      border-bottom: 1px solid var(--border-color);
      border-left: 1px solid var(--border-color);
      padding: 0;
      position: relative;
      vertical-align: top;
    }

      .receipt-list-table td:last-child {
        border-left: 0;
      }

    .receipt-list-table tbody tr:last-child td {
      border-bottom: 0;
    }

    .receipt-list-table tbody .receipt-list-unit-cell,
    .receipt-list-table tbody .receipt-list-quantity-cell,
    .receipt-list-table tbody .receipt-list-warehouse-cell,
    .receipt-list-table tbody .receipt-list-invoice-status-cell,
    .receipt-list-table tbody .receipt-list-invoice-number-cell,
    .receipt-list-table tbody .receipt-list-receipt-number-cell,
    .receipt-list-table tbody .receipt-list-request-cell {
      height: 1px;
    }

    .receipt-list-table td > .form-control,
    .receipt-list-table td > .form-control:disabled,
    .receipt-list-table td > .form-control[readonly],
    .receipt-list-table .receipt-list-unit-cell > .receipt-product-combobox > .form-control,
    .receipt-list-table .receipt-list-unit-cell > .receipt-product-combobox > .form-control:disabled,
    .receipt-list-table .receipt-list-unit-cell > .receipt-product-combobox > .form-control[readonly] {
      background-color: transparent !important;
      border-color: transparent;
      border-radius: 0;
      box-sizing: border-box;
      font-size: .68rem;
      height: 100%;
      margin: 0;
      min-height: 1.85rem;
      min-width: 0;
      opacity: 1;
      padding: .25rem .35rem;
      width: 100%;
    }

    .receipt-list-table .receipt-list-unit-cell > .receipt-product-combobox {
      height: 100%;
      min-height: 1.85rem;
    }

    .receipt-list-table .receipt-product-combobox .form-control {
      padding-left: 1.65rem;
    }

    .receipt-list-table td > .form-control:focus,
    .receipt-list-table .receipt-list-unit-cell > .receipt-product-combobox > .form-control:focus {
      background-color: transparent !important;
      border-color: var(--primary);
      box-shadow: 0 0 0 .12rem rgba(37, 99, 235, .12);
    }

    .receipt-list-table .form-control.is-invalid {
      border-color: #dc3545;
      box-shadow: inset 0 0 0 1px rgba(220, 53, 69, .18);
    }

    .receipt-list-table tbody tr:has(.receipt-product-options),
    .receipt-list-table tbody tr:has(.supplier-select--open),
    .receipt-list-table tbody tr:has(.product-select--open) {
      position: relative;
      z-index: 100068;
    }

    .receipt-list-table td:has(.receipt-product-options),
    .receipt-list-table td:has(.supplier-select--open),
    .receipt-list-table td:has(.product-select--open) {
      z-index: 100069;
    }

    .receipt-list-table .receipt-product-combobox:has(> .receipt-product-options) {
      z-index: 100070;
    }

    .receipt-list-table .receipt-product-options {
      min-width: 17rem;
      z-index: 100071;
    }

    .receipt-list-table tbody tr:nth-last-child(-n + 3) .receipt-product-options {
      bottom: calc(100% + .35rem);
      top: auto;
    }

    .receipt-list-table .receipt-list-product-cell {
      width: var(--receipt-product-column-width);
    }

    .receipt-list-table .receipt-list-code-cell {
      width: var(--receipt-code-column-width);
    }

    .receipt-list-table .receipt-list-unit-cell {
      width: var(--receipt-unit-column-width);
    }

    .receipt-list-table .receipt-list-warehouse-cell {
      width: var(--receipt-warehouse-column-width);
    }

    .receipt-list-table .receipt-list-invoice-status-cell {
      width: var(--receipt-invoice-status-column-width);
    }

    .receipt-list-table .receipt-list-invoice-number-cell {
      width: var(--receipt-invoice-number-column-width);
    }

    .receipt-list-table .receipt-list-supplier-cell {
      width: var(--receipt-supplier-column-width);
    }

    /*
     * انتخابگر کالا و طرف حساب در جدول ورود کالا بدون کادر و با پس‌زمینهٔ شفاف
     * است تا با سطرهای جدول یکدست دیده شود. این مقادیر پیش‌فرض کامپوننت‌ها
     * نیستند و از بیرون آن‌ها بازنویسی می‌شوند.
     */
    .receipt-list-table .receipt-list-product-cell :deep(.product-select),
    .receipt-list-table .receipt-list-code-cell :deep(.product-select),
    .receipt-list-table .receipt-list-supplier-cell :deep(.supplier-select) {
      --supplier-select-input-background: transparent;
      --supplier-select-input-border-color: transparent;
      --supplier-select-input-border-width: 0;
      --supplier-select-input-radius: 0;
      --product-select-input-background: transparent;
      --product-select-input-border-color: transparent;
      --product-select-input-border-width: 0;
      --product-select-input-radius: 0;
    }

    .receipt-list-table .receipt-list-code-cell :deep(.product-select) {
      --product-select-menu-width: var(--receipt-product-column-width);
    }

    .receipt-list-table .receipt-list-product-cell :deep(.product-select__control .form-control),
    .receipt-list-table .receipt-list-code-cell :deep(.product-select__control .form-control),
    .receipt-list-table .receipt-list-supplier-cell :deep(.supplier-select__control .form-control) {
      font-size: .68rem;
    }

    .receipt-list-table .receipt-list-product-cell :deep(.product-select .form-control:focus),
    .receipt-list-table .receipt-list-code-cell :deep(.product-select .form-control:focus),
    .receipt-list-table .receipt-list-supplier-cell :deep(.supplier-select .form-control:focus) {
      background-color: transparent;
    }

    .receipt-list-table .receipt-list-receipt-number-cell {
      width: var(--receipt-number-column-width);
    }

    .receipt-list-table .receipt-list-quantity-cell {
      width: var(--receipt-quantity-column-width);
    }

    .receipt-list-table .receipt-list-request-cell {
      width: var(--receipt-request-column-width);
    }

    .receipt-list-table .receipt-list-description-cell {
      width: auto;
    }

  .receipt-list-row-number {
    color: var(--text-secondary);
    text-align: center !important;
    width: var(--receipt-row-column-width);
  }

  .receipt-list-action-column {
    background: var(--surface);
    text-align: center !important;
    width: var(--receipt-action-column-width);
  }

    .receipt-list-action-column .master-data-row-action {
      margin-inline: auto;
    }

  .receipt-list-table td > .receipt-list-description-input {
    box-sizing: border-box;
    height: auto;
    line-height: 1.35;
    min-height: 1.85rem !important;
    overflow: hidden;
    resize: none;
    white-space: pre-wrap;
  }

  .receipt-list-row--empty {
    opacity: .68;
  }

    .receipt-list-row--empty:focus-within {
      opacity: 1;
    }

  .receipt-list-row--new-product td {
    background-color: rgba(245, 158, 11, .12);
  }

  :global(body[data-app-theme='dark'] .receipt-list-row--new-product td) {
    background-color: rgba(251, 191, 36, .11);
  }

  .receipt-list-cell-note {
    color: var(--primary);
    display: block;
    font-size: .67rem;
    line-height: 1.2;
    margin: .2rem .35rem 0;
  }

  .receipt-list-cell-error {
    font-size: .65rem;
    line-height: 1.2;
    margin: .15rem .3rem .25rem;
  }

  .receipt-item-row {
    padding: .75rem;
    margin-bottom: .65rem;
    border: 1px solid var(--border, #e6e6e6);
    border-radius: .65rem;
    background-color: #fbfbfb;
  }

  .receipt-item-row--new-product {
    background-color: rgba(245, 158, 11, .12);
    border-color: rgba(245, 158, 11, .35);
  }

  .receipt-item-row:last-child .receipt-product-options {
    bottom: calc(100% + .35rem);
    top: auto;
  }

  :global(body[data-app-theme='dark'] .receipt-item-row) {
    background-color: #1e2536;
    border-color: #334155;
    color: #f1f5f9;
  }

  :global(body[data-app-theme='dark'] .receipt-item-row--new-product) {
    background-color: rgba(251, 191, 36, .11);
    border-color: rgba(251, 191, 36, .3);
  }

  .receipt-product-combobox {
    --receipt-product-options-width: max(100%, 18rem);
    position: relative;
  }

  .receipt-product-combobox--field-width {
    --receipt-product-options-width: 100%;
  }

  .receipt-product-combobox > i {
    color: var(--text-secondary);
    left: .7rem;
    pointer-events: none;
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
  }

  .receipt-product-combobox .form-control {
    padding-left: 2rem;
  }

  .receipt-product-options {
    background: var(--surface);
    border: 1px solid var(--border-color);
    border-radius: .65rem;
    box-shadow: 0 .75rem 1.75rem rgba(15, 23, 42, .18);
    max-height: 15rem;
    min-width: 0;
    overflow-y: auto;
    padding: .3rem;
    position: absolute;
    right: 0;
    top: calc(100% + .35rem);
    width: var(--receipt-product-options-width);
    z-index: 100070;
  }

  .receipt-product-option {
    align-items: stretch;
    background: transparent;
    border: 0;
    border-radius: .45rem;
    color: var(--text-primary);
    cursor: pointer;
    display: flex;
    flex-direction: column;
    font-size: 11px;
    gap: .4rem;
    padding: .25rem .25rem;
    text-align: right;
    width: 100%;
  }

    .receipt-product-option + .receipt-product-option {
      margin-top: .15rem;
    }

    .receipt-product-option:hover, .receipt-product-option:focus {
      background: var(--primary-light);
      color: var(--primary-dark);
      outline: 0;
    }

  .receipt-product-option__title {
    display: block;
    font-weight: 600;
    min-width: 0;
    overflow: hidden;
    text-align: right;
    text-overflow: ellipsis;
    white-space: nowrap;
    width: 100%;
  }

  .receipt-product-option__meta {
    align-items: center;
    color: var(--text-secondary);
    direction: ltr;
    display: flex;
    font-size: .54rem;
    gap: .35rem;
    justify-content: flex-start;
    text-align: left;
    width: 100%;
  }

  .receipt-product-option__unit {
    direction: rtl;
  }

  .receipt-product-option__meta span {
    background: var(--body-bg);
    border: 1px solid var(--border-color);
    border-radius: 999px;
    padding: .12rem .4rem;
  }

  .receipt-product-option:hover .receipt-product-option__meta {
    color: inherit;
  }

  .receipt-product-empty {
    align-items: center;
    color: var(--text-secondary);
    display: flex;
    font-size: .8rem;
    gap: .4rem;
    padding: .7rem .55rem;
  }

    .receipt-product-empty i {
      color: var(--primary);
      font-size: 1.05rem;
    }

  .receipt-new-master-data {
    align-items: center;
    color: var(--primary);
    display: flex;
    font-size: .76rem;
    gap: .3rem;
    margin-top: .35rem;
  }

  .receipt-new-master-data--denied {
    color: var(--error, #dc3545);
  }

  .receipt-new-product-message {
    margin-bottom: -.25rem;
    margin-top: .55rem;
  }

  .receipt-item-primary-row, .receipt-item-secondary-row {
    display: grid;
    grid-template-columns: minmax(13rem, 1fr) minmax(6.5rem, .25fr) minmax(6.5rem, .25fr) minmax(5.5rem, .2fr) minmax(9rem, .38fr);
    gap: .65rem;
  }

  .receipt-item-details-row {
    display: grid;
    gap: .65rem;
    grid-template-columns: repeat(5, minmax(8rem, 1fr));
    margin-top: .65rem;
  }

  .receipt-item-primary-row {
    align-items: start;
  }

  .receipt-item-secondary-row {
    margin-top: .65rem;
    align-items: end;
  }

  .receipt-item-description {
    grid-column: 1;
  }

  .receipt-item-actions {
    grid-column: 5;
    align-self: end;
    justify-self: end;
    padding-bottom: .15rem;
  }

  .receipt-remove-item {
    color: #dc3545 !important;
    border-color: rgba(220, 53, 69, .45) !important;
  }

    .receipt-remove-item:hover:not(:disabled) {
      color: #fff !important;
      background-color: #dc3545 !important;
      border-color: #dc3545 !important;
    }

  .receipt-code-field input, .receipt-unit-field input, .receipt-quantity-field input {
    text-align: center;
  }

    .receipt-code-field input[readonly], .receipt-unit-field input[readonly] {
      background-color: rgba(0, 0, 0, .035);
    }

  @media (max-width: 991px) {
    .receipt-item-primary-row, .receipt-item-secondary-row {
      grid-template-columns: minmax(0, 1fr) minmax(7rem, .35fr);
    }

    .receipt-item-details-row {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .receipt-item-description {
      grid-column: 1;
    }

    .receipt-item-actions {
      grid-column: 2;
    }
  }

  @media (max-width: 767px) {
    .goods-receipts-page:has(.goods-receipts-mobile-actions) {
      padding-bottom: calc(5rem + env(safe-area-inset-bottom));
    }

    .goods-receipts-page > .card > .card-header {
      align-items: stretch;
      flex-direction: column;
      padding: .75rem;
    }

      .goods-receipts-page > .card > .card-header > div:first-child {
        min-width: 0;
        width: 100%;
      }

      .goods-receipts-page > .card > .card-header .card-title {
        font-size: 1rem;
        line-height: 1.5;
        margin: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

    .goods-receipts-toolbar {
      display: grid;
      gap: .5rem;
      grid-template-columns: minmax(0, 1fr) auto;
      width: 100%;
    }

    .goods-receipts-view-mode {
      min-width: 0;
      padding: .2rem .35rem;
      width: 100%;
    }

      .goods-receipts-view-mode .form-control {
        flex: 1 1 auto;
        min-width: 0;
        width: 100%;
      }

    .goods-receipts-action-group {
      justify-self: end;
    }

      .goods-receipts-action-group > .goods-receipts-import-button,
      .goods-receipts-action-group > .goods-receipts-desktop-export {
        display: none;
      }

      .goods-receipts-action-group > .goods-receipts-column-button {
        border-radius: .35rem !important;
      }

    .goods-receipts-desktop-add {
      display: none !important;
    }

    .goods-receipts-page > .card > .card-body {
      padding: .75rem;
    }

    .goods-receipts-table-container :deep(.dt-layout-table > div) {
      border: 0 !important;
      border-radius: 0;
      overflow: visible;
    }

    .goods-receipts-table-container :deep(table.dataTable) {
      table-layout: fixed;
      width: 100% !important;
    }

    .goods-receipts-table-container--items :deep(.goods-receipts-date-column) {
      width: 8rem !important;
    }

    .goods-receipts-table-container--items :deep(.goods-receipts-product-title-column) {
      text-align: right !important;
      white-space: normal !important;
      width: auto !important;
    }

    .goods-receipts-table-container :deep(table.dataTable > thead > tr > th),
    .goods-receipts-table-container :deep(table.dataTable > tbody > tr > td) {
      border: 0 !important;
    }

    .goods-receipts-table-container :deep(table.dataTable > thead > tr > th::before),
    .goods-receipts-table-container :deep(table.dataTable > tbody > tr > td::before) {
      display: none !important;
    }

    .goods-receipts-table-container :deep(table.dataTable.dtr-inline.collapsed > tbody > tr > td.dtr-control::before),
    .goods-receipts-table-container :deep(table.dataTable.dtr-inline.collapsed > tbody > tr > th.dtr-control::before) {
      background: transparent !important;
      border-radius: 0 !important;
      box-shadow: none !important;
      display: inline-block !important;
    }

    .goods-receipts-table-container :deep(table.dataTable > tbody > tr.child > td) {
      background: var(--body-bg);
      padding: .35rem .55rem .65rem !important;
    }

    .goods-receipts-table-container :deep(table.dataTable > tbody > tr.child ul.dtr-details) {
      display: block;
      margin: 0;
      width: 100%;
    }

    .goods-receipts-table-container :deep(table.dataTable > tbody > tr.child ul.dtr-details > li) {
      align-items: start;
      display: grid;
      gap: .75rem;
      grid-template-columns: 6.5rem minmax(0, 1fr);
      padding: .55rem .2rem;
    }

      .goods-receipts-table-container :deep(table.dataTable > tbody > tr.child ul.dtr-details > li:last-child) {
        border-bottom: 0;
      }

    .goods-receipts-mobile-actions {
      background: var(--surface);
      background: color-mix(in srgb, var(--surface) 94%, transparent);
      border-top: 1px solid var(--border-color);
      bottom: 0;
      box-shadow: 0 -.5rem 1.5rem rgba(15, 23, 42, .12);
      display: grid;
      gap: .6rem;
      grid-auto-columns: minmax(0, 1fr);
      grid-auto-flow: column;
      left: 0;
      padding: .65rem .75rem calc(.65rem + env(safe-area-inset-bottom));
      position: fixed;
      right: 0;
      z-index: 3;
    }

      .goods-receipts-mobile-actions .btn {
        align-items: center;
        display: inline-flex;
        font-size: .7rem;
        gap: .2rem;
        justify-content: center;
        min-height: 2.2rem;
        padding: .25rem;
      }

    .goods-receipts-page :deep(.dt-container .dt-layout-row:last-child) {
      align-items: stretch;
      gap: .65rem;
    }

    .goods-receipts-page :deep(.users-table-summary),
    .goods-receipts-page :deep(.users-table-pagination) {
      justify-content: center;
      margin: 0;
      width: 100%;
    }
  }

  @media (max-width: 575px) {
    .goods-receipt-modal .modal-dialog {
      margin: .25rem auto;
      max-height: 85vh;
      max-width: calc(100vw - .5rem);
      width: calc(100vw - .5rem);
    }

    .goods-receipts-view-mode {
      padding-left: .25rem;
    }

      .goods-receipts-view-mode > span {
        display: none;
      }

      .goods-receipts-view-mode .form-control {
        min-width: 8.25rem;
      }

    .receipt-items-header {
      align-items: stretch;
      flex-direction: column;
    }

    .receipt-entry-actions {
      justify-content: space-between;
    }

    .receipt-entry-mode {
      display: flex;
      flex: 1 1 auto;
    }

      .receipt-entry-mode .btn {
        flex: 1 1 50%;
        min-width: 0;
      }

    .receipt-product-options {
      min-width: 100%;
    }

    .receipt-item-primary-row {
      grid-template-columns: 1fr;
    }

    .receipt-item-details-row {
      grid-template-columns: 1fr;
    }

    .receipt-item-secondary-row {
      grid-template-columns: minmax(0, 1fr) auto;
    }

    .receipt-item-description {
      grid-column: 1;
    }

    .receipt-item-actions {
      grid-column: 2;
    }
  }

  ::highlight(table-search) {
    background-color: #fde047;
    color: #111827;
  }
</style>
