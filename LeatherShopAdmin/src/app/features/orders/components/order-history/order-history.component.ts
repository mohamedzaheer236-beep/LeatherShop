import { Component, ChangeDetectionStrategy, ChangeDetectorRef, inject, OnInit } from '@angular/core';
import { DatePipe, DecimalPipe, UpperCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { trigger, transition, style, animate } from '@angular/animations';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { TooltipModule } from 'primeng/tooltip';
import { OrderService } from '../../services/order.service';
import { Order, OrderStatus } from '../../models/order.model';
import { getStatusSeverity, TagSeverity } from '../../../../shared/utils/severity.utils';

@Component({
  selector: 'app-order-history',
  standalone: true,
  imports: [DatePipe, DecimalPipe, UpperCasePipe, FormsModule, TableModule, TagModule, ButtonModule, InputTextModule, CalendarModule, DropdownModule, TooltipModule],
  templateUrl: './order-history.component.html',
  styleUrl: './order-history.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  animations: [
    trigger('filterAnimation', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(-12px)' }),
        animate('250ms cubic-bezier(0.4, 0, 0.2, 1)', style({ opacity: 1, transform: 'translateY(0)' })),
      ]),
      transition(':leave', [
        animate('200ms cubic-bezier(0.4, 0, 0.2, 1)', style({ opacity: 0, transform: 'translateY(-8px)' })),
      ]),
    ]),
  ],
})
export class OrderHistoryComponent implements OnInit {
  private orderService = inject(OrderService);
  private cdr = inject(ChangeDetectorRef);

  orders: Order[] = [];
  totalRecords = 0;
  pageSize = 25;
  loading = false;
  sortField = 'createdAt';
  sortOrder = -1;

  // Column filter state
  showFilters = false;
  filters = {
    customerName: '',
    customerPhone: '',
    orderNumber: '',
    status: '',
    dateFrom: null as Date | null,
    dateTo: null as Date | null,
    amountMin: '',
    amountMax: '',
    isPaid: '',
    cancelledBy: '',
  };
  hasActiveFilters = false;

  statusFilterOptions = [
    { label: 'All', value: '' },
    { label: 'Pending', value: 'Pending' },
    { label: 'Confirmed', value: 'Confirmed' },
    { label: 'Shipped', value: 'Shipped' },
    { label: 'Delivered', value: 'Delivered' },
    { label: 'Cancelled', value: 'Cancelled' },
  ];

  paidFilterOptions = [
    { label: 'All', value: '' },
    { label: 'Paid', value: 'true' },
    { label: 'Unpaid', value: 'false' },
  ];

  cancelledByFilterOptions = [
    { label: 'All', value: '' },
    { label: 'Customer', value: 'Customer' },
    { label: 'Admin', value: 'Admin' },
    { label: 'System', value: 'System' },
  ];

  // Download tracking
  downloadingIds = new Set<number>();

  ngOnInit(): void {
    this.loadHistory(1);
  }

  applyFilters(): void {
    this.updateHasActiveFilters();
    this.loadHistory(1);
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
    if (!this.showFilters && this.hasActiveFilters) {
      this.resetAll();
    }
    this.cdr.markForCheck();
  }

  resetAll(): void {
    this.sortField = 'createdAt';
    this.sortOrder = -1;
    this.filters = { customerName: '', customerPhone: '', orderNumber: '', status: '', dateFrom: null, dateTo: null, amountMin: '', amountMax: '', isPaid: '', cancelledBy: '' };
    this.hasActiveFilters = false;
    this.loadHistory(1);
  }

  onLazyLoad(event: TableLazyLoadEvent): void {
    const page = Math.floor((event.first ?? 0) / (event.rows ?? this.pageSize)) + 1;
    this.pageSize = event.rows ?? this.pageSize;
    if (event.sortField) {
      this.sortField = event.sortField as string;
      this.sortOrder = event.sortOrder ?? -1;
    }
    this.loadHistory(page);
  }

  loadHistory(page = 1): void {
    this.loading = true;
    this.cdr.markForCheck();
    const sortOrderStr = this.sortOrder === 1 ? 'asc' : 'desc';
    this.orderService.getOrderHistory(page, this.pageSize, this.sortField, sortOrderStr, this.getActiveFilters()).subscribe({
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

  downloadInvoice(order: Order): void {
    this.downloadingIds.add(order.id);
    this.cdr.markForCheck();
    this.orderService.downloadInvoice(order.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Invoice-${order.orderNumber}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
        this.downloadingIds.delete(order.id);
        this.cdr.markForCheck();
      },
      error: () => {
        this.downloadingIds.delete(order.id);
        this.cdr.markForCheck();
      },
    });
  }

  isDownloading(id: number): boolean {
    return this.downloadingIds.has(id);
  }

  getSeverity(status: string): TagSeverity {
    return getStatusSeverity(status);
  }

  private getActiveFilters(): Record<string, string> | undefined {
    const active: Record<string, string> = {};
    const f = this.filters;
    if (f.customerName.trim()) active['customerName'] = f.customerName.trim();
    if (f.customerPhone.trim()) active['customerPhone'] = f.customerPhone.trim();
    if (f.orderNumber.trim()) active['orderNumber'] = f.orderNumber.trim();
    if (f.status) active['status'] = f.status;
    if (f.dateFrom) {
      const d = f.dateFrom;
      active['dateFrom'] = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    if (f.dateTo) {
      const d = f.dateTo;
      active['dateTo'] = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    if (f.amountMin !== '' && f.amountMin != null) active['amountMin'] = String(f.amountMin);
    if (f.amountMax !== '' && f.amountMax != null) active['amountMax'] = String(f.amountMax);
    if (f.isPaid) active['isPaid'] = f.isPaid;
    if (f.cancelledBy) active['cancelledBy'] = f.cancelledBy;
    return Object.keys(active).length > 0 ? active : undefined;
  }

  private updateHasActiveFilters(): void {
    const f = this.filters;
    this.hasActiveFilters = !!(f.customerName.trim() || f.customerPhone.trim() || f.orderNumber.trim() || f.status || f.dateFrom || f.dateTo || (f.amountMin !== '' && f.amountMin != null) || (f.amountMax !== '' && f.amountMax != null) || f.isPaid || f.cancelledBy);
  }
}
