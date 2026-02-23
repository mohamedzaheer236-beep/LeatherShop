import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [CommonModule, ProgressSpinnerModule],
  template: `
    <div class="spinner-overlay" *ngIf="loading" role="status" aria-live="polite">
      <p-progressSpinner strokeWidth="4" animationDuration=".8s"></p-progressSpinner>
      <p class="spinner-text">{{ message || 'Loading...' }}</p>
    </div>
  `,
  styles: [`
    .spinner-overlay {
      display: flex; flex-direction: column; align-items: center;
      justify-content: center; padding: 40px;
    }
    .spinner-text { margin-top: 12px; color: #666; font-size: 14px; }
  `]
})
export class LoadingSpinnerComponent {
  @Input() loading = false;
  @Input() message = 'Loading...';
}
