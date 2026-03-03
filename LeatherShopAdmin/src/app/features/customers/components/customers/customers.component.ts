import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { PaginatorState } from 'primeng/paginator';
import { CustomerService } from '../../services/customer.service';
import { Customer } from '../../models/customer.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { CustomerAddDialogComponent } from '../customer-add-dialog/customer-add-dialog.component';
import { CustomerEditDialogComponent } from '../customer-edit-dialog/customer-edit-dialog.component';
import { CustomerDeleteDialogComponent } from '../customer-delete-dialog/customer-delete-dialog.component';
import { CustomerImportDialogComponent } from '../customer-import-dialog/customer-import-dialog.component';
import { CustomerBroadcastDialogComponent } from '../customer-broadcast-dialog/customer-broadcast-dialog.component';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { ToolbarModule } from 'primeng/toolbar';
import { BadgeModule } from 'primeng/badge';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { PaginatorModule } from 'primeng/paginator';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    ReactiveFormsModule,
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
    CardModule,
    CheckboxModule,
    ToolbarModule,
    BadgeModule,
    MessageModule,
    TooltipModule,
    PaginatorModule,
  ],
  templateUrl: './customers.component.html',
  styleUrl: './customers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomersComponent implements OnInit {
  private fb = inject(FormBuilder);
  private customerService = inject(CustomerService);
  private notification = inject(NotificationService);
  private cdr = inject(ChangeDetectorRef);

  customers: Customer[] = [];
  loading = true;
  subscriberCount: number | null = 0;
  totalCount: number | null = 0;

  filterForm!: FormGroup;

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

  // Pagination
  totalRecords = 0;
  currentPage = 1;
  pageSize = 25;

  ngOnInit(): void {
    this.filterForm = this.fb.group({
      searchTerm: [''],
      subscribedOnly: [false],
    });
    this.loadCustomers();
    this.loadCounts();
  }

  get selectedPhoneNumbers(): string[] {
    return Array.from(this._selectedMap.values());
  }

  get selectedCount(): number {
    return this._selectedMap.size;
  }

  get searchTerm(): string {
    return this.filterForm.get('searchTerm')?.value || '';
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

  loadCustomers(): void {
    this.loading = true;
    const { searchTerm, subscribedOnly } = this.filterForm.value;
    this.customerService
      .getCustomers(subscribedOnly, searchTerm || undefined, this.currentPage, this.pageSize)
      .subscribe({
        next: result => {
          this.customers = result.items.map(c => ({ ...c, selected: this._selectedMap.has(c.id) }));
          this.totalRecords = result.totalCount;
          this.allSelected = this.customers.length > 0 && this.customers.every(c => c.selected);
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  // ─── Filtering & Pagination ───

  onFilterChange(): void {
    this.currentPage = 1;
    this.loadCustomers();
  }

  onSearch(): void {
    this.currentPage = 1;
    this.loadCustomers();
  }

  clearSearch(): void {
    this.filterForm.patchValue({ searchTerm: '' });
    this.currentPage = 1;
    this.loadCustomers();
  }

  onPageChange(event: PaginatorState): void {
    this.currentPage = (event.page ?? 0) + 1;
    this.pageSize = event.rows ?? this.pageSize;
    this.loadCustomers();
  }

  // ─── Selection ───

  toggleSelectAll(): void {
    this.customers.forEach(c => {
      c.selected = this.allSelected;
      if (this.allSelected) this._selectedMap.set(c.id, c.phoneNumber);
      else this._selectedMap.delete(c.id);
    });
  }

  onRowSelect(customer: Customer): void {
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

  // ─── Dialog Callbacks (refresh data after child operations) ───

  onCustomerSaved(): void {
    this.loadCustomers();
    this.loadCounts();
  }

  onCustomerDeleted(): void {
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
    this.notification.success('Broadcast sent to selected customers!');
  }

  // ─── Subscription Toggle (inline, not a dialog) ───

  toggleSubscription(customer: Customer): void {
    const newValue = !customer.isSubscribed;
    this.customerService.toggleSubscription(customer.id, newValue).subscribe({
      next: () => {
        customer.isSubscribed = newValue;
        this.notification.success(`Subscription ${newValue ? 'enabled' : 'disabled'}.`);
        this.loadCounts();
        this.cdr.markForCheck();
      },
      error: () => {
        // Toast shown by error interceptor
      },
    });
  }
}
