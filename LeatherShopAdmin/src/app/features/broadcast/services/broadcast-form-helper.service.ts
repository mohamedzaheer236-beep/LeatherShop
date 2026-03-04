import { ChangeDetectorRef, Injectable, inject } from '@angular/core';
import { BroadcastService } from './broadcast.service';
import { CarouselCardUI } from '../models/broadcast.model';
import { NotificationService } from '../../../shared/services/notification.service';
import { TemplateLoaderService } from '../../../shared/services/template-loader.service';
import { ProductService } from '../../products/services/product.service';
import { Product, ProductImageItem } from '../../products/models/product.model';
import { environment } from '../../../../environments/environment';

/**
 * Per-component helper that manages shared broadcast form state:
 * template metadata, carousel cards, image uploads, and product selection.
 *
 * Provide at the component level so each form gets its own instance
 * and inherits the host component's ChangeDetectorRef.
 */
@Injectable()
export class BroadcastFormHelperService {
  private broadcastService = inject(BroadcastService);
  private notification = inject(NotificationService);
  private productService = inject(ProductService);
  private cdr = inject(ChangeDetectorRef);
  readonly templateLoader = inject(TemplateLoaderService);

  // ─── Template Metadata ───
  isCarousel = false;
  hasImageHeader = false;
  bodyParamCount = 0;
  cardBodyMaxLength = 120;

  // ─── Carousel Card State ───
  carouselCards: CarouselCardUI[] = [];

  // ─── Header Image State ───
  headerImagePreview: string | null = null;
  headerImageUploading = false;

  // ─── Product List ───
  products: Product[] = [];
  productOptions: { label: string; value: number }[] = [];

  // ─── Initialization ───

  /** Load templates and products. Call from component's ngOnInit. */
  init(): void {
    this.templateLoader.loadTemplates(false, () => this.cdr.markForCheck());
    this.loadProducts();
  }

  // ─── Template Selection ───

  /** Parse template metadata and initialise carousel cards if needed. */
  applyTemplate(templateName: string): void {
    const tpl = templateName ? this.templateLoader.getTemplate(templateName) : undefined;
    this.hasImageHeader = tpl?.hasImageHeader ?? false;
    this.bodyParamCount = tpl?.bodyParamCount ?? 0;
    this.cardBodyMaxLength =
      tpl?.cardBodyMaxLength && tpl.cardBodyMaxLength > 0 ? tpl.cardBodyMaxLength : 120;

    if (!this.hasImageHeader) {
      this.headerImagePreview = null;
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

  /** Check if a template name is valid (approved). */
  isValidTemplate(name: string): boolean {
    return this.templateLoader.isValidTemplate(name);
  }

  /** Get language code for a template. */
  getLanguageCode(name: string): string {
    return this.templateLoader.getLanguageCode(name);
  }

  // ─── Computed ───

  get anyUploading(): boolean {
    return this.headerImageUploading || this.carouselCards.some(c => c.uploading);
  }

  get carouselCardsValid(): boolean {
    if (!this.isCarousel) return true;
    return this.carouselCards.every(c => c.imageUrl.trim() !== '');
  }

  // ─── Header Image Upload ───

  /**
   * Handle header image file selection, preview, and upload.
   * @param event     native file input change event
   * @param onUploaded callback invoked with the uploaded server path
   */
  handleHeaderImageUpload(event: Event, onUploaded: (path: string) => void): void {
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
        onUploaded(path);
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

  clearHeaderImage(): void {
    this.headerImagePreview = null;
  }

  // ─── Carousel Card Image Upload ───

  handleCardImageUpload(event: Event, index: number): void {
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
      card.buttonPayload =
        img.id > 0 ? `view_${card.selectedProductId}_pi${img.id}` : `view_${card.selectedProductId}`;
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

  /** Reset all state to defaults (dialog onShow / form reset). */
  reset(): void {
    this.isCarousel = false;
    this.carouselCards = [];
    this.hasImageHeader = false;
    this.bodyParamCount = 0;
    this.cardBodyMaxLength = 120;
    this.headerImagePreview = null;
    this.headerImageUploading = false;
  }

  // ─── Private ───

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

  validateImageFile(file: File): boolean {
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
