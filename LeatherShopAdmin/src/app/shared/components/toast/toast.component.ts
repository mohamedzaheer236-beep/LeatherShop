import { Component, ChangeDetectionStrategy } from '@angular/core';
import { ToastModule } from 'primeng/toast';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [ToastModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p-toast position="top-right" [life]="5000" [style]="{ 'margin-top': '60px' }"></p-toast>`,
})
export class ToastComponent {}
