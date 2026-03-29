import { Component, ChangeDetectionStrategy } from '@angular/core';
import { TabViewModule } from 'primeng/tabview';
import { OrdersComponent } from '../orders/orders.component';
import { OrderHistoryComponent } from '../order-history/order-history.component';

@Component({
  selector: 'app-orders-layout',
  standalone: true,
  imports: [TabViewModule, OrdersComponent, OrderHistoryComponent],
  templateUrl: './orders-layout.component.html',
  styleUrl: './orders-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersLayoutComponent {}
