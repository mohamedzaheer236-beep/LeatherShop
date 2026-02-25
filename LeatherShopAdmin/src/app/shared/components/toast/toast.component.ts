import { Component } from '@angular/core';
import { ToastModule } from 'primeng/toast';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [ToastModule],
  template: `<p-toast position="top-right" [life]="5000" [style]="{'margin-top': '60px'}"></p-toast>`
})
export class ToastComponent {}
