export type NumericOperator =
  | 'eq'
  | 'neq'
  | 'gt'
  | 'gte'
  | 'lt'
  | 'lte'
  | 'between'

export interface NumericFilterValue {
  operator: NumericOperator
  value: number | null
  valueTo?: number | null
}

export type TextFilterOperator = 'contains' | 'eq'

export interface TextFilterValue {
  operator: TextFilterOperator
  value: string
}

export type DateFilterOperator = 'before' | 'after' | 'eq' | 'between'

export interface DateFilterValue {
  operator: DateFilterOperator
  value: string
  valueTo?: string
}

export interface DateRangeFilterValue {
  from: string
  to: string
}

export interface FilterOption {
  value: string
  label: string
}

interface BaseFilterDefinition {
  key: string
  label: string
}

export interface TextFilterDefinition extends BaseFilterDefinition {
  type: 'text'
  placeholder?: string
  maxlength?: number
}

export interface TextComparisonFilterDefinition extends BaseFilterDefinition {
  type: 'text-comparison'
  placeholder?: string
  maxlength?: number
}

export interface SelectFilterDefinition extends BaseFilterDefinition {
  type: 'select' | 'multiselect'
  options?: FilterOption[]
  loadOptions?: () => Promise<FilterOption[]>
  placeholder?: string
}

export interface DateFilterDefinition extends BaseFilterDefinition {
  type: 'date' | 'date-range'
}

export interface DateComparisonFilterDefinition extends BaseFilterDefinition {
  type: 'date-comparison'
}

export interface BooleanFilterDefinition extends BaseFilterDefinition {
  type: 'boolean'
  trueLabel?: string
  falseLabel?: string
}

export interface NumericComparisonFilterDefinition extends BaseFilterDefinition {
  type: 'numeric-comparison'
  numericType: 'integer' | 'decimal'
  min?: number
  max?: number
  step?: number
  precision?: number
}

export type DataTableFilterDefinition =
  | TextFilterDefinition
  | TextComparisonFilterDefinition
  | SelectFilterDefinition
  | DateFilterDefinition
  | DateComparisonFilterDefinition
  | BooleanFilterDefinition
  | NumericComparisonFilterDefinition

export type DataTableFilterValue =
  | string
  | string[]
  | boolean
  | TextFilterValue
  | DateFilterValue
  | DateRangeFilterValue
  | NumericFilterValue
  | null

export type DataTableFilterValues = Record<string, DataTableFilterValue>
