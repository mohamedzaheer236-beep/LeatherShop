import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

import { ProgressSpinnerModule } from 'primeng/progressspinner';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [ProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading) {
      <div class="spinner-overlay" role="status" aria-live="polite">
        <p-progressSpinner strokeWidth="4" animationDuration=".8s"></p-progressSpinner>
        <p class="spinner-text">{{ message }}</p>
      </div>
    }
  `,
  styles: [
    `
      .spinner-overlay {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        padding: 40px;
      }
      .spinner-text {
        margin-top: 12px;
        color: #666;
        font-size: 14px;
      }
    `,
  ],
})
export class LoadingSpinnerComponent {
  @Input() loading = false;
  @Input() message = 'Loading...';
}
