import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { Order } from '../models/order.model';
import { PaginatedResult } from '../../../core/models/paginated-result.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class OrderService {
  private http = inject(HttpClient);

  private baseUrl = `${environment.apiUrl}/orders`;

  getOrders(status?: string, page = 1, pageSize = 25): Observable<PaginatedResult<Order>> {
    let params = new HttpParams().set('page', page.toString()).set('pageSize', pageSize.toString());
    if (status) params = params.set('status', status);
    return this.http.get<ApiResponse<PaginatedResult<Order>>>(this.baseUrl, { params }).pipe(map(res => res.data));
  }

  updateOrderStatus(id: number, status: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/status`, { status });
  }

  downloadInvoice(orderId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${orderId}/invoice`, { responseType: 'blob' });
  }
}
