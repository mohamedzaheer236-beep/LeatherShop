/**
 * Typed wrapper matching the backend ApiResponse<T> envelope.
 * Replaces all `http.get<any>(...).pipe(map(res => res.data))` patterns
 * with proper type safety: `http.get<ApiResponse<T>>(...).pipe(map(res => res.data))`.
 */
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errors?: string[];
}
