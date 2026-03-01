import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors
} from '@angular/forms';
import { BroadcastService } from '../../services/broadcast.service';
import { BroadcastHistory, CarouselCard } from '../../models/broadcast.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TemplateLoaderService } from '../../../../shared/services/template-loader.service';
import { ProductService } from '../../../products/services/product.service';
import { Product } from '../../../products/models/product.model';

import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { DividerModule } from 'primeng/divider';

interface CarouselCardUI {
  imageUrl: string;
  imagePreview: string | null;
  bodyParam: string;
  buttonPayload: string;
  selectedProductId: number | null;
  uploading: boolean;
}

@Component({
  selector: 'app-broadcast',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    CardModule,
    DropdownModule,
    InputTextModule,
    InputTextareaModule,
    ButtonModule,
    TableModule,
    TagModule,
    ToolbarModule,
    DividerModule
  ],
  templateUrl: './broadcast.component.html',
  styleUrl: './broadcast.component.scss'
})
export class BroadcastComponent implements OnInit, OnDestroy {
  broadcastForm!: FormGroup;
  sending = false;
  resultMessage = '';
  resultType: 'success' | 'error' | '' = '';
  submitted = false;

  history: BroadcastHistory[] = [];
  subscriberCount = 0;
  totalSent = 0;

  broadcastMode: 'custom' | 'template' = 'custom';
  customMessage = '';

  // Carousel template support
  selectedTemplateIsCarousel = false;
  carouselCards: CarouselCardUI[] = [];

  // Template metadata for field visibility
  selectedTemplateHasImageHeader = false;
  selectedTemplateBodyParamCount = 0;
  cardBodyMaxLength = 120; // dynamic: calculated from template's card body static text

  // Product list for carousel card "View Details" button
  products: Product[] = [];
  productOptions: { label: string; value: number }[] = [];

  // Header image upload (for standard templates)
  headerImagePreview: string | null = null;
  headerImageUploading = false;

  private pollingIntervals = new Map<number, ReturnType<typeof setInterval>>();

  constructor(
    private fb: FormBuilder,
    private broadcastService: BroadcastService,
    private notification: NotificationService,
    public templateLoader: TemplateLoaderService,
    private productService: ProductService
  ) {}

  ngOnInit(): void {
    this.initForm();
    this.loadHistory();
    this.templateLoader.loadTemplates();
    this.broadcastService.getSubscriberCount().subscribe({
      next: (data) => { this.subscriberCount = data.subscriberCount; },
      error: () => { /* Toast shown by error interceptor */ }
    });
    this.loadProducts();
  }

  private loadProducts(): void {
    this.productService.getProducts().subscribe({
      next: (products) => {
        this.products = products.filter(p => p.isActive);
        this.productOptions = this.products.map(p => ({
          label: `${p.name} — ₹${p.price}`,
          value: p.id
        }));
      },
      error: () => { /* Products dropdown will be empty — admin can still type payload manually */ }
    });
  }

  private initForm(): void {
    this.broadcastForm = this.fb.group({
      templateName: [null, [Validators.required, this.templateValidator.bind(this)]],
      parameters: [''],
      imageUrl: ['']
    });
  }

  /** Custom validator — checks if the selected template is approved */
  private templateValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null; // required validator handles empty
    if (!this.templateLoader.isValidTemplate(value)) {
      return { invalidTemplate: true };
    }
    return null;
  }

  get f() {
    return this.broadcastForm.controls;
  }

  onTemplateSelect(): void {
    this.f['templateName'].updateValueAndValidity();
    const templateName = this.f['templateName'].value;
    const tpl = templateName ? this.templateLoader.getTemplate(templateName) : undefined;

    // Set template metadata for field visibility
    this.selectedTemplateHasImageHeader = tpl?.hasImageHeader ?? false;
    this.selectedTemplateBodyParamCount = tpl?.bodyParamCount ?? 0;
    this.cardBodyMaxLength = tpl?.cardBodyMaxLength && tpl.cardBodyMaxLength > 0 ? tpl.cardBodyMaxLength : 120;

    // Clear image when switching to a template without header
    if (!this.selectedTemplateHasImageHeader) {
      this.headerImagePreview = null;
      this.broadcastForm.patchValue({ imageUrl: '' });
    }

    if (tpl?.isCarousel) {
      this.selectedTemplateIsCarousel = true;
      const cardCount = tpl.cardCount;
      this.carouselCards = Array.from({ length: cardCount }, () => ({
        imageUrl: '',
        imagePreview: null,
        bodyParam: '',
        buttonPayload: '',
        selectedProductId: null,
        uploading: false
      }));
    } else {
      this.selectedTemplateIsCarousel = false;
      this.carouselCards = [];
    }
  }

  /** Mark control touched when dropdown closes */
  onDropdownHide(): void {
    this.f['templateName'].markAsTouched();
  }

  get isValidTemplate(): boolean {
    return this.templateLoader.isValidTemplate(this.f['templateName'].value);
  }

  /** Helper: true when a field should show its error state */
  isFieldInvalid(field: string): boolean {
    const control = this.f[field];
    return control.invalid && (control.dirty || control.touched || this.submitted);
  }

  getResultSeverity(): 'success' | 'error' {
    return this.resultType === 'success' ? 'success' : 'error';
  }

  /** Check if any carousel card image is currently uploading */
  get anyUploading(): boolean {
    return this.headerImageUploading || this.carouselCards.some(c => c.uploading);
  }

  /** Check if carousel cards are fully filled (all cards have image + bodyParam) */
  get carouselCardsValid(): boolean {
    if (!this.selectedTemplateIsCarousel) return true;
    return this.carouselCards.every(c => c.imageUrl.trim() !== '');
  }

  // ─── Image Upload for Standard Templates ───

  onHeaderImageSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];
    if (!file.type.startsWith('image/')) {
      this.notification.error('Please select an image file.');
      return;
    }

    // Show preview immediately
    const reader = new FileReader();
    reader.onload = () => { this.headerImagePreview = reader.result as string; };
    reader.readAsDataURL(file);

    // Upload to server
    this.headerImageUploading = true;
    this.broadcastService.uploadImage(file).subscribe({
      next: (path) => {
        this.broadcastForm.patchValue({ imageUrl: path });
        this.headerImageUploading = false;
      },
      error: () => {
        this.headerImagePreview = null;
        this.headerImageUploading = false;
        this.notification.error('Image upload failed. Please try again.');
      }
    });
    // Reset input so same file can be selected again
    input.value = '';
  }

  removeHeaderImage(): void {
    this.headerImagePreview = null;
    this.broadcastForm.patchValue({ imageUrl: '' });
  }

  // ─── Image Upload for Carousel Cards ───

  onCardImageSelect(event: Event, index: number): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];
    if (!file.type.startsWith('image/')) {
      this.notification.error('Please select an image file.');
      return;
    }

    const card = this.carouselCards[index];

    // Show preview immediately
    const reader = new FileReader();
    reader.onload = () => { card.imagePreview = reader.result as string; };
    reader.readAsDataURL(file);

    // Upload to server
    card.uploading = true;
    this.broadcastService.uploadImage(file).subscribe({
      next: (path) => {
        card.imageUrl = path;
        card.uploading = false;
      },
      error: () => {
        card.imagePreview = null;
        card.uploading = false;
        this.notification.error(`Card ${index + 1} image upload failed.`);
      }
    });
    input.value = '';
  }

  removeCardImage(index: number): void {
    const card = this.carouselCards[index];
    card.imageUrl = '';
    card.imagePreview = null;
  }

  /** When admin selects a product for a carousel card, auto-generate the view_ payload */
  onCardProductSelect(index: number): void {
    const card = this.carouselCards[index];
    if (card.selectedProductId) {
      card.buttonPayload = `view_${card.selectedProductId}`;
      // Auto-fill body param with product name if empty
      if (!card.bodyParam.trim()) {
        const product = this.products.find(p => p.id === card.selectedProductId);
        if (product) {
          card.bodyParam = product.name.substring(0, 120);
        }
      }
    } else {
      card.buttonPayload = '';
    }
  }

  ngOnDestroy(): void {
    // Clear all active polling intervals
    this.pollingIntervals.forEach(interval => clearInterval(interval));
    this.pollingIntervals.clear();
  }

  loadHistory(): void {
    this.broadcastService.getBroadcastHistory().subscribe({
      next: (data) => {
        this.history = data;
        this.totalSent = data.reduce((sum, b) => sum + b.sentCount, 0);
      },
      error: () => { /* Toast shown by error interceptor */ }
    });
  }

  sendCustomMessage(): void {
    if (!this.customMessage.trim()) return;

    this.sending = true;
    this.resultMessage = '';

    this.broadcastService.sendBroadcast({
      templateName: 'shop_deals',
      languageCode: 'en',
      parameters: [this.customMessage.trim()]
    }).subscribe({
      next: (res) => {
        this.resultMessage = `Sending to ${res.totalRecipients} subscribers...`;
        this.resultType = 'success';
        this.customMessage = '';
        this.pollBroadcastStatus(res.broadcastId, res.totalRecipients);
      },
      error: () => {
        this.sending = false;
        this.resultMessage = 'Failed to send broadcast. Make sure the shop_deals template is approved.';
        this.resultType = 'error';
      }
    });
  }

  private pollBroadcastStatus(broadcastId: number, totalRecipients: number): void {
    // If already polling this broadcast, skip
    if (this.pollingIntervals.has(broadcastId)) return;

    let attempts = 0;
    const maxAttempts = 30;
    const interval = setInterval(() => {
      attempts++;
      this.broadcastService.getBroadcastStatus(broadcastId).subscribe({
        next: (status) => {
          const processed = status.sentCount + status.failedCount;
          if (processed >= totalRecipients || attempts >= maxAttempts) {
            clearInterval(interval);
            this.pollingIntervals.delete(broadcastId);
            this.sending = this.pollingIntervals.size > 0; // still sending if other polls active
            this.loadHistory();
            if (status.failedCount > 0 && status.sentCount === 0) {
              this.resultMessage = `Broadcast failed! ${status.failedCount} message(s) could not be delivered. Check if your template is approved.`;
              this.resultType = 'error';
              this.notification.error(`Broadcast failed for ${status.failedCount} recipient(s).`);
            } else if (status.failedCount > 0) {
              this.resultMessage = `Broadcast completed: ${status.sentCount} sent, ${status.failedCount} failed.`;
              this.resultType = 'success';
              this.notification.warning(`Broadcast: ${status.sentCount} sent, ${status.failedCount} failed.`);
            } else {
              this.resultMessage = `Broadcast successful! ${status.sentCount} message(s) delivered.`;
              this.resultType = 'success';
              this.notification.success(`Broadcast sent to ${status.sentCount} subscribers.`);
            }
          }
        },
        error: () => {
          clearInterval(interval);
          this.pollingIntervals.delete(broadcastId);
          this.sending = this.pollingIntervals.size > 0;
          this.resultMessage = 'Could not verify broadcast delivery status.';
          this.resultType = 'error';
          this.loadHistory();
        }
      });
    }, 1000);
    this.pollingIntervals.set(broadcastId, interval);
  }

  sendBroadcast(): void {
    this.submitted = true;
    this.broadcastForm.markAllAsTouched();

    if (this.broadcastForm.invalid) {
      this.resultMessage = 'Please select a valid approved template!';
      this.resultType = 'error';
      return;
    }

    // Validate carousel cards if carousel template
    if (this.selectedTemplateIsCarousel && !this.carouselCardsValid) {
      this.resultMessage = 'Please upload images for all carousel cards.';
      this.resultType = 'error';
      return;
    }

    this.sending = true;
    this.resultMessage = '';

    const { templateName, parameters } = this.broadcastForm.value;
    const languageCode = this.templateLoader.getLanguageCode(templateName);

    if (this.selectedTemplateIsCarousel) {
      // Build carousel request
      const cards: CarouselCard[] = this.carouselCards.map(c => ({
        imageUrl: c.imageUrl,
        bodyParam: c.bodyParam,
        buttonPayload: c.buttonPayload
      }));

      this.broadcastService.sendBroadcast({
        templateName,
        languageCode,
        parameters: [],
        isCarousel: true,
        carouselCards: cards
      }).subscribe({
        next: (res) => {
          this.resultMessage = `Sending carousel to ${res.totalRecipients} subscribers...`;
          this.resultType = 'success';
          this.submitted = false;
          this.broadcastForm.reset();
          this.selectedTemplateIsCarousel = false;
          this.carouselCards = [];
          this.pollBroadcastStatus(res.broadcastId, res.totalRecipients);
        },
        error: () => {
          this.sending = false;
          this.resultMessage = 'Failed to send carousel broadcast. Check your template.';
          this.resultType = 'error';
        }
      });
    } else {
      // Standard template
      const params = parameters && parameters.trim()
        ? parameters.split(',').map((p: string) => p.trim())
        : [];
      const imageUrl = this.broadcastForm.value.imageUrl;

      this.broadcastService.sendBroadcast({
        templateName,
        languageCode,
        parameters: params,
        imageUrl: imageUrl || undefined
      }).subscribe({
        next: (res) => {
          this.resultMessage = `Sending to ${res.totalRecipients} subscribers...`;
          this.resultType = 'success';
          this.submitted = false;
          this.broadcastForm.reset();
          this.headerImagePreview = null;
          this.pollBroadcastStatus(res.broadcastId, res.totalRecipients);
        },
        error: () => {
          this.sending = false;
          this.resultMessage = 'Failed to send broadcast. Check your template.';
          this.resultType = 'error';
        }
      });
    }
  }
}