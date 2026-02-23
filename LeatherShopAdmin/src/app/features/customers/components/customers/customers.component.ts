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
import { TableModule } from 'primeng/table';
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

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent, TableModule, ButtonModule, InputTextModule, TagModule, CardModule, CheckboxModule, DialogModule, DropdownModule, InputTextareaModule, ToolbarModule, DividerModule, BadgeModule, MessageModule],
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

  // Add customer dialog
  showAddDialog = false;
  newCustomer: CreateCustomer = { phoneNumber: '', name: '' };
  addingCustomer = false;

  // Bulk import dialog
  showImportDialog = false;
  importText = '';
  importing = false;

  // Selection
  allSelected = false;

  // Broadcast from selection
  showBroadcastDialog = false;
  broadcastTemplate = '';
  broadcastLang = '';
  broadcastParams = '';
  broadcastImageUrl = '';
  sendingBroadcast = false;

  // Templates
  templates: WhatsAppTemplate[] = [];
  templateOptions: any[] = [];
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
        this.templateOptions = data.map(t => ({ label: `${t.name} (${t.language}) - ${t.category}`, value: t.name }));
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
    if (selected) { this.broadcastLang = selected.language; }
    else { this.broadcastLang = 'en_US'; }
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

  onFilterChange(): void { this.loadCustomers(); }
  onSearch(): void { this.loadCustomers(); }
  clearSearch(): void { this.searchTerm = ''; this.loadCustomers(); }

  get selectedCount(): number { return this.customers.filter(c => c.selected).length; }
  get selectedCustomers(): Customer[] { return this.customers.filter(c => c.selected); }

  toggleSelectAll(): void {
    this.allSelected = !this.allSelected;
    this.customers.forEach(c => c.selected = this.allSelected);
  }

  onRowSelect(): void {
    this.allSelected = this.customers.length > 0 && this.customers.every(c => c.selected);
  }

  openAddDialog(): void {
    this.showAddDialog = true;
    this.newCustomer = { phoneNumber: '', name: '' };
  }

  addCustomer(): void {
    if (!this.newCustomer.phoneNumber.trim()) {
      this.notification.error('Phone number is required');
      return;
    }
    this.addingCustomer = true;
    this.customerService.createCustomer(this.newCustomer).subscribe({
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

  openImportDialog(): void {
    this.showImportDialog = true;
    this.importText = '';
  }

  importCustomers(): void {
    const lines = this.importText.trim().split('\n').filter(l => l.trim());
    if (lines.length === 0) {
      this.notification.error('Paste at least one phone number');
      return;
    }
    const customers: CreateCustomer[] = lines.map(line => {
      const parts = line.split(',').map(p => p.trim());
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
    this.broadcastTemplate = '';
    this.broadcastParams = '';
    this.broadcastImageUrl = '';
  }

  sendToSelected(): void {
    if (!this.isValidBroadcastTemplate) {
      this.notification.error('Please select a valid approved template!');
      return;
    }
    const phoneNumbers = this.selectedCustomers.map(c => c.phoneNumber);
    if (phoneNumbers.length === 0) {
      this.notification.error('No customers selected!');
      return;
    }
    const params = this.broadcastParams.trim()
      ? this.broadcastParams.split(',').map(p => p.trim()) : [];
    this.sendingBroadcast = true;
    this.broadcastService.sendBroadcast({
      templateName: this.broadcastTemplate, languageCode: this.broadcastLang,
      parameters: params, imageUrl: this.broadcastImageUrl || undefined,
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
}