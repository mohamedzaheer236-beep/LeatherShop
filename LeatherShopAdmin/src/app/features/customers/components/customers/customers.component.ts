import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors
} from '@angular/forms';
import { CustomerService } from '../../services/customer.service';
import { Customer, CreateCustomer, UpdateCustomer } from '../../models/customer.model';
import { BroadcastService } from '../../../broadcast/services/broadcast.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TemplateLoaderService } from '../../../../shared/services/template-loader.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ToolbarModule } from 'primeng/toolbar';
import { DividerModule } from 'primeng/divider';
import { BadgeModule } from 'primeng/badge';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LoadingSpinnerComponent, TableModule, ButtonModule, InputTextModule, TagModule, CardModule, CheckboxModule, DialogModule, DropdownModule, InputTextareaModule, ToolbarModule, DividerModule, BadgeModule, MessageModule, TooltipModule],
  templateUrl: './customers.component.html',
  styleUrl: './customers.component.scss'
})
export class CustomersComponent implements OnInit {
  customers: Customer[] = [];
  loading = true;
  subscriberCount = 0;
  totalCount = 0;

  // Reactive forms
  addCustomerForm!: FormGroup;
  importForm!: FormGroup;
  broadcastForm!: FormGroup;
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

  // Selection — kept with ngModel for dynamic row binding
  allSelected = false;
  private _selectedCount = 0;

  // Broadcast from selection
  showBroadcastDialog = false;
  broadcastLang = '';
  sendingBroadcast = false;
  broadcastSubmitted = false;

  constructor(
    private fb: FormBuilder,
    private customerService: CustomerService,
    private broadcastService: BroadcastService,
    private notification: NotificationService,
    public templateLoader: TemplateLoaderService
  ) {}

  ngOnInit(): void {
    this.initForms();
    this.loadCustomers();
    this.loadCounts();
    this.templateLoader.loadTemplates();
  }

  private initForms(): void {
    this.addCustomerForm = this.fb.group({
      phoneNumber: ['', [Validators.required, Validators.pattern(/^\d{10,15}$/)]],
      name: [''],
      address: ['', [Validators.required, Validators.minLength(10)]]
    });

    this.editCustomerForm = this.fb.group({
      name: [''],
      address: ['', [Validators.required, Validators.minLength(10)]],
      isSubscribed: [true]
    });

    this.importForm = this.fb.group({
      importText: ['', [Validators.required]]
    });

    this.broadcastForm = this.fb.group({
      broadcastTemplate: ['', [Validators.required, this.broadcastTemplateValidator.bind(this)]],
      broadcastParams: [''],
      broadcastImageUrl: ['']
    });

    this.filterForm = this.fb.group({
      searchTerm: [''],
      subscribedOnly: [false]
    });
  }

  /** Custom validator for broadcast template */
  private broadcastTemplateValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null;
    if (!this.templateLoader.isValidTemplate(value)) {
      return { invalidTemplate: true };
    }
    return null;
  }

  get isValidBroadcastTemplate(): boolean {
    return this.templateLoader.isValidTemplate(this.broadcastForm.get('broadcastTemplate')?.value);
  }

  onBroadcastTemplateSelect(): void {
    const name = this.broadcastForm.get('broadcastTemplate')?.value;
    this.broadcastLang = this.templateLoader.getLanguageCode(name);
    this.broadcastForm.get('broadcastTemplate')?.updateValueAndValidity();
  }

  onBroadcastTemplateFilter(event: { originalEvent: Event; filter: string }): void {
    if (event.filter && event.filter.trim()) {
      this.broadcastForm.get('broadcastTemplate')?.markAsDirty();
      this.broadcastForm.get('broadcastTemplate')?.markAsTouched();
    }
  }

  loadCounts(): void {
    this.customerService.getSubscriberCount().subscribe({
      next: data => {
        this.subscriberCount = data.subscriberCount;
        this.totalCount = data.totalCount;
      },
      error: () => {
        // Show N/A state instead of misleading zeros
        this.subscriberCount = -1;
        this.totalCount = -1;
      }
    });
  }

  loadCustomers(): void {
    this.loading = true;
    const { searchTerm, subscribedOnly } = this.filterForm.value;
    this.customerService.getCustomers(subscribedOnly, searchTerm || undefined).subscribe({
      next: (data) => {
        this.customers = data.map(c => ({ ...c, selected: false }));
        this.allSelected = false;
        this._selectedCount = 0;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  onFilterChange(): void { this.loadCustomers(); }
  onSearch(): void { this.loadCustomers(); }
  clearSearch(): void {
    this.filterForm.patchValue({ searchTerm: '' });
    this.loadCustomers();
  }

  get searchTerm(): string { return this.filterForm.get('searchTerm')?.value || ''; }

  get selectedCount(): number { return this._selectedCount; }

  toggleSelectAll(): void {
    this.customers.forEach(c => c.selected = this.allSelected);
    this._selectedCount = this.allSelected ? this.customers.length : 0;
  }

  onRowSelect(customer: Customer): void {
    this._selectedCount += customer.selected ? 1 : -1;
    this.allSelected = this._selectedCount === this.customers.length;
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
        this.loadCustomers(); this.loadCounts();
      },
      error: (err: any) => {
        this.notification.error(err.error?.message || 'Failed to add customer');
        this.addingCustomer = false;
      }
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
      isSubscribed: customer.isSubscribed
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
        this.loadCustomers(); this.loadCounts();
      },
      error: (err: any) => {
        this.notification.error(err.error?.message || 'Failed to update customer');
        this.editingCustomer = false;
      }
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
        this.loadCustomers(); this.loadCounts();
      },
      error: () => {
        this.notification.error('Failed to delete customer.');
        this.deletingCustomer = false;
      }
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
    const lines = importText.trim().split('\n').filter((l: string) => l.trim());
    if (lines.length === 0) {
      this.notification.error('Paste at least one phone number');
      return;
    }
    const customers: CreateCustomer[] = lines.map((line: string) => {
      const parts = line.split(',').map((p: string) => p.trim());
      return { phoneNumber: parts[0], name: parts[1] || '' };
    });
    this.importing = true;
    this.customerService.bulkImportCustomers(customers).subscribe({
      next: (res: any) => {
        this.notification.success(`Imported ${res.imported} customers (${res.skippedDuplicates} duplicates skipped)`);
        this.importing = false;
        this.showImportDialog = false;
        this.loadCustomers(); this.loadCounts();
      },
      error: () => {
        this.notification.error('Import failed. Please check your data format.');
        this.importing = false;
      }
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
      error: () => this.notification.error('Failed to update subscription.')
    });
  }

  openBroadcastDialog(): void {
    this.showBroadcastDialog = true;
    this.broadcastSubmitted = false;
    this.broadcastForm.reset({ broadcastTemplate: '', broadcastParams: '', broadcastImageUrl: '' });
  }

  sendToSelected(): void {
    this.broadcastSubmitted = true;
    this.broadcastForm.markAllAsTouched();

    if (this.broadcastForm.invalid) {
      this.notification.error('Please select a valid approved template!');
      return;
    }

    const phoneNumbers = this.customers.filter(c => c.selected).map(c => c.phoneNumber);
    if (phoneNumbers.length === 0) {
      this.notification.error('No customers selected!');
      return;
    }

    const { broadcastTemplate, broadcastParams, broadcastImageUrl } = this.broadcastForm.value;
    const params = broadcastParams && broadcastParams.trim()
      ? broadcastParams.split(',').map((p: string) => p.trim()) : [];

    this.sendingBroadcast = true;
    this.broadcastService.sendBroadcast({
      templateName: broadcastTemplate, languageCode: this.broadcastLang,
      parameters: params, imageUrl: broadcastImageUrl || undefined,
      phoneNumbers: phoneNumbers
    }).subscribe({
      next: (res: any) => {
        this.sendingBroadcast = false;
        this.showBroadcastDialog = false;
        this.notification.success(`Broadcast sent to ${res.totalRecipients} selected customers!`);
      },
      error: () => {
        this.sendingBroadcast = false;
        this.notification.error('Failed to send broadcast.');
      }
    });
  }

  isBroadcastFieldInvalid(field: string): boolean {
    const control = this.broadcastForm.get(field);
    return !!(control && control.invalid && (control.dirty || control.touched || this.broadcastSubmitted));
  }
}