const pad = (n: number): string => String(n).padStart(2, '0')

function datePart(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** مقدار UTC/ISO را به `YYYY-MM-DD` در منطقه زمانی محلی تبدیل می‌کند. */
export function toLocalDate(value: string | Date): string {
  return datePart(new Date(value))
}

/** خروجی `YYYY-MM-DDTHH:mm:ss` مطابق فرمت DatePicker. بدون ترفند toISOString تا با DST خراب نشود. */
export function toLocalDateTime(value: string | Date): string {
  const d = new Date(value)
  return `${datePart(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

export function nowLocalDateTime(): string {
  return toLocalDateTime(new Date())
}

export function localDateTimeToIso(local: string): string {
  return new Date(local).toISOString()
}

/** تاریخ بدون ساعت (مثل تاریخ عودت) را نیمه‌شب محلی در نظر می‌گیرد. */
export function localDateToIso(local: string): string {
  return new Date(`${local}T00:00:00`).toISOString()
}

const persianDate = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
  year: 'numeric', month: '2-digit', day: '2-digit',
})

/** تقویم جلالی صریح است و به تنظیمات مرورگر وابسته نیست. */
export function formatPersianDate(value: string | null | undefined, fallback = '—'): string {
  return value ? persianDate.format(new Date(value)) : fallback
}

const persianNumber = new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 3 })
export function formatPersianNumber(value: number): string {
  return persianNumber.format(value)
}
