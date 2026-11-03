import assert from 'node:assert/strict'
import { test } from 'node:test'
import { rialToTomanWords } from '../src/utils/currency.ts'

test('converts rials to tomans before spelling the amount', () => {
  assert.equal(rialToTomanWords('124,000'), 'دوازده هزار و چهار صد تومان')
  assert.equal(rialToTomanWords('1,240,000'), 'صد و بیست و چهار هزار تومان')
})

test('supports millions, billions and mixed large amounts', () => {
  assert.equal(rialToTomanWords('10,000,000'), 'یک میلیون تومان')
  assert.equal(rialToTomanWords('10,000,000,000'), 'یک میلیارد تومان')
  assert.equal(rialToTomanWords('12,340,000,000'), 'یک میلیارد و دویست و سی و چهار میلیون تومان')
  assert.equal(rialToTomanWords('10,000,000,000,000,000'), 'یک کوآدریلیون تومان')
})

test('preserves fractional tomans without floating-point rounding', () => {
  assert.equal(rialToTomanWords('15'), 'یک ممیز پنج دهم تومان')
  assert.equal(rialToTomanWords('10.50'), 'یک ممیز پنج صدم تومان')
  assert.equal(rialToTomanWords('10.01'), 'یک ممیز یک هزارم تومان')
  assert.equal(rialToTomanWords('0.01'), 'صفر ممیز یک هزارم تومان')
  assert.equal(rialToTomanWords('9007199254740991'),
    `${rialToTomanWords('9007199254740990').replace(' تومان', '')} ممیز یک دهم تومان`)
})

test('handles empty, zero, incomplete decimals and invalid amounts', () => {
  assert.equal(rialToTomanWords(''), '')
  assert.equal(rialToTomanWords('0'), 'صفر تومان')
  assert.equal(rialToTomanWords('10.'), 'یک تومان')
  assert.equal(rialToTomanWords('-10'), '')
  assert.equal(rialToTomanWords('invalid'), '')
  assert.equal(rialToTomanWords('999999999999999999999999'), '')
})
