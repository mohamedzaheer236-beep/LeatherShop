import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { OrderService } from '../../services/order.service';
import { Order } from '../../models/order.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { getStatusSeverity, getStatusButtonSeverity, TagSeverity, ButtonSeverity } from '../../../../shared/utils/severity.utils';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { PaginatorModule } from 'primeng/paginator';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LoadingSpinnerComponent, TableModule, TagModule, ButtonModule, DropdownModule, CardModule, ToolbarModule, PaginatorModule],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.scss'
})
export class OrdersComponent implements OnInit {
  orders: Order[] = [];
  loading = true;
  filterForm!: FormGroup;
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

  // Pagination
  totalRecords = 0;
  currentPage = 1;
  pageSize = 25;

  constructor(
    private fb: FormBuilder,
    private orderService: OrderService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.filterForm = this.fb.group({
      filterStatus: ['']
    });
    this.loadOrders();
  }

  loadOrders(): void {
    this.loading = true;
    const filterStatus = this.filterForm.get('filterStatus')?.value || '';
    this.orderService.getOrders(filterStatus, this.currentPage, this.pageSize).subscribe({
      next: (result) => {
        this.orders = result.items;
        this.totalRecords = result.totalCount;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  onFilterChange(): void {
    this.currentPage = 1; // Reset to first page when filter changes
    this.loadOrders();
  }

  onPageChange(event: any): void {
    this.currentPage = event.page + 1; // PrimeNG paginator is 0-based, our API is 1-based
    this.pageSize = event.rows;
    this.expandedOrderId = null; // Collapse any expanded order
    this.loadOrders();
  }

  toggleExpand(orderId: number): void {
    this.expandedOrderId = this.expandedOrderId === orderId ? null : orderId;
  }

  updateStatus(order: Order, newStatus: string): void {
    const previousStatus = order.status;
    this.orderService.updateOrderStatus(order.id, newStatus).subscribe({
      next: () => {
        order.status = newStatus;
        this.notification.success(`Order status updated to ${newStatus}.`);
      },
      error: () => {
        order.status = previousStatus;
        // Toast shown by error interceptor
      }
    });
  }

  getSeverity(status: string): TagSeverity {
    return getStatusSeverity(status);
  }

  getStatusButtonSeverity(status: string, currentStatus: string): ButtonSeverity {
    return getStatusButtonSeverity(status, currentStatus);
  }

  trackByOrderId(_index: number, order: Order): number {
    return order.id;
  }
}