import { numberToWords } from '@persian-tools/persian-tools'

/** Convert the displayed rial amount without rounding away fractional tomans. */
export function rialToTomanWords(value: string): string {
  const normalized = value.replace(/,/g, '').trim()
  if (!/^\d+(\.\d{0,2})?$/.test(normalized)) return ''

  const [rials = '', decimalRials = ''] = normalized.split('.')
  // Shift the decimal point one place instead of dividing a floating-point amount.
  const wholeTomans = Number(rials.slice(0, -1) || '0')
  const fractionalDigits = `${rials.slice(-1)}${decimalRials}`.replace(/0+$/, '')
  if (!Number.isSafeInteger(wholeTomans)) return ''

  const wholeWords = numberToWords(wholeTomans)
  if (typeof wholeWords !== 'string') return ''
  if (!fractionalDigits) return `${wholeWords} تومان`

  const fractionWords = numberToWords(Number(fractionalDigits))
  if (typeof fractionWords !== 'string') return ''
  const denominator = ['دهم', 'صدم', 'هزارم'][fractionalDigits.length - 1]
  return `${wholeWords} ممیز ${fractionWords} ${denominator} تومان`
}
