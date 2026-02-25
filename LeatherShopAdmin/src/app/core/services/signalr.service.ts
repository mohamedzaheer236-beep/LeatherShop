import { Injectable, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface OrderNotification {
  orderId: number;
  orderNumber: string;
  customerName: string;
  amount: number;
  timestamp: string;
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

@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {
  private hubConnection: signalR.HubConnection | null = null;

  // Observables for components to subscribe to
  readonly newOrder$ = new Subject<OrderNotification>();
  readonly chatMessage$ = new Subject<ChatMessageEvent>();
  readonly newChatMessage$ = new Subject<NewChatMessageEvent>();

  constructor(private auth: AuthService) {}

  /** Start the SignalR connection (call after login). */
  start(): void {
    if (this.hubConnection) return; // already connected

    const token = this.auth.getToken();
    if (!token) return;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, { accessTokenFactory: () => token })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Register event handlers
    this.hubConnection.on('NewOrder', (data: OrderNotification) => this.newOrder$.next(data));
    this.hubConnection.on('ReceiveMessage', (data: ChatMessageEvent) => this.chatMessage$.next(data));
    this.hubConnection.on('NewChatMessage', (data: NewChatMessageEvent) => this.newChatMessage$.next(data));

    this.hubConnection.onclose(() => {
      this.hubConnection = null; // allow start() to create a new connection
    });

    this.hubConnection.start()
      .catch(() => { /* connection error handled by reconnect policy */ });
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
    this.hubConnection?.invoke('JoinCustomerChat', customerId)
      .catch(() => { /* silently handle — hub may not be connected */ });
  }

  /** Leave a customer's chat group. */
  leaveCustomerChat(customerId: number): void {
    this.hubConnection?.invoke('LeaveCustomerChat', customerId)
      .catch(() => { /* silently handle — hub may not be connected */ });
  }

  ngOnDestroy(): void {
    this.stop();
    this.newOrder$.complete();
    this.chatMessage$.complete();
    this.newChatMessage$.complete();
  }
}
