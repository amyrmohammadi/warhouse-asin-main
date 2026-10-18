import type { ApiResult } from '@/services/auth'
import { clearSession, getSession } from '@/services/auth'

export type AuditAction = 'Create' | 'Update' | 'Delete'

export interface AuditLogListItem {
  id: number
  userId: string | null
  userFullName: string
  action: AuditAction
  tableName: string
  primaryKey: string
  oldValues: string | null
  newValues: string | null
  changedColumns: string | null
  occurredAtUtc: string
}

export interface GetAuditLogsResponse {
  items: AuditLogListItem[]
  totalCount: number
  filteredCount: number
  pageNumber: number
  pageSize: number
  paginationEnabled: boolean
}

export interface GetAuditLogsParameters {
  paginationEnabled?: boolean
  pageNumber: number
  pageSize: number
  search?: string
  userFullName?: string
  action?: AuditAction
  tableName?: string
  primaryKey?: string
  changedColumns?: string
  sortBy?: string
  sortDescending?: boolean
  signal?: AbortSignal
}

export class AuditLogsApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message)
    this.name = 'AuditLogsApiError'
  }
}

export async function getAuditLogs(
  parameters: GetAuditLogsParameters,
): Promise<GetAuditLogsResponse> {
  const session = getSession()
  if (!session?.accessToken) {
    throw new AuditLogsApiError(
      'نشست شما منقضی شده است. لطفاً دوباره وارد شوید.',
      401,
    )
  }

  const query = new URLSearchParams({
    paginationEnabled: String(parameters.paginationEnabled ?? false),
    pageNumber: String(parameters.pageNumber),
    pageSize: String(parameters.pageSize),
  })

  if (parameters.search) query.set('search', parameters.search)
  if (parameters.userFullName) query.set('userFullName', parameters.userFullName)
  if (parameters.action) query.set('action', parameters.action)
  if (parameters.tableName) query.set('tableName', parameters.tableName)
  if (parameters.primaryKey) query.set('primaryKey', parameters.primaryKey)
  if (parameters.changedColumns) query.set('changedColumns', parameters.changedColumns)
  if (parameters.sortBy) query.set('sortBy', parameters.sortBy)
  if (parameters.sortDescending !== undefined) {
    query.set('sortDescending', String(parameters.sortDescending))
  }

  let response: Response
  try {
    response = await fetch(`/endpoint/admin/audit-logs?${query}`, {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      signal: parameters.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new AuditLogsApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  if (response.status === 401) clearSession()

  let result: ApiResult<GetAuditLogsResponse> | null = null
  try {
    result = (await response.json()) as ApiResult<GetAuditLogsResponse>
  } catch {
    // The status-specific message below is more useful than a JSON parse error.
  }

  if (!response.ok || !result?.isSuccess || !result.data) {
    const message =
      result?.messages?.find(Boolean) ??
      (response.status === 403
        ? 'شما مجوز مشاهده گزارش فعالیت کاربران را ندارید.'
        : 'دریافت گزارش فعالیت کاربران انجام نشد.')
    throw new AuditLogsApiError(message, response.status)
  }

  return result.data
}
