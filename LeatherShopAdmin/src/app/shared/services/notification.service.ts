import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

export interface Notification {
  type: 'success' | 'error' | 'info' | 'warning';
  message: string;
  id: number;
}

/**
 * Centralized notification/toast service.
 * Components subscribe to notifications$ to display toasts.
 */
@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private _notifications = new Subject<Notification>();
  private _counter = 0;

  notifications$ = this._notifications.asObservable();

  success(message: string): void {
    this._emit('success', message);
  }

  error(message: string): void {
    this._emit('error', message);
  }

  info(message: string): void {
    this._emit('info', message);
  }

  warning(message: string): void {
    this._emit('warning', message);
  }

  private _emit(type: Notification['type'], message: string): void {
    this._notifications.next({ type, message, id: ++this._counter });
  }
}
