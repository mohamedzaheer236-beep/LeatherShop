import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, interval, concatMap, takeWhile, last, take, EMPTY, catchError } from 'rxjs';
import { BroadcastRequest, BroadcastResult, BroadcastHistory, WhatsAppTemplate, BroadcastRecipient, BroadcastDeliverySummary, BroadcastRetryResult } from '../models/broadcast.model';
import { PaginatedResult } from '../../../core/models/paginated-result.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class BroadcastService {
  private http = inject(HttpClient);

  private baseUrl = `${environment.apiUrl}/broadcast`;

  sendBroadcast(request: BroadcastRequest): Observable<BroadcastResult> {
    return this.http.post<ApiResponse<BroadcastResult>>(`${this.baseUrl}/send`, request).pipe(map(res => res.data));
  }

  getBroadcastStatus(broadcastId: number): Observable<BroadcastHistory> {
    return this.http
      .get<ApiResponse<BroadcastHistory>>(`${this.baseUrl}/${broadcastId}/status`)
      .pipe(map(res => res.data));
  }

  getBroadcastHistory(page = 1, pageSize = 10, sortField?: string, sortOrder?: string, filters?: Record<string, string>): Observable<PaginatedResult<BroadcastHistory>> {
    let params = new HttpParams().set('page', page.toString()).set('pageSize', pageSize.toString());
    if (sortField) params = params.set('sortField', sortField);
    if (sortOrder) params = params.set('sortOrder', sortOrder);
    if (filters) {
      for (const [key, value] of Object.entries(filters)) {
        if (value) params = params.set(key, value);
      }
    }
    return this.http
      .get<ApiResponse<PaginatedResult<BroadcastHistory>>>(`${this.baseUrl}/history`, { params })
      .pipe(map(res => res.data));
  }

  getApprovedTemplates(): Observable<WhatsAppTemplate[]> {
    return this.http.get<ApiResponse<WhatsAppTemplate[]>>(`${this.baseUrl}/templates`).pipe(map(res => res.data));
  }

  uploadImage(file: File): Observable<string> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ApiResponse<string>>(`${this.baseUrl}/upload-image`, formData).pipe(map(res => res.data));
  }

  getTotalSentCount(): Observable<number> {
    return this.http.get<ApiResponse<number>>(`${this.baseUrl}/stats`).pipe(map(res => res.data));
  }

  /**
   * Polls a broadcast's delivery status every second until all messages are
   * processed or 30 attempts are exhausted.
   *
   * Returns an Observable that emits the final {@link BroadcastStatus} and
   * completes. Unsubscribing cancels the polling automatically.
   */
  pollBroadcastStatus(broadcastId: number, totalRecipients: number): Observable<BroadcastHistory> {
    const maxAttempts = 30;
    return interval(1000).pipe(
      take(maxAttempts),
      concatMap(() => this.getBroadcastStatus(broadcastId).pipe(catchError(() => EMPTY))),
      takeWhile(status => status.sentCount + status.failedCount < totalRecipients, true),
      last(),
    );
  }

  getRecipients(broadcastId: number, page = 1, pageSize = 20, status?: string): Observable<PaginatedResult<BroadcastRecipient>> {
    let params = new HttpParams().set('page', page.toString()).set('pageSize', pageSize.toString());
    if (status) params = params.set('status', status);
    return this.http
      .get<ApiResponse<PaginatedResult<BroadcastRecipient>>>(`${this.baseUrl}/${broadcastId}/recipients`, { params })
      .pipe(map(res => res.data));
  }

  getDeliverySummary(broadcastId: number): Observable<BroadcastDeliverySummary> {
    return this.http
      .get<ApiResponse<BroadcastDeliverySummary>>(`${this.baseUrl}/${broadcastId}/delivery-summary`)
      .pipe(map(res => res.data));
  }

  retryFailedRecipients(broadcastId: number): Observable<BroadcastRetryResult> {
    return this.http
      .post<ApiResponse<BroadcastRetryResult>>(`${this.baseUrl}/${broadcastId}/retry`, {})
      .pipe(map(res => res.data));
  }
}
