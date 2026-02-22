import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { NotificationService, Notification } from '../../services/notification.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container">
      @for (toast of toasts; track toast.id) {
        <div class="toast" [class]="'toast-' + toast.type" (click)="remove(toast.id)">
          <span class="toast-icon">
            @switch (toast.type) {
              @case ('success') { ✅ }
              @case ('error') { ❌ }
              @case ('warning') { ⚠️ }
              @case ('info') { ℹ️ }
            }
          </span>
          <span class="toast-message">{{ toast.message }}</span>
          <button class="toast-close" (click)="remove(toast.id)">&times;</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-container {
      position: fixed;
      top: 20px;
      right: 20px;
      z-index: 10000;
      display: flex;
      flex-direction: column;
      gap: 8px;
      max-width: 400px;
    }
    .toast {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 12px 16px;
      border-radius: 8px;
      color: #fff;
      font-size: 14px;
      box-shadow: 0 4px 12px rgba(0,0,0,0.15);
      cursor: pointer;
      animation: slideIn 0.3s ease;
    }
    .toast-success { background: #2e7d32; }
    .toast-error { background: #c62828; }
    .toast-warning { background: #f57f17; }
    .toast-info { background: #1565c0; }
    .toast-icon { font-size: 16px; }
    .toast-message { flex: 1; }
    .toast-close {
      background: none;
      border: none;
      color: #fff;
      font-size: 18px;
      cursor: pointer;
      opacity: 0.7;
      padding: 0 4px;
    }
    .toast-close:hover { opacity: 1; }
    @keyframes slideIn {
      from { transform: translateX(100%); opacity: 0; }
      to { transform: translateX(0); opacity: 1; }
    }
  `]
})
export class ToastComponent implements OnInit, OnDestroy {
  toasts: Notification[] = [];
  private sub!: Subscription;

  constructor(private notification: NotificationService) {}

  ngOnInit(): void {
    this.sub = this.notification.notifications$.subscribe(toast => {
      this.toasts.push(toast);
      setTimeout(() => this.remove(toast.id), 5000);
    });
  }

  remove(id: number): void {
    this.toasts = this.toasts.filter(t => t.id !== id);
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }
}
