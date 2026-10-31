import { usePermissions } from '@/composables/usePermissions'

export function useGoodsReceiptColumnPermissions() {
  const { has } = usePermissions()
  const canReadWarehouse = has('GoodsReceiptsWarehouseRead')
  const canReadInvoiceStatus = has('GoodsReceiptsInvoiceStatusRead')
  const canReadCounterparty = has('GoodsReceiptsCounterpartyRead')
  const canReadInvoiceNumber = has('GoodsReceiptsInvoiceNumberRead')
  const canReadReceiptNumber = has('GoodsReceiptsReceiptNumberRead')
  const fields: Record<string, boolean> = {
    warehouseId: canReadWarehouse, warehouseTitle: canReadWarehouse,
    invoiceStatus: canReadInvoiceStatus, counterparty: canReadCounterparty, supplierId: canReadCounterparty,
    invoiceNumber: canReadInvoiceNumber, receiptNumber: canReadReceiptNumber,
  }
  return {
    canReadWarehouse, canReadInvoiceStatus, canReadCounterparty, canReadInvoiceNumber, canReadReceiptNumber,
    canEditWarehouse: canReadWarehouse && has('GoodsReceiptsWarehouseUpdate'),
    canManageInvoice: canReadInvoiceStatus && canReadInvoiceNumber && canReadCounterparty && has('GoodsReceiptsReceiptInvoiceOperations'),
    canManageReceipt: canReadReceiptNumber && has('GoodsReceiptsReceiptRegistration'),
    canReadColumn: (field: string): boolean => fields[field] ?? true,
  }
}
