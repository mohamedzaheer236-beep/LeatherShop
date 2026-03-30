import { Injectable, OnDestroy, inject } from '@angular/core';
import { Subject, firstValueFrom } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface OrderNotification {
  id: number;
  orderId: number;
  orderNumber: string;
  customerName: string;
  amount: number;
  timestamp: string;
  status: string;
}

export interface ChatMessageEvent {
  id: number;
  customerId: number;
  direction: string;
  messageType: string;
  content: string;
  senderName: string;
  isFromBot: boolean;
  timestamp: string;
}

export interface NewChatMessageEvent {
  customerId: number;
  customerName: string;
  content: string;
  timestamp: string;
}

export interface OutboxFailedEvent {
  outboxMessageId: number;
  customerName: string;
  context: string;
  lastError: string;
  failedAt: string;
}

export interface BroadcastProgressEvent {
  broadcastId: number;
  sent: number;
  failed: number;
  total: number;
  status: 'processing' | 'completed';
}

export interface BroadcastRetryProgressEvent {
  broadcastId: number;
  processed: number;
  succeeded: number;
  failed: number;
  total: number;
  status: 'processing' | 'completed';
}

@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {
  private auth = inject(AuthService);

  private hubConnection: signalR.HubConnection | null = null;

  // Observables for components to subscribe to
  readonly newOrder$ = new Subject<OrderNotification>();
  readonly chatMessage$ = new Subject<ChatMessageEvent>();
  readonly newChatMessage$ = new Subject<NewChatMessageEvent>();
  readonly outboxFailed$ = new Subject<OutboxFailedEvent>();
  readonly broadcastProgress$ = new Subject<BroadcastProgressEvent>();
  readonly broadcastRetryProgress$ = new Subject<BroadcastRetryProgressEvent>();

  /** Start the SignalR connection (call after login). */
  start(): void {
    if (this.hubConnection) return; // already connected

    if (!this.auth.getToken()) return;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, {
        accessTokenFactory: async () => {
          // If the current token is expired, refresh it before reconnecting
          if (!this.auth.isLoggedIn()) {
            try {
              const res = await firstValueFrom(this.auth.refreshAccessToken());
              if (!res.success) return '';
            } catch {
              return '';
            }
          }
          return this.auth.getToken() ?? '';
        },
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Register event handlers
    this.hubConnection.on('NewOrder', (data: OrderNotification) => this.newOrder$.next(data));
    this.hubConnection.on('ReceiveMessage', (data: ChatMessageEvent) => this.chatMessage$.next(data));
    this.hubConnection.on('NewChatMessage', (data: NewChatMessageEvent) => this.newChatMessage$.next(data));
    this.hubConnection.on('OutboxMessageFailed', (data: OutboxFailedEvent) => this.outboxFailed$.next(data));
    this.hubConnection.on('BroadcastProgress', (data: BroadcastProgressEvent) => this.broadcastProgress$.next(data));
    this.hubConnection.on('BroadcastRetryProgress', (data: BroadcastRetryProgressEvent) => this.broadcastRetryProgress$.next(data));

    this.hubConnection.onclose(() => {
      this.hubConnection = null; // allow start() to create a new connection
    });

    this.startWithRetry();
  }

  /**
   * Attempts initial SignalR connection with retry.
   * withAutomaticReconnect only handles drops AFTER a successful connection.
   * This handles the case where the hub is unreachable at login time.
   */
  private startWithRetry(attempt = 0): void {
    const maxRetries = 5;
    const delays = [0, 2000, 5000, 10000, 30000];

    this.hubConnection?.start().catch(() => {
      if (attempt < maxRetries && this.hubConnection) {
        const delay = delays[Math.min(attempt, delays.length - 1)];
        setTimeout(() => this.startWithRetry(attempt + 1), delay);
      }
      // After max retries, give up silently — user can refresh the page
    });
  }

  /** Stop the connection (call on logout). Returns a Promise so callers can await completion. */
  stop(): Promise<void> {
    if (this.hubConnection) {
      const conn = this.hubConnection;
      this.hubConnection = null; // prevent new calls while stopping
      return conn.stop();
    }
    return Promise.resolve();
  }

  /** Join a customer's chat group to receive real-time messages. */
  joinCustomerChat(customerId: number): void {
    this.hubConnection?.invoke('JoinCustomerChat', customerId).catch(() => {
      /* silently handle — hub may not be connected */
    });
  }

  /** Leave a customer's chat group. */
  leaveCustomerChat(customerId: number): void {
    this.hubConnection?.invoke('LeaveCustomerChat', customerId).catch(() => {
      /* silently handle — hub may not be connected */
    });
  }

  ngOnDestroy(): void {
    this.stop();
    this.newOrder$.complete();
    this.chatMessage$.complete();
    this.newChatMessage$.complete();
    this.outboxFailed$.complete();
    this.broadcastProgress$.complete();
  }
}
