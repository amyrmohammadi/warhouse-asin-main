import type { ColumnsConfig } from 'datatables.net'

export function createRowNumberColumn(): ColumnsConfig {
  return {
    data: null,
    name: 'rowNumber',
    title: 'ردیف',
    className: 'all text-center data-table-row-number',
    orderable: false,
    searchable: false,
    render: (_data, _type, _row, meta) => meta.settings.displayStart + meta.row + 1,
  }
}
