import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { BroadcastRequest, BroadcastHistory, WhatsAppTemplate } from '../models/broadcast.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class BroadcastService {
  private baseUrl = `${environment.apiUrl}/broadcast`;
  private customerUrl = `${environment.apiUrl}/customers`;

  constructor(private http: HttpClient) {}

  sendBroadcast(request: BroadcastRequest): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/send`, request).pipe(map(res => res.data));
  }

  getBroadcastHistory(): Observable<BroadcastHistory[]> {
    return this.http.get<any>(`${this.baseUrl}/history`).pipe(map(res => res.data));
  }

  getApprovedTemplates(): Observable<WhatsAppTemplate[]> {
    return this.http.get<any>(`${this.baseUrl}/templates`).pipe(map(res => res.data));
  }

  getSubscriberCount(): Observable<{ subscriberCount: number; totalCount: number }> {
    return this.http.get<any>(`${this.customerUrl}/count`).pipe(map(res => res.data));
  }
}
