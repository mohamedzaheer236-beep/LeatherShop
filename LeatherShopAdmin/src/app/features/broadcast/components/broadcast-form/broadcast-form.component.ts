import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, OnDestroy, OnInit, Output, inject } from '@angular/core';

import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { BroadcastService } from '../../services/broadcast.service';
import { CarouselCard, CarouselCardUI } from '../../models/broadcast.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TemplateLoaderService } from '../../../../shared/services/template-loader.service';
import { ProductService } from '../../../products/services/product.service';
import { Product, ProductImageItem } from '../../../products/models/product.model';
import { environment } from '../../../../../environments/environment';

import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-broadcast-form',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, CardModule, DropdownModule, InputTextModule, ButtonModule],
  templateUrl: './broadcast-form.component.html',
  styleUrl: './broadcast-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BroadcastFormComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private broadcastService = inject(BroadcastService);
  private notification = inject(NotificationService);
  templateLoader = inject(TemplateLoaderService);
  private productService = inject(ProductService);
  private cdr = inject(ChangeDetectorRef);

  /** Emits when a broadcast has been sent (parent should refresh history). */
  @Output() sent = new EventEmitter<void>();

  broadcastForm!: FormGroup;
  sending = false;
  resultMessage = '';
  resultType: 'success' | 'error' | '' = '';
  submitted = false;

  // Carousel template support
  selectedTemplateIsCarousel = false;
  carouselCards: CarouselCardUI[] = [];

  // Template metadata for field visibility
  selectedTemplateHasImageHeader = false;
  selectedTemplateBodyParamCount = 0;
  cardBodyMaxLength = 120;

  // Product list for carousel card "View Details" button
  products: Product[] = [];
  productOptions: { label: string; value: number }[] = [];

  // Header image upload (for standard templates)
  headerImagePreview: string | null = null;
  headerImageUploading = false;

  private pollingIntervals = new Map<number, ReturnType<typeof setInterval>>();

  ngOnInit(): void {
    this.initForm();
    this.templateLoader.loadTemplates();
    this.loadProducts();
  }

  ngOnDestroy(): void {
    this.pollingIntervals.forEach(interval => clearInterval(interval));
    this.pollingIntervals.clear();
  }

  // ─── Form Setup ───

  private initForm(): void {
    this.broadcastForm = this.fb.group({
      templateName: [null, [Validators.required, this.templateValidator.bind(this)]],
      parameters: [''],
      imageUrl: [''],
    });
  }

  private templateValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null;
    if (!this.templateLoader.isValidTemplate(value)) {
      return { invalidTemplate: true };
    }
    return null;
  }

  get f() {
    return this.broadcastForm.controls;
  }

  isFieldInvalid(field: string): boolean {
    const control = this.f[field];
    return control.invalid && (control.dirty || control.touched || this.submitted);
  }

  get isValidTemplate(): boolean {
    return this.templateLoader.isValidTemplate(this.f['templateName'].value);
  }

  get anyUploading(): boolean {
    return this.headerImageUploading || this.carouselCards.some(c => c.uploading);
  }

  get carouselCardsValid(): boolean {
    if (!this.selectedTemplateIsCarousel) return true;
    return this.carouselCards.every(c => c.imageUrl.trim() !== '');
  }

  // ─── Template Selection ───

  onTemplateSelect(): void {
    this.f['templateName'].updateValueAndValidity();
    const templateName = this.f['templateName'].value;
    const tpl = templateName ? this.templateLoader.getTemplate(templateName) : undefined;

    this.selectedTemplateHasImageHeader = tpl?.hasImageHeader ?? false;
    this.selectedTemplateBodyParamCount = tpl?.bodyParamCount ?? 0;
    this.cardBodyMaxLength = tpl?.cardBodyMaxLength && tpl.cardBodyMaxLength > 0 ? tpl.cardBodyMaxLength : 120;

    if (!this.selectedTemplateHasImageHeader) {
      this.headerImagePreview = null;
      this.broadcastForm.patchValue({ imageUrl: '' });
    }

    if (tpl?.isCarousel) {
      this.selectedTemplateIsCarousel = true;
      this.carouselCards = Array.from({ length: tpl.cardCount }, () => ({
        imageUrl: '',
        imagePreview: null,
        bodyParam: '',
        buttonPayload: '',
        selectedProductId: null,
        selectedImageId: null,
        uploading: false,
      }));
    } else {
      this.selectedTemplateIsCarousel = false;
      this.carouselCards = [];
    }
  }

  onDropdownHide(): void {
    this.f['templateName'].markAsTouched();
  }

  // ─── Header Image Upload ───

  onHeaderImageSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];

    if (!this.validateImageFile(file)) return;

    const reader = new FileReader();
    reader.onload = () => {
      this.headerImagePreview = reader.result as string;
      this.cdr.markForCheck();
    };
    reader.readAsDataURL(file);

    this.headerImageUploading = true;
    this.broadcastService.uploadImage(file).subscribe({
      next: path => {
        this.broadcastForm.patchValue({ imageUrl: path });
        this.headerImageUploading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.headerImagePreview = null;
        this.headerImageUploading = false;
        this.notification.error('Image upload failed. Please try again.');
        this.cdr.markForCheck();
      },
    });
    input.value = '';
  }

  removeHeaderImage(): void {
    this.headerImagePreview = null;
    this.broadcastForm.patchValue({ imageUrl: '' });
  }

  // ─── Carousel Card Image Upload ───

  onCardImageSelect(event: Event, index: number): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];

    if (!this.validateImageFile(file)) return;

    const card = this.carouselCards[index];

    const reader = new FileReader();
    reader.onload = () => {
      card.imagePreview = reader.result as string;
      this.cdr.markForCheck();
    };
    reader.readAsDataURL(file);

    card.uploading = true;
    this.broadcastService.uploadImage(file).subscribe({
      next: path => {
        card.imageUrl = path;
        card.uploading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        card.imagePreview = null;
        card.uploading = false;
        this.notification.error(`Card ${index + 1} image upload failed.`);
        this.cdr.markForCheck();
      },
    });
    input.value = '';
  }

  removeCardImage(index: number): void {
    const card = this.carouselCards[index];
    card.imageUrl = '';
    card.imagePreview = null;
  }

  // ─── Carousel Card Product Selection ───

  onCardProductSelect(index: number): void {
    const card = this.carouselCards[index];
    if (card.selectedProductId) {
      const product = this.products.find(p => p.id === card.selectedProductId);
      if (!card.bodyParam.trim() && product) {
        card.bodyParam = product.name.substring(0, this.cardBodyMaxLength);
      }
      if (product?.imageItems?.length) {
        this.selectCardImage(index, product.imageItems[0]);
      } else {
        card.buttonPayload = `view_${card.selectedProductId}`;
        card.selectedImageId = null;
        card.imageUrl = '';
        card.imagePreview = null;
      }
    } else {
      card.buttonPayload = '';
      card.selectedImageId = null;
      card.imageUrl = '';
      card.imagePreview = null;
    }
  }

  selectCardImage(index: number, img: ProductImageItem): void {
    const card = this.carouselCards[index];
    card.selectedImageId = img.id;
    card.imageUrl = img.url;
    card.imagePreview = this.resolveImageUrl(img.url);
    if (card.selectedProductId) {
      card.buttonPayload = img.id > 0 ? `view_${card.selectedProductId}_pi${img.id}` : `view_${card.selectedProductId}`;
    }
  }

  getCardProductImages(index: number): ProductImageItem[] {
    const card = this.carouselCards[index];
    if (!card.selectedProductId) return [];
    const product = this.products.find(p => p.id === card.selectedProductId);
    return product?.imageItems ?? [];
  }

  resolveImageUrl(url: string): string {
    if (!url) return '';
    return url.startsWith('http') ? url : environment.baseUrl + url;
  }

  // ─── Send Broadcast ───

  sendBroadcast(): void {
    this.submitted = true;
    this.broadcastForm.markAllAsTouched();

    if (this.broadcastForm.invalid) {
      this.resultMessage = 'Please select a valid approved template!';
      this.resultType = 'error';
      return;
    }

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
      const cards: CarouselCard[] = this.carouselCards.map(c => ({
        imageUrl: c.imageUrl,
        bodyParam: c.bodyParam,
        buttonPayload: c.buttonPayload,
      }));

      this.broadcastService
        .sendBroadcast({
          templateName,
          languageCode,
          parameters: [],
          isCarousel: true,
          carouselCards: cards,
        })
        .subscribe({
          next: res => {
            this.resultMessage = `Sending carousel to ${res.totalRecipients} subscribers...`;
            this.resultType = 'success';
            this.submitted = false;
            this.broadcastForm.reset();
            this.selectedTemplateIsCarousel = false;
            this.carouselCards = [];
            this.cdr.markForCheck();
            this.pollBroadcastStatus(res.broadcastId, res.totalRecipients);
          },
          error: () => {
            this.sending = false;
            this.resultMessage = 'Failed to send carousel broadcast. Check your template.';
            this.resultType = 'error';
            this.cdr.markForCheck();
          },
        });
    } else {
      const params = parameters && parameters.trim() ? parameters.split(',').map((p: string) => p.trim()) : [];
      const imageUrl = this.broadcastForm.value.imageUrl;

      this.broadcastService
        .sendBroadcast({
          templateName,
          languageCode,
          parameters: params,
          imageUrl: imageUrl || undefined,
        })
        .subscribe({
          next: res => {
            this.resultMessage = `Sending to ${res.totalRecipients} subscribers...`;
            this.resultType = 'success';
            this.submitted = false;
            this.broadcastForm.reset();
            this.headerImagePreview = null;
            this.cdr.markForCheck();
            this.pollBroadcastStatus(res.broadcastId, res.totalRecipients);
          },
          error: () => {
            this.sending = false;
            this.resultMessage = 'Failed to send broadcast. Check your template.';
            this.resultType = 'error';
            this.cdr.markForCheck();
          },
        });
    }
  }

  // ─── Private Helpers ───

  private loadProducts(): void {
    this.productService.getProducts(undefined, undefined, undefined, 1, 100).subscribe({
      next: result => {
        this.products = result.items.filter(p => p.isActive);
        this.productOptions = this.products.map(p => ({
          label: `${p.name} — ₹${p.price}`,
          value: p.id,
        }));
        this.cdr.markForCheck();
      },
      error: () => {
        /* silently ignore — products are optional enhancement */
      },
    });
  }

  private validateImageFile(file: File): boolean {
    if (!file.type.startsWith('image/')) {
      this.notification.error('Please select an image file.');
      return false;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.notification.error('Image must be under 5 MB.');
      return false;
    }
    return true;
  }

  private pollBroadcastStatus(broadcastId: number, totalRecipients: number): void {
    if (this.pollingIntervals.has(broadcastId)) return;

    let attempts = 0;
    const maxAttempts = 30;
    const interval = setInterval(() => {
      attempts++;
      this.broadcastService.getBroadcastStatus(broadcastId).subscribe({
        next: status => {
          const processed = status.sentCount + status.failedCount;
          if (processed >= totalRecipients || attempts >= maxAttempts) {
            clearInterval(interval);
            this.pollingIntervals.delete(broadcastId);
            this.sending = this.pollingIntervals.size > 0;
            this.sent.emit();
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
            this.cdr.markForCheck();
          }
        },
        error: () => {
          clearInterval(interval);
          this.pollingIntervals.delete(broadcastId);
          this.sending = this.pollingIntervals.size > 0;
          this.resultMessage = 'Could not verify broadcast delivery status.';
          this.resultType = 'error';
          this.sent.emit();
          this.cdr.markForCheck();
        },
      });
    }, 1000);
    this.pollingIntervals.set(broadcastId, interval);
  }
}
