import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe, DecimalPipe } from '@angular/common';
import { PaginatorState } from 'primeng/paginator';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { OrderService } from '../../services/order.service';
import { Order, OrderStatus } from '../../models/order.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { SignalRService } from '../../../../core/services/signalr.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { getStatusSeverity, TagSeverity } from '../../../../shared/utils/severity.utils';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { PaginatorModule } from 'primeng/paginator';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    LoadingSpinnerComponent,
    TableModule,
    TagModule,
    ButtonModule,
    DropdownModule,
    CardModule,
    ToolbarModule,
    PaginatorModule,
    TooltipModule,
    DialogModule,
    InputTextModule,
  ],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersComponent implements OnInit {
  private fb = inject(FormBuilder);
  private orderService = inject(OrderService);
  private notification = inject(NotificationService);
  private signalR = inject(SignalRService);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);

  orders: Order[] = [];
  loading = true;
  filterForm!: FormGroup;
  statusDropdownOptions = [
    { label: 'All Statuses', value: '' },
    { label: 'Pending', value: 'Pending' },
    { label: 'Confirmed', value: 'Confirmed' },
    { label: 'Shipped', value: 'Shipped' },
    { label: 'Delivered', value: 'Delivered' },
    { label: 'Cancelled', value: 'Cancelled' },
  ];
  statusOptions: OrderStatus[] = ['Pending', 'Confirmed', 'Shipped', 'Delivered', 'Cancelled'];
  expandedOrderId: number | null = null;

  /** Mirrors server-side ValidStatusTransitions — keeps UI consistent with backend rules. */
  private readonly validTransitions: Record<OrderStatus, OrderStatus[]> = {
    Pending:   ['Confirmed', 'Cancelled'],
    Confirmed: ['Shipped',   'Cancelled'],
    Shipped:   ['Delivered', 'Cancelled'],
    Delivered: [],
    Cancelled: [],
  };

  isTransitionAllowed(currentStatus: OrderStatus, targetStatus: OrderStatus): boolean {
    return this.validTransitions[currentStatus]?.includes(targetStatus) ?? false;
  }

  // Pagination
  totalRecords = 0;
  currentPage = 1;
  pageSize = 25;

  // Shipping dialog
  showShipDialog = false;
  isEditingTracking = false;
  shipForm!: FormGroup;
  private pendingShipOrder: Order | null = null;

  ngOnInit(): void {
    this.filterForm = this.fb.group({
      filterStatus: [''],
    });
    this.shipForm = this.fb.group({
      trackingNumber: ['', [Validators.required, Validators.maxLength(100)]],
      trackingLink:   ['', [Validators.maxLength(500)]],
    });
    this.loadOrders();

    // Auto-refresh when an order changes via SignalR (new order, payment, cancellation)
    this.signalR.newOrder$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.loadOrders();
    });
  }

  loadOrders(): void {
    this.loading = true;
    const filterStatus = this.filterForm.get('filterStatus')?.value || '';
    this.orderService.getOrders(filterStatus, this.currentPage, this.pageSize).subscribe({
      next: result => {
        this.orders = result.items;
        this.totalRecords = result.totalCount;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  onFilterChange(): void {
    this.currentPage = 1; // Reset to first page when filter changes
    this.loadOrders();
  }

  onPageChange(event: PaginatorState): void {
    this.currentPage = (event.page ?? 0) + 1; // PrimeNG paginator is 0-based, our API is 1-based
    this.pageSize = event.rows ?? this.pageSize;
    this.expandedOrderId = null; // Collapse any expanded order
    this.loadOrders();
  }

  toggleExpand(orderId: number): void {
    this.expandedOrderId = this.expandedOrderId === orderId ? null : orderId;
  }

  updateStatus(order: Order, newStatus: OrderStatus): void {
    // Guard: only allow valid transitions (mirrors server-side rules)
    if (!this.isTransitionAllowed(order.status, newStatus)) return;

    if (newStatus === 'Shipped') {
      this.pendingShipOrder = order;
      this.shipForm.reset();
      this.showShipDialog = true;
      this.cdr.markForCheck();
      return;
    }
    this.submitStatusUpdate(order, newStatus);
  }

  editTracking(order: Order, event: Event): void {
    event.stopPropagation();
    this.pendingShipOrder = order;
    this.isEditingTracking = true;
    this.shipForm.reset({
      trackingNumber: order.trackingNumber ?? '',
      trackingLink: order.trackingLink ?? '',
    });
    this.showShipDialog = true;
    this.cdr.markForCheck();
  }

  confirmShip(): void {
    if (this.shipForm.invalid || !this.pendingShipOrder) return;
    const { trackingNumber, trackingLink } = this.shipForm.value;
    const order = this.pendingShipOrder;
    this.showShipDialog = false;
    if (this.isEditingTracking) {
      const tn = trackingNumber.trim();
      const tl = trackingLink?.trim() || undefined;
      this.orderService.updateTracking(order.id, tn, tl).subscribe({
        next: () => {
          order.trackingNumber = tn;
          order.trackingLink = tl;
          this.notification.success('Tracking updated. Customer notified via WhatsApp.');
          this.cdr.markForCheck();
        },
        error: () => this.cdr.markForCheck(),
      });
    } else {
      this.submitStatusUpdate(order, 'Shipped', trackingNumber.trim(), trackingLink?.trim() || undefined);
    }
  }

  cancelShipDialog(): void {
    this.showShipDialog = false;
    this.pendingShipOrder = null;
    this.isEditingTracking = false;
  }

  private submitStatusUpdate(order: Order, newStatus: OrderStatus, trackingNumber?: string, trackingLink?: string): void {
    const previousStatus = order.status;
    const previousCancelledBy = order.cancelledBy;
    this.orderService.updateOrderStatus(order.id, newStatus, trackingNumber, trackingLink).subscribe({
      next: () => {
        order.status = newStatus;
        if (newStatus === 'Cancelled') order.cancelledBy = 'Admin';
        if (newStatus === 'Shipped') {
          order.trackingNumber = trackingNumber;
          order.trackingLink = trackingLink;
        }
        this.notification.success(`Order status updated to ${newStatus}.`);
        this.cdr.markForCheck();
      },
      error: () => {
        order.status = previousStatus;
        order.cancelledBy = previousCancelledBy;
        this.cdr.markForCheck();
      },
    });
  }

  getSeverity(status: string): TagSeverity {
    return getStatusSeverity(status);
  }

  trackByOrderId(_index: number, order: Order): number {
    return order.id;
  }

  downloadInvoice(event: Event, order: Order): void {
    event.stopPropagation(); // Don't toggle expand
    order.downloading = true;
    this.orderService.downloadInvoice(order.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Invoice-${order.orderNumber || order.id}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
        order.downloading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        order.downloading = false;
        this.cdr.markForCheck();
      },
    });
  }
}
