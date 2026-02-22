import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="spinner-overlay" *ngIf="loading">
      <div class="spinner"></div>
      <p class="spinner-text" *ngIf="message">{{ message }}</p>
    </div>
  `,
  styles: [`
    .spinner-overlay {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 40px;
    }
    .spinner {
      width: 40px;
      height: 40px;
      border: 4px solid #e0e0e0;
      border-top: 4px solid #2e7d32;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    .spinner-text {
      margin-top: 12px;
      color: #666;
      font-size: 14px;
    }
    @keyframes spin {
      from { transform: rotate(0deg); }
      to { transform: rotate(360deg); }
    }
  `]
})
export class LoadingSpinnerComponent {
  @Input() loading = false;
  @Input() message = 'Loading...';
}
