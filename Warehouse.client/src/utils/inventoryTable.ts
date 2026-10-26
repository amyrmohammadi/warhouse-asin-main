import type { ColumnsConfig, Config } from 'datatables.net'
import DataTablesCore from 'datatables.net-bs4'

interface InventoryTableOptions {
  ajax: Config['ajax']
  columns: ColumnsConfig[]
  emptyText: string
  loadingText: string
  zeroText: string
  /** مرتب‌سازی پیش‌فرض بر اساس `name` ستون؛ بدون آن، مرتب‌سازی غیرفعال است. */
  order?: { name: string; dir: 'asc' | 'desc' }
  onDraw?: () => void
  onInit?: () => void
}

/** تنظیمات مشترک جدول‌های سمت سرور در صفحه‌های ورود و خروج. */
export function createInventoryTableOptions(o: InventoryTableOptions): Config {
  const orderIndex = o.order ? o.columns.findIndex(column => column.name === o.order!.name) : -1
  return {
    ajax: o.ajax,
    columns: o.columns,
    serverSide: true,
    processing: true,
    searching: false,
    ordering: orderIndex >= 0,
    order: orderIndex >= 0 ? [[orderIndex, o.order!.dir]] : [],
    pageLength: 50,
    lengthMenu: [10,20, 50, 100,200,500],
    autoWidth: false,
    pagingType: 'full_numbers',
    responsive: {
      details: { type: 'inline', renderer: DataTablesCore.Responsive.renderer.listHiddenNodes() },
    },
    drawCallback: o.onDraw,
    initComplete: o.onInit,
    layout: {
      topStart: null,
      topEnd: null,
      bottomStart: { className: 'users-table-summary', features: ['pageLength', 'info'] },
      bottomEnd: { className: 'users-table-pagination', features: ['paging'] },
    },
    language: {
      emptyTable: o.emptyText,
      info: '_START_–_END_ از _TOTAL_',
      infoEmpty: '۰ از ۰',
      infoFiltered: '',
      lengthMenu: '_MENU_',
      loadingRecords: o.loadingText,
      processing: 'در حال پردازش...',
      zeroRecords: o.zeroText,
      paginate: { first: '<<', last: '>>', next: '>', previous: '<' },
    },
  }
}
