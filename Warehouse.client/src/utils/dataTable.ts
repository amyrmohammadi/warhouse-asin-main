import type { ColumnsConfig } from 'datatables.net'

export function createRowNumberColumn(sortBy?: string): ColumnsConfig {
  return {
    data: null,
    name: sortBy ?? 'rowNumber',
    title: '#',
    className: 'all text-center data-table-row-number',
    orderable: Boolean(sortBy),
    searchable: false,
    render: (_data, _type, _row, meta) => meta.settings.displayStart + meta.row + 1,
  }
}
