import { Component } from '@angular/core';
import { ToastModule } from 'primeng/toast';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [ToastModule],
  template: `<p-toast position="top-right" [life]="5000" [style]="{'margin-top': '60px'}"></p-toast>`,
  styles: [`
    :host ::ng-deep .p-toast .p-toast-message {
      border-radius: 12px;
      box-shadow: 0 8px 32px rgba(0,0,0,0.18);
      backdrop-filter: blur(8px);
    }
    :host ::ng-deep .p-toast .p-toast-message-success {
      background: #065f46;
      border: 1px solid #059669;
      color: #ecfdf5;
    }
    :host ::ng-deep .p-toast .p-toast-message-success .p-toast-message-text,
    :host ::ng-deep .p-toast .p-toast-message-success .p-toast-summary,
    :host ::ng-deep .p-toast .p-toast-message-success .p-toast-detail {
      color: #ecfdf5;
    }
    :host ::ng-deep .p-toast .p-toast-message-success .p-toast-icon-close {
      color: #a7f3d0;
    }
    :host ::ng-deep .p-toast .p-toast-message-error {
      background: #7f1d1d;
      border: 1px solid #b91c1c;
      color: #fef2f2;
    }
    :host ::ng-deep .p-toast .p-toast-message-error .p-toast-message-text,
    :host ::ng-deep .p-toast .p-toast-message-error .p-toast-summary,
    :host ::ng-deep .p-toast .p-toast-message-error .p-toast-detail {
      color: #fef2f2;
    }
    :host ::ng-deep .p-toast .p-toast-message-error .p-toast-icon-close {
      color: #fca5a5;
    }
    :host ::ng-deep .p-toast .p-toast-message-warn {
      background: #78350f;
      border: 1px solid #b45309;
      color: #fefce8;
    }
    :host ::ng-deep .p-toast .p-toast-message-warn .p-toast-message-text,
    :host ::ng-deep .p-toast .p-toast-message-warn .p-toast-summary,
    :host ::ng-deep .p-toast .p-toast-message-warn .p-toast-detail {
      color: #fefce8;
    }
    :host ::ng-deep .p-toast .p-toast-message-warn .p-toast-icon-close {
      color: #fde68a;
    }
    :host ::ng-deep .p-toast .p-toast-message-info {
      background: #1e3a5f;
      border: 1px solid #2563eb;
      color: #eff6ff;
    }
    :host ::ng-deep .p-toast .p-toast-message-info .p-toast-message-text,
    :host ::ng-deep .p-toast .p-toast-message-info .p-toast-summary,
    :host ::ng-deep .p-toast .p-toast-message-info .p-toast-detail {
      color: #eff6ff;
    }
    :host ::ng-deep .p-toast .p-toast-message-info .p-toast-icon-close {
      color: #93c5fd;
    }
  `]
})
export class ToastComponent {}
