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
import { PaginatorState } from 'primeng/paginator';
import { CustomerService } from '../../services/customer.service';
import { Customer, CreateCustomer, UpdateCustomer } from '../../models/customer.model';
import { BroadcastService } from '../../../broadcast/services/broadcast.service';
import { CarouselCard, CarouselCardUI } from '../../../broadcast/models/broadcast.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TemplateLoaderService } from '../../../../shared/services/template-loader.service';
import { ProductService } from '../../../products/services/product.service';
import { Product, ProductImageItem } from '../../../products/models/product.model';
import { environment } from '../../../../../environments/environment';
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
import { PaginatorModule } from 'primeng/paginator';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LoadingSpinnerComponent, TableModule, ButtonModule, InputTextModule, TagModule, CardModule, CheckboxModule, DialogModule, DropdownModule, InputTextareaModule, ToolbarModule, DividerModule, BadgeModule, MessageModule, TooltipModule, PaginatorModule],
  templateUrl: './customers.component.html',
  styleUrl: './customers.component.scss'
})
export class CustomersComponent implements OnInit {
  customers: Customer[] = [];
  loading = true;
  subscriberCount: number | null = 0;
  totalCount: number | null = 0;

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

  // Selection — tracked by ID→phone map so selections survive page changes
  allSelected = false;
  private _selectedMap = new Map<number, string>();

  // Pagination
  totalRecords = 0;
  currentPage = 1;
  pageSize = 25;

  // Broadcast from selection
  showBroadcastDialog = false;
  broadcastLang = '';
  sendingBroadcast = false;
  broadcastSubmitted = false;

  // Carousel + smart field state for broadcast dialog
  bcIsCarousel = false;
  bcCarouselCards: CarouselCardUI[] = [];
  bcHasImageHeader = false;
  bcBodyParamCount = 0;
  bcCardBodyMaxLength = 120;
  bcHeaderImagePreview: string | null = null;
  bcHeaderImageUploading = false;

  // Products for carousel card picker
  products: Product[] = [];
  productOptions: { label: string; value: number }[] = [];

  constructor(
    private fb: FormBuilder,
    private customerService: CustomerService,
    private broadcastService: BroadcastService,
    private notification: NotificationService,
    public templateLoader: TemplateLoaderService,
    private productService: ProductService
  ) {}

  ngOnInit(): void {
    this.initForms();
    this.loadCustomers();
    this.loadCounts();
    this.templateLoader.loadTemplates();
    this.loadProducts();
  }

  private loadProducts(): void {
    this.productService.getProducts(undefined, undefined, undefined, 1, 100).subscribe({
      next: (result) => {
        this.products = result.items.filter(p => p.isActive);
        this.productOptions = this.products.map(p => ({
          label: `${p.name} — ₹${p.price}`,
          value: p.id
        }));
      },
      error: () => {}
    });
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

    const tpl = name ? this.templateLoader.getTemplate(name) : undefined;
    this.bcHasImageHeader = tpl?.hasImageHeader ?? false;
    this.bcBodyParamCount = tpl?.bodyParamCount ?? 0;
    this.bcCardBodyMaxLength = tpl?.cardBodyMaxLength && tpl.cardBodyMaxLength > 0 ? tpl.cardBodyMaxLength : 120;

    if (!this.bcHasImageHeader) {
      this.bcHeaderImagePreview = null;
      this.broadcastForm.patchValue({ broadcastImageUrl: '' });
    }

    if (tpl?.isCarousel) {
      this.bcIsCarousel = true;
      this.bcCarouselCards = Array.from({ length: tpl.cardCount }, () => ({
        imageUrl: '', imagePreview: null, bodyParam: '', buttonPayload: '',
        selectedProductId: null, selectedImageId: null, uploading: false
      }));
    } else {
      this.bcIsCarousel = false;
      this.bcCarouselCards = [];
    }
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
        this.subscriberCount = null;
        this.totalCount = null;
      }
    });
  }

  loadCustomers(): void {
    this.loading = true;
    const { searchTerm, subscribedOnly } = this.filterForm.value;
    this.customerService.getCustomers(subscribedOnly, searchTerm || undefined, this.currentPage, this.pageSize).subscribe({
      next: (result) => {
        this.customers = result.items.map(c => ({ ...c, selected: this._selectedMap.has(c.id) }));
        this.totalRecords = result.totalCount;
        this.allSelected = this.customers.length > 0 && this.customers.every(c => c.selected);
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  onFilterChange(): void { this.currentPage = 1; this.loadCustomers(); }
  onSearch(): void { this.currentPage = 1; this.loadCustomers(); }
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

  get searchTerm(): string { return this.filterForm.get('searchTerm')?.value || ''; }

  get selectedCount(): number { return this._selectedMap.size; }

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
    this.customers.forEach(c => c.selected = false);
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
        this.loadCustomers(); this.loadCounts();
      },
      error: () => {
        // Toast shown by error interceptor (includes API message for duplicates, etc.)
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
      error: () => {
        // Toast shown by error interceptor (includes API message)
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
        // Toast already shown by error interceptor (uses API message for 409, etc.)
        this.deletingCustomer = false;
        this.showDeleteConfirm = false;
        this.customerToDelete = null;
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
      const lineNums = invalidLines.length <= 5
        ? invalidLines.join(', ')
        : invalidLines.slice(0, 5).join(', ') + `, ... (${invalidLines.length} total)`;
      this.notification.warning(`Skipping ${invalidLines.length} line(s) with invalid phone numbers (line ${lineNums}). Importing ${validCustomers.length} valid entries.`);
    }

    this.importing = true;
    this.customerService.bulkImportCustomers(validCustomers).subscribe({
      next: (res) => {
        this.notification.success(`Imported ${res.imported} customers (${res.skippedDuplicates} duplicates skipped)`);
        this.importing = false;
        this.showImportDialog = false;
        this.loadCustomers(); this.loadCounts();
      },
      error: () => {
        // Toast shown by error interceptor
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
      error: () => {
        // Toast shown by error interceptor
      }
    });
  }

  openBroadcastDialog(): void {
    this.showBroadcastDialog = true;
    this.broadcastSubmitted = false;
    this.broadcastForm.reset({ broadcastTemplate: '', broadcastParams: '', broadcastImageUrl: '' });
    this.bcIsCarousel = false;
    this.bcCarouselCards = [];
    this.bcHasImageHeader = false;
    this.bcBodyParamCount = 0;
    this.bcHeaderImagePreview = null;
  }

  sendToSelected(): void {
    this.broadcastSubmitted = true;
    this.broadcastForm.markAllAsTouched();

    if (this.broadcastForm.invalid) {
      this.notification.error('Please select a valid approved template!');
      return;
    }

    if (this.bcIsCarousel && !this.bcCarouselCards.every(c => c.imageUrl.trim() !== '')) {
      this.notification.error('Please select images for all carousel cards.');
      return;
    }

    const phoneNumbers = Array.from(this._selectedMap.values());
    if (phoneNumbers.length === 0) {
      this.notification.error('No customers selected!');
      return;
    }

    const { broadcastTemplate, broadcastParams, broadcastImageUrl } = this.broadcastForm.value;

    this.sendingBroadcast = true;

    if (this.bcIsCarousel) {
      const cards: CarouselCard[] = this.bcCarouselCards.map(c => ({
        imageUrl: c.imageUrl,
        bodyParam: c.bodyParam,
        buttonPayload: c.buttonPayload
      }));
      this.broadcastService.sendBroadcast({
        templateName: broadcastTemplate, languageCode: this.broadcastLang,
        parameters: [], isCarousel: true, carouselCards: cards,
        phoneNumbers: phoneNumbers
      }).subscribe({
        next: (res) => {
          this.sendingBroadcast = false;
          this.showBroadcastDialog = false;
          this.notification.success(`Carousel sending to ${res.totalRecipients} selected customers...`);
        },
        error: () => { this.sendingBroadcast = false; }
      });
    } else {
      const params = broadcastParams && broadcastParams.trim()
        ? broadcastParams.split(',').map((p: string) => p.trim()) : [];
      this.broadcastService.sendBroadcast({
        templateName: broadcastTemplate, languageCode: this.broadcastLang,
        parameters: params, imageUrl: broadcastImageUrl || undefined,
        phoneNumbers: phoneNumbers
      }).subscribe({
        next: (res) => {
          this.sendingBroadcast = false;
          this.showBroadcastDialog = false;
          this.notification.success(`Broadcast sending to ${res.totalRecipients} selected customers...`);
        },
        error: () => { this.sendingBroadcast = false; }
      });
    }
  }

  // ─── Broadcast dialog helpers ───

  resolveImageUrl(url: string): string {
    if (!url) return '';
    return url.startsWith('http') ? url : environment.baseUrl + url;
  }

  get bcAnyUploading(): boolean {
    return this.bcHeaderImageUploading || this.bcCarouselCards.some(c => c.uploading);
  }

  get bcCarouselCardsValid(): boolean {
    if (!this.bcIsCarousel) return true;
    return this.bcCarouselCards.every(c => c.imageUrl.trim() !== '');
  }

  onBcHeaderImageSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];
    if (!file.type.startsWith('image/')) { this.notification.error('Please select an image file.'); return; }
    if (file.size > 5 * 1024 * 1024) { this.notification.error('Image must be under 5 MB.'); return; }
    const reader = new FileReader();
    reader.onload = () => { this.bcHeaderImagePreview = reader.result as string; };
    reader.readAsDataURL(file);
    this.bcHeaderImageUploading = true;
    this.broadcastService.uploadImage(file).subscribe({
      next: (path) => { this.broadcastForm.patchValue({ broadcastImageUrl: path }); this.bcHeaderImageUploading = false; },
      error: () => { this.bcHeaderImagePreview = null; this.bcHeaderImageUploading = false; this.notification.error('Image upload failed.'); }
    });
    input.value = '';
  }

  removeBcHeaderImage(): void {
    this.bcHeaderImagePreview = null;
    this.broadcastForm.patchValue({ broadcastImageUrl: '' });
  }

  onBcCardImageSelect(event: Event, index: number): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];
    if (!file.type.startsWith('image/')) { this.notification.error('Please select an image file.'); return; }
    if (file.size > 5 * 1024 * 1024) { this.notification.error('Image must be under 5 MB.'); return; }
    const card = this.bcCarouselCards[index];
    const reader = new FileReader();
    reader.onload = () => { card.imagePreview = reader.result as string; };
    reader.readAsDataURL(file);
    card.uploading = true;
    this.broadcastService.uploadImage(file).subscribe({
      next: (path) => { card.imageUrl = path; card.uploading = false; },
      error: () => { card.imagePreview = null; card.uploading = false; this.notification.error(`Card ${index + 1} image upload failed.`); }
    });
    input.value = '';
  }

  onBcCardProductSelect(index: number): void {
    const card = this.bcCarouselCards[index];
    if (card.selectedProductId) {
      const product = this.products.find(p => p.id === card.selectedProductId);
      if (!card.bodyParam.trim() && product) {
        card.bodyParam = product.name.substring(0, this.bcCardBodyMaxLength);
      }
      if (product?.imageItems?.length) {
        this.selectBcCardImage(index, product.imageItems[0]);
      } else {
        card.buttonPayload = `view_${card.selectedProductId}`;
        card.selectedImageId = null; card.imageUrl = ''; card.imagePreview = null;
      }
    } else {
      card.buttonPayload = ''; card.selectedImageId = null; card.imageUrl = ''; card.imagePreview = null;
    }
  }

  selectBcCardImage(index: number, img: ProductImageItem): void {
    const card = this.bcCarouselCards[index];
    card.selectedImageId = img.id;
    card.imageUrl = img.url;
    card.imagePreview = this.resolveImageUrl(img.url);
    if (card.selectedProductId) {
      card.buttonPayload = img.id > 0 ? `view_${card.selectedProductId}_pi${img.id}` : `view_${card.selectedProductId}`;
    }
  }

  removeBcCardImage(index: number): void {
    const card = this.bcCarouselCards[index];
    card.imageUrl = '';
    card.imagePreview = null;
  }

  getBcCardProductImages(index: number): ProductImageItem[] {
    const card = this.bcCarouselCards[index];
    if (!card.selectedProductId) return [];
    const product = this.products.find(p => p.id === card.selectedProductId);
    return product?.imageItems ?? [];
  }

  isBroadcastFieldInvalid(field: string): boolean {
    const control = this.broadcastForm.get(field);
    return !!(control && control.invalid && (control.dirty || control.touched || this.broadcastSubmitted));
  }
}