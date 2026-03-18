import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { OrderNotification } from './signalr.service';
import { ApiResponse } from '../models/api-response.model';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/notifications`;

  /** Fetch unread notifications (max 50, most recent first). */
  getUnread(): Observable<OrderNotification[]> {
    return this.http
      .get<ApiResponse<OrderNotification[]>>(`${this.baseUrl}/unread`)
      .pipe(map(res => res.data));
  }

  /** Mark a single notification as read by its database ID. */
  markAsRead(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/read`, null);
  }

  /** Mark all unread notifications as read. */
  markAllAsRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/read-all`, null);
  }
}
