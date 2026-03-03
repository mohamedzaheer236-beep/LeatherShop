import { Component, EventEmitter, Input, Output, OnInit, inject } from '@angular/core';

import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { BroadcastService } from '../../../broadcast/services/broadcast.service';
import { CarouselCard, CarouselCardUI } from '../../../broadcast/models/broadcast.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TemplateLoaderService } from '../../../../shared/services/template-loader.service';
import { ProductService } from '../../../products/services/product.service';
import { Product, ProductImageItem } from '../../../products/models/product.model';
import { environment } from '../../../../../environments/environment';

import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-customer-broadcast-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, DialogModule, DropdownModule, InputTextModule, ButtonModule],
  templateUrl: './customer-broadcast-dialog.component.html',
  styleUrl: './customer-broadcast-dialog.component.scss',
})
export class CustomerBroadcastDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private broadcastService = inject(BroadcastService);
  private notification = inject(NotificationService);
  templateLoader = inject(TemplateLoaderService);
  private productService = inject(ProductService);

  @Input() phoneNumbers: string[] = [];
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() sent = new EventEmitter<void>();

  broadcastForm!: FormGroup;
  broadcastLang = '';
  sending = false;
  submitted = false;

  // Carousel / template metadata
  isCarousel = false;
  carouselCards: CarouselCardUI[] = [];
  hasImageHeader = false;
  bodyParamCount = 0;
  cardBodyMaxLength = 120;
  headerImagePreview: string | null = null;
  headerImageUploading = false;

  // Products for carousel card picker
  products: Product[] = [];
  productOptions: { label: string; value: number }[] = [];

  ngOnInit(): void {
    this.broadcastForm = this.fb.group({
      template: ['', [Validators.required, this.templateValidator.bind(this)]],
      params: [''],
      imageUrl: [''],
    });
    this.templateLoader.loadTemplates();
    this.loadProducts();
  }

  // ─── Template Selection ───

  private templateValidator(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (!value) return null;
    return this.templateLoader.isValidTemplate(value) ? null : { invalidTemplate: true };
  }

  get isValidTemplate(): boolean {
    return this.templateLoader.isValidTemplate(this.broadcastForm.get('template')?.value);
  }

  onTemplateSelect(): void {
    const name = this.broadcastForm.get('template')?.value;
    this.broadcastLang = this.templateLoader.getLanguageCode(name);
    this.broadcastForm.get('template')?.updateValueAndValidity();

    const tpl = name ? this.templateLoader.getTemplate(name) : undefined;
    this.hasImageHeader = tpl?.hasImageHeader ?? false;
    this.bodyParamCount = tpl?.bodyParamCount ?? 0;
    this.cardBodyMaxLength = tpl?.cardBodyMaxLength && tpl.cardBodyMaxLength > 0 ? tpl.cardBodyMaxLength : 120;

    if (!this.hasImageHeader) {
      this.headerImagePreview = null;
      this.broadcastForm.patchValue({ imageUrl: '' });
    }

    if (tpl?.isCarousel) {
      this.isCarousel = true;
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
      this.isCarousel = false;
      this.carouselCards = [];
    }
  }

  // ─── Image Upload ───

  onHeaderImageSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];
    if (!this.validateImageFile(file)) return;

    const reader = new FileReader();
    reader.onload = () => {
      this.headerImagePreview = reader.result as string;
    };
    reader.readAsDataURL(file);

    this.headerImageUploading = true;
    this.broadcastService.uploadImage(file).subscribe({
      next: path => {
        this.broadcastForm.patchValue({ imageUrl: path });
        this.headerImageUploading = false;
      },
      error: () => {
        this.headerImagePreview = null;
        this.headerImageUploading = false;
        this.notification.error('Image upload failed.');
      },
    });
    input.value = '';
  }

  removeHeaderImage(): void {
    this.headerImagePreview = null;
    this.broadcastForm.patchValue({ imageUrl: '' });
  }

  onCardImageSelect(event: Event, index: number): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];
    if (!this.validateImageFile(file)) return;

    const card = this.carouselCards[index];
    const reader = new FileReader();
    reader.onload = () => {
      card.imagePreview = reader.result as string;
    };
    reader.readAsDataURL(file);

    card.uploading = true;
    this.broadcastService.uploadImage(file).subscribe({
      next: path => {
        card.imageUrl = path;
        card.uploading = false;
      },
      error: () => {
        card.imagePreview = null;
        card.uploading = false;
        this.notification.error(`Card ${index + 1} image upload failed.`);
      },
    });
    input.value = '';
  }

  removeCardImage(index: number): void {
    const card = this.carouselCards[index];
    card.imageUrl = '';
    card.imagePreview = null;
  }

  // ─── Carousel Card Product ───

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
    return this.products.find(p => p.id === card.selectedProductId)?.imageItems ?? [];
  }

  resolveImageUrl(url: string): string {
    if (!url) return '';
    return url.startsWith('http') ? url : environment.baseUrl + url;
  }

  // ─── Computed ───

  get anyUploading(): boolean {
    return this.headerImageUploading || this.carouselCards.some(c => c.uploading);
  }

  get carouselCardsValid(): boolean {
    if (!this.isCarousel) return true;
    return this.carouselCards.every(c => c.imageUrl.trim() !== '');
  }

  isFieldInvalid(field: string): boolean {
    const control = this.broadcastForm.get(field);
    return !!(control && control.invalid && (control.dirty || control.touched || this.submitted));
  }

  // ─── Send ───

  send(): void {
    this.submitted = true;
    this.broadcastForm.markAllAsTouched();

    if (this.broadcastForm.invalid) {
      this.notification.error('Please select a valid approved template!');
      return;
    }

    if (this.isCarousel && !this.carouselCardsValid) {
      this.notification.error('Please select images for all carousel cards.');
      return;
    }

    if (this.phoneNumbers.length === 0) {
      this.notification.error('No customers selected!');
      return;
    }

    this.sending = true;
    const templateName = this.broadcastForm.get('template')?.value;

    if (this.isCarousel) {
      const cards: CarouselCard[] = this.carouselCards.map(c => ({
        imageUrl: c.imageUrl,
        bodyParam: c.bodyParam,
        buttonPayload: c.buttonPayload,
      }));
      this.broadcastService
        .sendBroadcast({
          templateName,
          languageCode: this.broadcastLang,
          parameters: [],
          isCarousel: true,
          carouselCards: cards,
          phoneNumbers: this.phoneNumbers,
        })
        .subscribe({
          next: res => {
            this.onSendSuccess(`Carousel sending to ${res.totalRecipients} customers...`);
          },
          error: () => {
            this.sending = false;
          },
        });
    } else {
      const rawParams = this.broadcastForm.get('params')?.value || '';
      const params = rawParams.trim() ? rawParams.split(',').map((p: string) => p.trim()) : [];
      const imageUrl = this.broadcastForm.get('imageUrl')?.value;

      this.broadcastService
        .sendBroadcast({
          templateName,
          languageCode: this.broadcastLang,
          parameters: params,
          imageUrl: imageUrl || undefined,
          phoneNumbers: this.phoneNumbers,
        })
        .subscribe({
          next: res => {
            this.onSendSuccess(`Broadcast sending to ${res.totalRecipients} customers...`);
          },
          error: () => {
            this.sending = false;
          },
        });
    }
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  onShow(): void {
    this.submitted = false;
    this.broadcastForm.reset({ template: '', params: '', imageUrl: '' });
    this.isCarousel = false;
    this.carouselCards = [];
    this.hasImageHeader = false;
    this.bodyParamCount = 0;
    this.headerImagePreview = null;
  }

  // ─── Private ───

  private onSendSuccess(message: string): void {
    this.sending = false;
    this.close();
    this.notification.success(message);
    this.sent.emit();
  }

  private loadProducts(): void {
    this.productService.getProducts(undefined, undefined, undefined, 1, 100).subscribe({
      next: result => {
        this.products = result.items.filter(p => p.isActive);
        this.productOptions = this.products.map(p => ({
          label: `${p.name} — ₹${p.price}`,
          value: p.id,
        }));
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
}
