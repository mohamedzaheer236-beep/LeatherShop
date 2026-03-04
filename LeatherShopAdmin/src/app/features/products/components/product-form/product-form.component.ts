import { ChangeDetectionStrategy, ChangeDetectorRef, Component, HostListener, OnInit, inject } from '@angular/core';

import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, of, timer } from 'rxjs';
import { switchMap, map, catchError } from 'rxjs/operators';
import { ProductService } from '../../services/product.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import { isFieldInvalid as checkFieldInvalid } from '../../../../shared/utils/form.utils';
import { HasUnsavedChanges } from '../../../../core/guards/unsaved-changes.guard';
import { ConfirmationService } from 'primeng/api';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { DropdownModule } from 'primeng/dropdown';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToolbarModule } from 'primeng/toolbar';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    CardModule,
    InputTextModule,
    InputNumberModule,
    InputTextareaModule,
    DropdownModule,
    ButtonModule,
    ConfirmDialogModule,
    ToolbarModule,
  ],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.scss',
  providers: [ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductFormComponent implements OnInit, HasUnsavedChanges {
  private fb = inject(FormBuilder);
  private productService = inject(ProductService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private notification = inject(NotificationService);
  private confirmationService = inject(ConfirmationService);
  private cdr = inject(ChangeDetectorRef);

  productForm!: FormGroup;
  isEdit = false;
  productId = 0;
  saving = false;
  submitted = false;
  savedSuccessfully = false;

  /** Multi-image support: up to 4 images. Index 0 = primary. */
  uploading = false;
  images: { preview: string; path: string }[] = [];
  readonly maxImages = 4;
  imageErrors: string[] = [];

  private originalSnapshot = '';

  categoryOptions: { label: string; value: string }[] = [];

  ngOnInit(): void {
    this.initForm();

    // Set initial snapshot immediately after initForm for both branches
    // This prevents false-positive unsaved-changes dialogs during async load
    this.originalSnapshot = JSON.stringify(this.productForm.value);

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit = true;
      this.productId = +id;
      this.productService.getProduct(this.productId).subscribe({
        next: data => {
          this.productForm.patchValue({
            name: data.name,
            description: data.description,
            brand: data.brand,
            category: data.category,
            price: data.price,
            stockQuantity: data.stockQuantity,
            imageUrl: data.imageUrl,
          });

          // Load existing images into the multi-image array
          this.images = [];
          if (data.imageUrls && data.imageUrls.length > 0) {
            for (const url of data.imageUrls) {
              const preview = url.startsWith('http') ? url : environment.baseUrl + url;
              this.images.push({ preview, path: url });
            }
          } else if (data.imageUrl) {
            // Fallback: single imageUrl only (no imageUrls from server)
            const preview = data.imageUrl.startsWith('http') ? data.imageUrl : environment.baseUrl + data.imageUrl;
            this.images.push({ preview, path: data.imageUrl });
          }
          // Sync loaded images back to form controls so imageUrls is not stale
          this.syncFormImages();
          // In edit mode, allow stock of 0 (out of stock)
          this.productForm.get('stockQuantity')!.setValidators([Validators.required, Validators.min(0)]);
          this.productForm.get('stockQuantity')!.updateValueAndValidity();
          // Update snapshot once product data is loaded — this is the real baseline
          this.originalSnapshot = JSON.stringify(this.productForm.value);
          this.cdr.markForCheck();
        },
        error: () => {
          // Toast shown by error interceptor — just navigate back
          this.router.navigate(['/products']);
        },
      });
    }

    // Load categories dynamically from API
    this.productService.getCategories().subscribe({
      next: data => {
        this.categoryOptions = data.map(c => ({ label: c, value: c }));
        this.cdr.markForCheck();
      },
      error: () => {
        // Toast shown by error interceptor
      },
    });
  }

  private initForm(): void {
    this.productForm = this.fb.group({
      name: ['', [Validators.required], [this.nameValidator.bind(this)]],
      brand: ['', [Validators.required]],
      category: ['', [Validators.required]],
      price: [null, [Validators.required, Validators.min(1)]],
      stockQuantity: [null, [Validators.required, Validators.min(1)]],
      imageUrl: [''],
      imageUrls: [[] as string[]],
      description: [''],
    });
  }

  /** Async validator: checks if product name already exists */
  private nameValidator(control: AbstractControl): Observable<ValidationErrors | null> {
    if (!control.value || control.value.trim().length === 0) {
      return of(null);
    }
    return timer(300).pipe(
      switchMap(() => this.productService.checkName(control.value, this.isEdit ? this.productId : undefined)),
      map(exists => (exists ? { nameExists: true } : null)),
      catchError(() => of(null)),
    );
  }

  /** Handle image file selection — supports multiple files */
  onImageSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const fileList: FileList | null = input?.files ?? null;
    if (!fileList || fileList.length === 0) return;

    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
    const allowedLabel = 'JPG, PNG, WebP, GIF';
    const filesToProcess: File[] = [];
    this.imageErrors = [];

    for (const file of Array.from(fileList)) {
      if (this.images.length + filesToProcess.length >= this.maxImages) {
        this.imageErrors.push(`Maximum ${this.maxImages} images allowed.`);
        break;
      }
      if (!allowedTypes.includes(file.type)) {
        this.imageErrors.push(`"${file.name}" — unsupported format. Only ${allowedLabel} are allowed.`);
        continue;
      }
      filesToProcess.push(file);
    }

    if (filesToProcess.length === 0) {
      // Reset the file input
      if (event.target) (event.target as HTMLInputElement).value = '';
      return;
    }

    this.uploading = true;

    // Compress images client-side before uploading (resize + quality reduction)
    Promise.all(filesToProcess.map(f => this.compressImage(f)))
      .then(compressedFiles => {
        // Generate local previews
        const previewPromises = compressedFiles.map(
          file =>
            new Promise<string>(resolve => {
              const reader = new FileReader();
              reader.onload = () => resolve(reader.result as string);
              reader.readAsDataURL(file);
            }),
        );

        // Upload compressed files to server
        this.productService.uploadImages(compressedFiles).subscribe({
          next: paths => {
            Promise.all(previewPromises).then(previews => {
              for (let i = 0; i < paths.length; i++) {
                this.images.push({ preview: previews[i], path: paths[i] });
              }
              this.syncFormImages();
              this.uploading = false;
              this.notification.success(paths.length === 1 ? 'Image uploaded!' : `${paths.length} images uploaded!`);
              this.cdr.markForCheck();
            });
          },
          error: () => {
            this.uploading = false;
            this.cdr.markForCheck();
          },
        });
      })
      .catch(() => {
        this.uploading = false;
        this.notification.error('Failed to process images. Please try again.');
        this.cdr.markForCheck();
      });

    // Reset the file input so the same file(s) can be re-selected
    if (event.target) (event.target as HTMLInputElement).value = '';
  }

  /**
   * Compress an image client-side to target ~300KB.
   * Resizes to max 1200px and iteratively lowers JPEG quality until under 300KB.
   */
  private compressImage(file: File, maxDimension = 1200): Promise<File> {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.onload = () => {
        URL.revokeObjectURL(img.src); // free blob memory immediately
        let { width, height } = img;

        // Resize if larger than maxDimension
        if (width > maxDimension || height > maxDimension) {
          if (width > height) {
            height = Math.round(height * (maxDimension / width));
            width = maxDimension;
          } else {
            width = Math.round(width * (maxDimension / height));
            height = maxDimension;
          }
        }

        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext('2d')!;
        ctx.drawImage(img, 0, 0, width, height);

        const targetBytes = 300 * 1024; // 300 KB

        // Try decreasing quality until we're under 300KB
        const tryCompress = (quality: number) => {
          canvas.toBlob(
            blob => {
              if (!blob) {
                reject(new Error('Compression failed'));
                return;
              }
              // If still too big and quality can go lower, try again
              if (blob.size > targetBytes && quality > 0.3) {
                tryCompress(quality - 0.1);
                return;
              }
              const compressedName = file.name.replace(/\.[^.]+$/, '.jpg');
              resolve(new File([blob], compressedName, { type: 'image/jpeg' }));
            },
            'image/jpeg',
            quality,
          );
        };

        tryCompress(0.85);
      };
      img.onerror = () => {
        URL.revokeObjectURL(img.src);
        reject(new Error('Failed to load image'));
      };
      img.src = URL.createObjectURL(file);
    });
  }

  /** Remove an image by index */
  removeImage(index: number): void {
    this.images = this.images.filter((_, i) => i !== index);
    this.syncFormImages();
    this.cdr.markForCheck();
  }

  /** Sync the images array back to form controls */
  private syncFormImages(): void {
    const primary = this.images.length > 0 ? this.images[0].path : '';
    const additional = this.images.slice(1).map(img => img.path);
    this.productForm.patchValue({ imageUrl: primary, imageUrls: additional });
  }

  get f() {
    return this.productForm.controls;
  }

  /** True when the form has been modified from its initial state */
  get isDirty(): boolean {
    return JSON.stringify(this.productForm.value) !== this.originalSnapshot;
  }

  /** Guard: warn user before closing the browser tab */
  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.isDirty && !this.savedSuccessfully) {
      event.preventDefault();
    }
  }

  /** Called by CanDeactivate guard */
  canDeactivate(): boolean | Observable<boolean> {
    if (!this.isDirty || this.savedSuccessfully) return true;
    return new Observable<boolean>(observer => {
      this.confirmationService.confirm({
        header: 'Unsaved Changes',
        message: 'You have unsaved changes. Are you sure you want to leave this page?',
        icon: 'pi pi-exclamation-triangle',
        acceptLabel: 'Leave',
        rejectLabel: 'Stay',
        acceptButtonStyleClass: 'p-button-danger',
        rejectButtonStyleClass: 'p-button-secondary p-button-outlined',
        accept: () => {
          observer.next(true);
          observer.complete();
        },
        reject: () => {
          observer.next(false);
          observer.complete();
        },
      });
    });
  }

  /** Check if a field should display its error state */
  isFieldInvalid(field: string): boolean {
    return checkFieldInvalid(this.productForm, field, this.submitted);
  }

  onSubmit(): void {
    this.submitted = true;
    this.productForm.markAllAsTouched();

    if (this.f['name'].pending) {
      this.notification.error('Checking product name availability, please wait...');
      return;
    }

    if (this.productForm.invalid) {
      // Show specific toast for the first invalid field
      if (this.f['name'].errors?.['required']) this.notification.error('Product name is required');
      else if (this.f['name'].errors?.['nameExists'])
        this.notification.error('A product with this name already exists');
      else if (this.f['brand'].errors) this.notification.error('Brand is required');
      else if (this.f['category'].errors) this.notification.error('Category is required');
      else if (this.f['price'].errors) this.notification.error('Price must be at least ₹1');
      else if (this.f['stockQuantity'].errors?.['required']) this.notification.error('Stock quantity is required');
      else if (this.f['stockQuantity'].errors?.['min'])
        this.notification.error('Stock quantity must be at least 1 when creating a product');
      return;
    }

    this.saving = true;
    const formValue = { ...this.productForm.value };
    // Send null instead of empty string for optional imageUrl (backend [Url] validator rejects "")
    if (!formValue.imageUrl) formValue.imageUrl = null;
    // Ensure imageUrls is sent as array (even if empty) so backend replaces the old set
    if (!formValue.imageUrls || formValue.imageUrls.length === 0) {
      formValue.imageUrls = [];
    }

    if (this.isEdit) {
      this.productService.updateProduct(this.productId, formValue).subscribe({
        next: () => {
          this.saving = false;
          this.savedSuccessfully = true;
          this.notification.success('Product updated successfully!');
          this.cdr.markForCheck();
          this.router.navigate(['/products']);
        },
        error: () => {
          this.saving = false;
          this.cdr.markForCheck();
        },
      });
    } else {
      this.productService.createProduct(formValue).subscribe({
        next: () => {
          this.saving = false;
          this.savedSuccessfully = true;
          this.notification.success('Product created successfully!');
          this.cdr.markForCheck();
          this.router.navigate(['/products']);
        },
        error: () => {
          this.saving = false;
          this.cdr.markForCheck();
        },
      });
    }
  }
}
