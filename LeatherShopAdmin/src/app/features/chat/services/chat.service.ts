import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { Conversation, PaginatedMessages, ChatMessage, FailedOutboxMessage } from '../models/chat.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { environment } from '../../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private baseUrl = `${environment.apiUrl}/chat`;

  constructor(private http: HttpClient) {}

  getConversations(search?: string): Observable<Conversation[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<ApiResponse<Conversation[]>>(`${this.baseUrl}/conversations`, { params }).pipe(map(res => res.data));
  }

  getMessages(customerId: number, page: number = 1, pageSize: number = 50): Observable<PaginatedMessages> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<ApiResponse<PaginatedMessages>>(`${this.baseUrl}/${customerId}/messages`, { params }).pipe(map(res => res.data));
  }

  sendMessage(customerId: number, message: string): Observable<ChatMessage> {
    return this.http.post<ApiResponse<ChatMessage>>(`${this.baseUrl}/${customerId}/send`, { message }).pipe(map(res => res.data));
  }

  toggleBot(customerId: number): Observable<{ isBotPaused: boolean }> {
    return this.http.post<ApiResponse<{ isBotPaused: boolean }>>(`${this.baseUrl}/${customerId}/toggle-bot`, {}).pipe(map(res => res.data));
  }

  deleteConversation(customerId: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/${customerId}/messages`);
  }

  getFailedMessages(): Observable<FailedOutboxMessage[]> {
    return this.http.get<ApiResponse<FailedOutboxMessage[]>>(`${this.baseUrl}/failed-messages`).pipe(map(res => res.data));
  }

  retryOutboxMessage(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/outbox/${id}/retry`, {}).pipe(map(() => void 0));
  }

  getFailedMessageCount(): Observable<number> {
    return this.http.get<ApiResponse<{ count: number }>>(`${this.baseUrl}/failed-messages/count`).pipe(map(res => res.data.count));
  }
}
