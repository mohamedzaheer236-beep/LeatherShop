import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CustomerService } from '../../services/customer.service';
import { Customer, CreateCustomer } from '../../models/customer.model';
import { BroadcastService } from '../../../broadcast/services/broadcast.service';
import { WhatsAppTemplate } from '../../../broadcast/models/broadcast.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent],
  templateUrl: './customers.component.html',
  styleUrl: './customers.component.scss'
})
export class CustomersComponent implements OnInit {
  customers: Customer[] = [];
  filteredCustomers: Customer[] = [];
  loading = true;
  subscribedOnly = false;
  subscriberCount = 0;
  totalCount = 0;
  searchTerm = '';

  // Add customer form
  showAddForm = false;
  newCustomer: CreateCustomer = { phoneNumber: '', name: '' };
  addMessage = '';
  addMessageType: 'success' | 'error' | '' = '';
  addingCustomer = false;

  // Bulk import
  showImportForm = false;
  importText = '';
  importMessage = '';
  importMessageType: 'success' | 'error' | '' = '';
  importing = false;

  // Selection
  allSelected = false;

  // Broadcast from selection
  showBroadcastPanel = false;
  broadcastTemplate = '';
  broadcastLang = '';
  broadcastParams = '';
  broadcastImageUrl = '';
  sendingBroadcast = false;
  broadcastMessage = '';
  broadcastMessageType: 'success' | 'error' | '' = '';

  // Templates
  templates: WhatsAppTemplate[] = [];
  templatesLoaded = false;

  constructor(
    private customerService: CustomerService,
    private broadcastService: BroadcastService,
    private router: Router,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadCustomers();
    this.loadCounts();
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.broadcastService.getApprovedTemplates().subscribe({
      next: (data) => {
        this.templates = data;
        this.templatesLoaded = true;
      },
      error: () => this.templatesLoaded = true
    });
  }

  get isValidBroadcastTemplate(): boolean {
    if (!this.broadcastTemplate.trim()) return false;
    if (this.templatesLoaded && this.templates.length > 0) {
      return this.templates.some(t => t.name === this.broadcastTemplate);
    }
    return true;
  }

  onBroadcastTemplateSelect(): void {
    const selected = this.templates.find(t => t.name === this.broadcastTemplate);
    if (selected) {
      this.broadcastLang = selected.language;
    } else {
      this.broadcastLang = 'en_US';
    }
  }

  loadCounts(): void {
    this.customerService.getSubscriberCount().subscribe(data => {
      this.subscriberCount = data.subscriberCount;
      this.totalCount = data.totalCount;
    });
  }

  loadCustomers(): void {
    this.loading = true;
    this.customerService.getCustomers(this.subscribedOnly, this.searchTerm || undefined).subscribe({
      next: (data) => {
        this.customers = data.map(c => ({ ...c, selected: false }));
        this.filteredCustomers = this.customers;
        this.allSelected = false;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  onFilterChange(): void {
    this.loadCustomers();
  }

  onSearch(): void {
    this.loadCustomers();
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.loadCustomers();
  }

  // --- Selection ---
  get selectedCount(): number {
    return this.customers.filter(c => c.selected).length;
  }

  get selectedCustomers(): Customer[] {
    return this.customers.filter(c => c.selected);
  }

  toggleSelectAll(): void {
    this.allSelected = !this.allSelected;
    this.customers.forEach(c => c.selected = this.allSelected);
  }

  onRowSelect(): void {
    this.allSelected = this.customers.length > 0 && this.customers.every(c => c.selected);
  }

  // --- Add Customer ---
  openAddForm(): void {
    this.showAddForm = true;
    this.showImportForm = false;
    this.newCustomer = { phoneNumber: '', name: '' };
    this.addMessage = '';
  }

  closeAddForm(): void {
    this.showAddForm = false;
    this.addMessage = '';
  }

  addCustomer(): void {
    if (!this.newCustomer.phoneNumber.trim()) {
      this.addMessage = 'Phone number is required';
      this.addMessageType = 'error';
      return;
    }
    this.addingCustomer = true;
    this.customerService.createCustomer(this.newCustomer).subscribe({
      next: () => {
        this.addMessage = 'Customer added successfully!';
        this.addMessageType = 'success';
        this.addingCustomer = false;
        this.newCustomer = { phoneNumber: '', name: '' };
        this.notification.success('Customer added successfully!');
        this.loadCustomers();
        this.loadCounts();
      },
      error: (err: any) => {
        this.addMessage = err.error?.message || err.error || 'Failed to add customer';
        this.addMessageType = 'error';
        this.addingCustomer = false;
      }
    });
  }

  // --- Bulk Import ---
  openImportForm(): void {
    this.showImportForm = true;
    this.showAddForm = false;
    this.importText = '';
    this.importMessage = '';
  }

  closeImportForm(): void {
    this.showImportForm = false;
    this.importMessage = '';
  }

  importCustomers(): void {
    const lines = this.importText.trim().split('\n').filter(l => l.trim());
    if (lines.length === 0) {
      this.importMessage = 'Paste at least one phone number';
      this.importMessageType = 'error';
      return;
    }

    const customers: CreateCustomer[] = lines.map(line => {
      const parts = line.split(',').map(p => p.trim());
      return { phoneNumber: parts[0], name: parts[1] || '' };
    });

    this.importing = true;
    this.customerService.bulkImportCustomers(customers).subscribe({
      next: (res: any) => {
        this.importMessage = `Imported ${res.imported} customers (${res.skippedDuplicates} duplicates skipped)`;
        this.importMessageType = 'success';
        this.importing = false;
        this.loadCustomers();
        this.loadCounts();
      },
      error: () => {
        this.importMessage = 'Import failed. Please check your data format.';
        this.importMessageType = 'error';
        this.importing = false;
      }
    });
  }

  // --- Toggle Subscription ---
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

  // --- Broadcast to Selected ---
  openBroadcastPanel(): void {
    this.showBroadcastPanel = true;
    this.broadcastTemplate = '';
    this.broadcastParams = '';
    this.broadcastImageUrl = '';
    this.broadcastMessage = '';
  }

  closeBroadcastPanel(): void {
    this.showBroadcastPanel = false;
    this.broadcastMessage = '';
  }

  sendToSelected(): void {
    if (!this.isValidBroadcastTemplate) {
      this.broadcastMessage = 'Please select a valid approved template!';
      this.broadcastMessageType = 'error';
      return;
    }

    const phoneNumbers = this.selectedCustomers.map(c => c.phoneNumber);
    if (phoneNumbers.length === 0) {
      this.broadcastMessage = 'No customers selected!';
      this.broadcastMessageType = 'error';
      return;
    }

    const params = this.broadcastParams.trim()
      ? this.broadcastParams.split(',').map(p => p.trim())
      : [];

    this.sendingBroadcast = true;
    this.broadcastService.sendBroadcast({
      templateName: this.broadcastTemplate,
      languageCode: this.broadcastLang,
      parameters: params,
      imageUrl: this.broadcastImageUrl || undefined,
      phoneNumbers: phoneNumbers
    }).subscribe({
      next: (res: any) => {
        this.sendingBroadcast = false;
        this.broadcastMessage = `Broadcast sent to ${res.totalRecipients} selected customers!`;
        this.broadcastMessageType = 'success';
      },
      error: () => {
        this.sendingBroadcast = false;
        this.broadcastMessage = 'Failed to send broadcast.';
        this.broadcastMessageType = 'error';
      }
    });
  }
}
