import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OrderService } from '../../services/order.service';
import { Order } from '../../models/order.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { getStatusSeverity, getStatusButtonSeverity as sharedButtonSeverity, TagSeverity } from '../../../../shared/utils/severity.utils';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent, TableModule, TagModule, ButtonModule, DropdownModule, CardModule, ToolbarModule],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.scss'
})
export class OrdersComponent implements OnInit {
  orders: Order[] = [];
  loading = true;
  filterStatus = '';
  statusDropdownOptions = [
    { label: 'All Statuses', value: '' },
    { label: 'Pending', value: 'Pending' },
    { label: 'Confirmed', value: 'Confirmed' },
    { label: 'Shipped', value: 'Shipped' },
    { label: 'Delivered', value: 'Delivered' },
    { label: 'Cancelled', value: 'Cancelled' }
  ];
  statusOptions = ['Pending', 'Confirmed', 'Shipped', 'Delivered', 'Cancelled'];
  expandedOrderId: number | null = null;

  constructor(
    private orderService: OrderService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void { this.loadOrders(); }

  loadOrders(): void {
    this.loading = true;
    this.orderService.getOrders(this.filterStatus).subscribe({
      next: (data) => { this.orders = data; this.loading = false; },
      error: () => this.loading = false
    });
  }

  onFilterChange(): void { this.loadOrders(); }

  toggleExpand(orderId: number): void {
    this.expandedOrderId = this.expandedOrderId === orderId ? null : orderId;
  }

  updateStatus(order: Order, newStatus: string): void {
    this.orderService.updateOrderStatus(order.id, newStatus).subscribe({
      next: () => {
        order.status = newStatus;
        this.notification.success(`Order status updated to ${newStatus}.`);
      }
    });
  }

  getSeverity(status: string): TagSeverity {
    return getStatusSeverity(status);
  }

  getStatusButtonSeverity(status: string, currentStatus: string): TagSeverity {
    return sharedButtonSeverity(status, currentStatus);
  }
}