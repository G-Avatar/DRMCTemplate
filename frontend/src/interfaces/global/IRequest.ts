export interface IRequest<T> {
  status?: number
  errors?: Record<string, string[]>
  message?: string
  data?: T
  loading: boolean
  percentage?: number
}
