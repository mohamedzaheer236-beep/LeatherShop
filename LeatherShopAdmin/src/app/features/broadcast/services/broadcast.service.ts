import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { BroadcastRequest, BroadcastResult, BroadcastHistory, WhatsAppTemplate } from '../models/broadcast.model';
import { PaginatedResult } from '../../../core/models/paginated-result.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class BroadcastService {
  private baseUrl = `${environment.apiUrl}/broadcast`;
  private customerUrl = `${environment.apiUrl}/customers`;

  constructor(private http: HttpClient) {}

  sendBroadcast(request: BroadcastRequest): Observable<BroadcastResult> {
    return this.http.post<ApiResponse<BroadcastResult>>(`${this.baseUrl}/send`, request).pipe(map(res => res.data));
  }

  getBroadcastStatus(broadcastId: number): Observable<BroadcastHistory> {
    return this.http.get<ApiResponse<BroadcastHistory>>(`${this.baseUrl}/${broadcastId}/status`).pipe(map(res => res.data));
  }

  getBroadcastHistory(page: number = 1, pageSize: number = 10): Observable<PaginatedResult<BroadcastHistory>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<ApiResponse<PaginatedResult<BroadcastHistory>>>(`${this.baseUrl}/history`, { params }).pipe(map(res => res.data));
  }

  getApprovedTemplates(): Observable<WhatsAppTemplate[]> {
    return this.http.get<ApiResponse<WhatsAppTemplate[]>>(`${this.baseUrl}/templates`).pipe(map(res => res.data));
  }

  uploadImage(file: File): Observable<string> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ApiResponse<string>>(`${this.baseUrl}/upload-image`, formData).pipe(map(res => res.data));
  }

  getSubscriberCount(): Observable<{ subscriberCount: number; totalCount: number }> {
    return this.http.get<ApiResponse<{ subscriberCount: number; totalCount: number }>>(`${this.customerUrl}/count`).pipe(map(res => res.data));
  }
}
