import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { Customer, CreateCustomer, CustomerCreated, UpdateCustomer, BulkImportResult } from '../models/customer.model';
import { PaginatedResult } from '../../../core/models/paginated-result.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class CustomerService {
  private http = inject(HttpClient);

  private baseUrl = `${environment.apiUrl}/customers`;

  getCustomers(
    subscribedOnly?: boolean,
    search?: string,
    category?: string,
    page = 1,
    pageSize = 25,
    sortField?: string,
    sortOrder?: string,
    filters?: Record<string, string>,
  ): Observable<PaginatedResult<Customer>> {
    let params = new HttpParams().set('page', page.toString()).set('pageSize', pageSize.toString());
    if (subscribedOnly) params = params.set('subscribedOnly', 'true');
    if (search) params = params.set('search', search);
    if (category) params = params.set('category', category);
    if (sortField) params = params.set('sortField', sortField);
    if (sortOrder) params = params.set('sortOrder', sortOrder);
    if (filters) {
      for (const [key, value] of Object.entries(filters)) {
        if (value) params = params.set(key, value);
      }
    }
    return this.http.get<ApiResponse<PaginatedResult<Customer>>>(this.baseUrl, { params }).pipe(map(res => res.data));
  }

  createCustomer(customer: CreateCustomer): Observable<CustomerCreated> {
    return this.http.post<ApiResponse<CustomerCreated>>(this.baseUrl, customer).pipe(map(res => res.data));
  }

  updateCustomer(id: number, customer: UpdateCustomer): Observable<Customer> {
    return this.http.put<ApiResponse<Customer>>(`${this.baseUrl}/${id}`, customer).pipe(map(res => res.data));
  }

  deleteCustomer(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/${id}`);
  }

  bulkDeleteCustomers(ids: number[]): Observable<{ deleted: number; skippedWithOrders: number; message: string }> {
    return this.http
      .post<ApiResponse<{ deleted: number; skippedWithOrders: number; message: string }>>(`${this.baseUrl}/bulk-delete`, { ids })
      .pipe(map(res => res.data));
  }

  bulkImportCustomers(customers: CreateCustomer[]): Observable<BulkImportResult> {
    return this.http
      .post<ApiResponse<BulkImportResult>>(`${this.baseUrl}/import`, { customers })
      .pipe(map(res => res.data));
  }

  toggleSubscription(id: number, isSubscribed: boolean): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/subscribe`, JSON.stringify(isSubscribed), {
      headers: { 'Content-Type': 'application/json' },
    });
  }

  checkPhoneExists(phone: string): Observable<boolean> {
    return this.http
      .get<{ exists: boolean }>(`${this.baseUrl}/check-phone`, { params: { phone } })
      .pipe(map(res => res.exists));
  }

  checkPhonesExist(phones: string[]): Observable<string[]> {
    return this.http
      .post<{ existing: string[] }>(`${this.baseUrl}/check-phones`, { phones })
      .pipe(map(res => res.existing));
  }

  getSubscriberCount(): Observable<{ subscriberCount: number; totalCount: number }> {
    return this.http
      .get<ApiResponse<{ subscriberCount: number; totalCount: number }>>(`${this.baseUrl}/count`)
      .pipe(map(res => res.data));
  }
}
