/**
 * Generic wrapper matching the backend PaginatedResult<T> model.
 * Used by all paginated API endpoints (Orders, Customers, Products, Broadcasts).
 */
export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
