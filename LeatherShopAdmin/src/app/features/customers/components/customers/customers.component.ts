import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { DatePipe, UpperCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { trigger, transition, style, animate } from '@angular/animations';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { CustomerService } from '../../services/customer.service';
import { Customer, CustomerWithSelection } from '../../models/customer.model';
import { CUSTOMER_CATEGORIES } from '../../models/customer.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { getCategorySeverity, getCategoryLabel } from '../../../../shared/utils/severity.utils';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { CustomerAddDialogComponent } from '../customer-add-dialog/customer-add-dialog.component';
import { CustomerEditDialogComponent } from '../customer-edit-dialog/customer-edit-dialog.component';
import { CustomerDeleteDialogComponent } from '../customer-delete-dialog/customer-delete-dialog.component';
import { CustomerImportDialogComponent } from '../customer-import-dialog/customer-import-dialog.component';
import { CustomerBroadcastDialogComponent } from '../customer-broadcast-dialog/customer-broadcast-dialog.component';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { CheckboxModule } from 'primeng/checkbox';
import { TooltipModule } from 'primeng/tooltip';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [
    DatePipe,
    UpperCasePipe,
    FormsModule,
    LoadingSpinnerComponent,
    CustomerAddDialogComponent,
    CustomerEditDialogComponent,
    CustomerDeleteDialogComponent,
    CustomerImportDialogComponent,
    CustomerBroadcastDialogComponent,
    TableModule,
    ButtonModule,
    InputTextModule,
    TagModule,
    CheckboxModule,
    TooltipModule,
    DropdownModule,
    CalendarModule,
    ConfirmDialogModule,
  ],
  templateUrl: './customers.component.html',
  styleUrl: './customers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ConfirmationService],
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
export class CustomersComponent implements OnInit {
  private customerService = inject(CustomerService);
  private notification = inject(NotificationService);
  private cdr = inject(ChangeDetectorRef);
  private confirmationService = inject(ConfirmationService);

  customers: CustomerWithSelection[] = [];
  loading = true;
  subscriberCount: number | null = 0;
  totalCount: number | null = 0;
  categoryOptions = CUSTOMER_CATEGORIES;

  // Dialog visibility
  showAddDialog = false;
  showEditDialog = false;
  showDeleteConfirm = false;
  showImportDialog = false;
  showBroadcastDialog = false;

  // Context for edit/delete dialogs
  editTarget: Customer | null = null;
  deleteTarget: Customer | null = null;

  // Selection — tracked by ID→phone map so selections survive page changes
  allSelected = false;
  private _selectedMap = new Map<number, string>();

  // Pagination & sorting (lazy table)
  totalRecords = 0;
  pageSize = 25;
  sortField = 'createdAt';
  sortOrder = -1;
  private _loadId = 0;

  // Column filter state
  showFilters = false;
  filters = {
    name: '',
    phone: '',
    address: '',
    category: '',
    subscribedOnly: '',
    orderCountMin: '',
    orderCountMax: '',
    dateFrom: null as Date | null,
    dateTo: null as Date | null,
  };
  hasActiveFilters = false;

  categoryFilterOptions = [
    { label: 'All', value: '' },
    ...CUSTOMER_CATEGORIES.map(c => ({ label: c.label, value: c.value })),
  ];

  subscribedFilterOptions = [
    { label: 'All', value: '' },
    { label: 'Active', value: 'true' },
    { label: 'Inactive', value: 'false' },
  ];

  ngOnInit(): void {
    this.loadCounts();
  }

  get selectedPhoneNumbers(): string[] {
    return Array.from(this._selectedMap.values());
  }

  get selectedIds(): number[] {
    return Array.from(this._selectedMap.keys());
  }

  get selectedCount(): number {
    return this._selectedMap.size;
  }

  // ─── Data Loading ───

  loadCounts(): void {
    this.customerService.getSubscriberCount().subscribe({
      next: data => {
        this.subscriberCount = data.subscriberCount;
        this.totalCount = data.totalCount;
        this.cdr.markForCheck();
      },
      error: () => {
        this.subscriberCount = null;
        this.totalCount = null;
        this.cdr.markForCheck();
      },
    });
  }

  loadCustomers(page = 1): void {
    this.loading = true;
    this.cdr.markForCheck();
    const loadId = ++this._loadId;
    const sortOrderStr = this.sortOrder === 1 ? 'asc' : 'desc';
    const subscribedOnly = this.filters.subscribedOnly === 'true' ? true : this.filters.subscribedOnly === 'false' ? false : undefined;
    this.customerService
      .getCustomers(subscribedOnly, undefined, this.filters.category || undefined, page, this.pageSize,
        this.sortField, sortOrderStr, this.getActiveFilters())
      .subscribe({
        next: result => {
          if (loadId !== this._loadId) return; // discard stale response
          this.customers = result.items.map(c => ({ ...c, selected: this._selectedMap.has(c.id) }));
          this.totalRecords = result.totalCount;
          this.allSelected = this.customers.length > 0 && this.customers.every(c => c.selected);
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          if (loadId !== this._loadId) return;
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  // ─── Lazy Load & Filtering ───

  onLazyLoad(event: TableLazyLoadEvent): void {
    const page = Math.floor((event.first ?? 0) / (event.rows ?? this.pageSize)) + 1;
    this.pageSize = event.rows ?? this.pageSize;
    if (event.sortField) {
      this.sortField = event.sortField as string;
      this.sortOrder = event.sortOrder ?? -1;
    }
    this.loadCustomers(page);
  }

  applyFilters(): void {
    this.updateHasActiveFilters();
    this.loadCustomers(1);
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
    this.filters = { name: '', phone: '', address: '', category: '', subscribedOnly: '', orderCountMin: '', orderCountMax: '', dateFrom: null, dateTo: null };
    this.hasActiveFilters = false;
    this.loadCustomers(1);
  }

  getCategoryLabel(value: string): string {
    return getCategoryLabel(value, CUSTOMER_CATEGORIES);
  }

  getCategorySeverity(value: string) {
    return getCategorySeverity(value);
  }

  // ─── Selection ───

  toggleSelectAll(): void {
    this.customers.forEach(c => {
      c.selected = this.allSelected;
      if (this.allSelected) this._selectedMap.set(c.id, c.phoneNumber);
      else this._selectedMap.delete(c.id);
    });
  }

  onRowSelect(customer: CustomerWithSelection): void {
    if (customer.selected) this._selectedMap.set(customer.id, customer.phoneNumber);
    else this._selectedMap.delete(customer.id);
    this.allSelected = this.customers.length > 0 && this.customers.every(c => c.selected);
  }

  clearSelection(): void {
    this.customers.forEach(c => (c.selected = false));
    this._selectedMap.clear();
    this.allSelected = false;
  }

  // ─── Dialog Openers ───

  openEditDialog(customer: Customer): void {
    this.editTarget = customer;
    this.showEditDialog = true;
  }

  confirmDelete(customer: Customer): void {
    this.deleteTarget = customer;
    this.showDeleteConfirm = true;
  }

  confirmBulkDelete(): void {
    const count = this.selectedCount;
    this.confirmationService.confirm({
      message: `Are you sure you want to delete ${count} selected customer(s)? Customers with orders will be skipped.`,
      header: 'Confirm Bulk Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.customerService.bulkDeleteCustomers(this.selectedIds).subscribe({
          next: result => {
            if (result.skippedWithOrders > 0) {
              this.notification.warning(result.message);
            } else {
              this.notification.success(result.message);
            }
            this.clearSelection();
            this.loadCustomers();
            this.loadCounts();
            this.cdr.markForCheck();
          },
          error: () => {
            this.cdr.markForCheck();
          },
        });
      },
    });
  }

  // ─── Dialog Callbacks (refresh data after child operations) ───

  onCustomerSaved(): void {
    this.loadCustomers();
    this.loadCounts();
  }

  onCustomerDeleted(): void {
    if (this.deleteTarget) {
      this._selectedMap.delete(this.deleteTarget.id);
    }
    this.deleteTarget = null;
    this.loadCustomers();
    this.loadCounts();
  }

  onCustomersImported(): void {
    this.loadCustomers();
    this.loadCounts();
  }

  onBroadcastSent(): void {
    this.showBroadcastDialog = false;
  }

  // ─── Subscription Toggle (inline, not a dialog) ───

  toggleSubscription(customer: Customer): void {
    const newValue = !customer.isSubscribed;
    this.customerService.toggleSubscription(customer.id, newValue).subscribe({
      next: () => {
        this.notification.success(`Subscription ${newValue ? 'enabled' : 'disabled'}.`);
        this.loadCounts();
        // Reload list to respect active filters (e.g. subscribed-only)
        this.loadCustomers();
      },
      error: () => {
        // Toast shown by error interceptor
      },
    });
  }

  // ─── Private Helpers ───

  private getActiveFilters(): Record<string, string> | undefined {
    const active: Record<string, string> = {};
    const f = this.filters;
    if (f.name.trim()) active['name'] = f.name.trim();
    if (f.phone.trim()) active['phone'] = f.phone.trim();
    if (f.address.trim()) active['address'] = f.address.trim();
    if (f.dateFrom) {
      const d = f.dateFrom;
      active['dateFrom'] = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    if (f.dateTo) {
      const d = f.dateTo;
      active['dateTo'] = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    if (f.orderCountMin !== '' && f.orderCountMin != null) active['orderCountMin'] = String(f.orderCountMin);
    if (f.orderCountMax !== '' && f.orderCountMax != null) active['orderCountMax'] = String(f.orderCountMax);
    return Object.keys(active).length > 0 ? active : undefined;
  }

  private updateHasActiveFilters(): void {
    const f = this.filters;
    this.hasActiveFilters = !!(f.name.trim() || f.phone.trim() || f.address.trim() || f.category || f.subscribedOnly || f.dateFrom || f.dateTo || (f.orderCountMin !== '' && f.orderCountMin != null) || (f.orderCountMax !== '' && f.orderCountMax != null));
  }
}
