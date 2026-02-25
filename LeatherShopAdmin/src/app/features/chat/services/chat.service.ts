import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { Conversation, PaginatedMessages } from '../models/chat.model';
import { environment } from '../../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private baseUrl = `${environment.apiUrl}/chat`;

  constructor(private http: HttpClient) {}

  getConversations(search?: string): Observable<Conversation[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<any>(`${this.baseUrl}/conversations`, { params }).pipe(map(res => res.data));
  }

  getMessages(customerId: number, page: number = 1, pageSize: number = 50): Observable<PaginatedMessages> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<any>(`${this.baseUrl}/${customerId}/messages`, { params }).pipe(map(res => res.data));
  }

  sendMessage(customerId: number, message: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${customerId}/send`, { message }).pipe(map(res => res.data));
  }

  toggleBot(customerId: number): Observable<{ isBotPaused: boolean }> {
    return this.http.post<any>(`${this.baseUrl}/${customerId}/toggle-bot`, {}).pipe(map(res => res.data));
  }

  deleteConversation(customerId: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/${customerId}/messages`).pipe(map(res => res));
  }
}
