import { Component, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { PaginatorState } from 'primeng/paginator';
import { CustomerService } from '../../services/customer.service';
import { Customer, CreateCustomer, UpdateCustomer } from '../../models/customer.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { CustomerBroadcastDialogComponent } from '../customer-broadcast-dialog/customer-broadcast-dialog.component';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ToolbarModule } from 'primeng/toolbar';
import { DividerModule } from 'primeng/divider';
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
    CustomerBroadcastDialogComponent,
    TableModule,
    ButtonModule,
    InputTextModule,
    TagModule,
    CardModule,
    CheckboxModule,
    DialogModule,
    InputTextareaModule,
    ToolbarModule,
    DividerModule,
    BadgeModule,
    MessageModule,
    TooltipModule,
    PaginatorModule,
  ],
  templateUrl: './customers.component.html',
  styleUrl: './customers.component.scss',
})
export class CustomersComponent implements OnInit {
  private fb = inject(FormBuilder);
  private customerService = inject(CustomerService);
  private notification = inject(NotificationService);

  customers: Customer[] = [];
  loading = true;
  subscriberCount: number | null = 0;
  totalCount: number | null = 0;

  // Reactive forms
  addCustomerForm!: FormGroup;
  importForm!: FormGroup;
  filterForm!: FormGroup;

  // Add customer dialog
  showAddDialog = false;
  addingCustomer = false;
  addSubmitted = false;

  // Edit customer dialog
  showEditDialog = false;
  editingCustomer = false;
  editSubmitted = false;
  editCustomerForm!: FormGroup;
  editingCustomerId: number | null = null;

  // Delete confirmation
  showDeleteConfirm = false;
  deletingCustomer = false;
  customerToDelete: Customer | null = null;

  // Bulk import dialog
  showImportDialog = false;
  importing = false;

  // Selection — tracked by ID→phone map so selections survive page changes
  allSelected = false;
  private _selectedMap = new Map<number, string>();

  // Pagination
  totalRecords = 0;
  currentPage = 1;
  pageSize = 25;

  // Broadcast from selection
  showBroadcastDialog = false;

  ngOnInit(): void {
    this.initForms();
    this.loadCustomers();
    this.loadCounts();
  }

  private initForms(): void {
    this.addCustomerForm = this.fb.group({
      phoneNumber: ['', [Validators.required, Validators.pattern(/^\d{10,15}$/)]],
      name: [''],
      address: ['', [Validators.required, Validators.minLength(10)]],
    });

    this.editCustomerForm = this.fb.group({
      name: [''],
      address: ['', [Validators.required, Validators.minLength(10)]],
      isSubscribed: [true],
    });

    this.importForm = this.fb.group({
      importText: ['', [Validators.required]],
    });

    this.filterForm = this.fb.group({
      searchTerm: [''],
      subscribedOnly: [false],
    });
  }

  get selectedPhoneNumbers(): string[] {
    return Array.from(this._selectedMap.values());
  }
  loadCounts(): void {
    this.customerService.getSubscriberCount().subscribe({
      next: data => {
        this.subscriberCount = data.subscriberCount;
        this.totalCount = data.totalCount;
      },
      error: () => {
        // Show N/A state instead of misleading zeros
        this.subscriberCount = null;
        this.totalCount = null;
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
        },
        error: () => (this.loading = false),
      });
  }

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

  get searchTerm(): string {
    return this.filterForm.get('searchTerm')?.value || '';
  }

  get selectedCount(): number {
    return this._selectedMap.size;
  }

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

  openAddDialog(): void {
    this.showAddDialog = true;
    this.addSubmitted = false;
    this.addCustomerForm.reset({ phoneNumber: '', name: '', address: '' });
  }

  addCustomer(): void {
    this.addSubmitted = true;
    this.addCustomerForm.markAllAsTouched();

    if (this.addCustomerForm.invalid) {
      this.notification.error('Phone number is required (10-15 digits)');
      return;
    }

    this.addingCustomer = true;
    const formValue = this.addCustomerForm.value;
    this.customerService.createCustomer(formValue).subscribe({
      next: () => {
        this.addingCustomer = false;
        this.showAddDialog = false;
        this.notification.success('Customer added successfully!');
        this.loadCustomers();
        this.loadCounts();
      },
      error: () => {
        // Toast shown by error interceptor (includes API message for duplicates, etc.)
        this.addingCustomer = false;
      },
    });
  }

  isAddFieldInvalid(field: string): boolean {
    const control = this.addCustomerForm.get(field);
    return !!(control && control.invalid && (control.dirty || control.touched || this.addSubmitted));
  }

  // ---- EDIT CUSTOMER ----

  openEditDialog(customer: Customer): void {
    this.editingCustomerId = customer.id;
    this.showEditDialog = true;
    this.editSubmitted = false;
    this.editCustomerForm.reset({
      name: customer.name || '',
      address: customer.address || '',
      isSubscribed: customer.isSubscribed,
    });
  }

  editCustomer(): void {
    this.editSubmitted = true;
    this.editCustomerForm.markAllAsTouched();

    if (this.editCustomerForm.invalid || !this.editingCustomerId) {
      this.notification.error('Please fill in all required fields.');
      return;
    }

    this.editingCustomer = true;
    const dto: UpdateCustomer = this.editCustomerForm.value;
    this.customerService.updateCustomer(this.editingCustomerId, dto).subscribe({
      next: () => {
        this.editingCustomer = false;
        this.showEditDialog = false;
        this.notification.success('Customer updated successfully!');
        this.loadCustomers();
        this.loadCounts();
      },
      error: () => {
        // Toast shown by error interceptor (includes API message)
        this.editingCustomer = false;
      },
    });
  }

  isEditFieldInvalid(field: string): boolean {
    const control = this.editCustomerForm.get(field);
    return !!(control && control.invalid && (control.dirty || control.touched || this.editSubmitted));
  }

  // ---- DELETE CUSTOMER ----

  confirmDelete(customer: Customer): void {
    this.customerToDelete = customer;
    this.showDeleteConfirm = true;
  }

  deleteCustomer(): void {
    if (!this.customerToDelete) return;
    this.deletingCustomer = true;
    this.customerService.deleteCustomer(this.customerToDelete.id).subscribe({
      next: () => {
        this.deletingCustomer = false;
        this.showDeleteConfirm = false;
        this.customerToDelete = null;
        this.notification.success('Customer deleted successfully!');
        this.loadCustomers();
        this.loadCounts();
      },
      error: () => {
        // Toast already shown by error interceptor (uses API message for 409, etc.)
        this.deletingCustomer = false;
        this.showDeleteConfirm = false;
        this.customerToDelete = null;
      },
    });
  }

  openImportDialog(): void {
    this.showImportDialog = true;
    this.importForm.reset({ importText: '' });
  }

  importCustomers(): void {
    if (this.importForm.invalid) {
      this.notification.error('Paste at least one phone number');
      return;
    }

    const importText = this.importForm.get('importText')?.value || '';
    const lines = importText
      .trim()
      .split('\n')
      .filter((l: string) => l.trim());
    if (lines.length === 0) {
      this.notification.error('Paste at least one phone number');
      return;
    }

    const phonePattern = /^\d{10,15}$/;
    const validCustomers: CreateCustomer[] = [];
    const invalidLines: number[] = [];

    lines.forEach((line: string, index: number) => {
      const parts = line.split(',').map((p: string) => p.trim());
      const phone = parts[0];
      if (phonePattern.test(phone)) {
        validCustomers.push({ phoneNumber: phone, name: parts[1] || '' });
      } else {
        invalidLines.push(index + 1);
      }
    });

    if (validCustomers.length === 0) {
      this.notification.error(`All ${lines.length} line(s) have invalid phone numbers. Phone must be 10-15 digits.`);
      return;
    }

    if (invalidLines.length > 0) {
      const lineNums =
        invalidLines.length <= 5
          ? invalidLines.join(', ')
          : invalidLines.slice(0, 5).join(', ') + `, ... (${invalidLines.length} total)`;
      this.notification.warning(
        `Skipping ${invalidLines.length} line(s) with invalid phone numbers (line ${lineNums}). Importing ${validCustomers.length} valid entries.`,
      );
    }

    this.importing = true;
    this.customerService.bulkImportCustomers(validCustomers).subscribe({
      next: res => {
        this.notification.success(`Imported ${res.imported} customers (${res.skippedDuplicates} duplicates skipped)`);
        this.importing = false;
        this.showImportDialog = false;
        this.loadCustomers();
        this.loadCounts();
      },
      error: () => {
        // Toast shown by error interceptor
        this.importing = false;
      },
    });
  }

  toggleSubscription(customer: Customer): void {
    const newValue = !customer.isSubscribed;
    this.customerService.toggleSubscription(customer.id, newValue).subscribe({
      next: () => {
        customer.isSubscribed = newValue;
        this.notification.success(`Subscription ${newValue ? 'enabled' : 'disabled'}.`);
        this.loadCounts();
      },
      error: () => {
        // Toast shown by error interceptor
      },
    });
  }

  openBroadcastDialog(): void {
    this.showBroadcastDialog = true;
  }

  onBroadcastSent(): void {
    this.showBroadcastDialog = false;
    this.notification.success('Broadcast sent to selected customers!');
  }
}
