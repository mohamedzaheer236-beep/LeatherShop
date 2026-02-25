import { Component, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, of, timer } from 'rxjs';
import { switchMap, map, catchError } from 'rxjs/operators';
import { ProductService } from '../../services/product.service';
import { NotificationService } from '../../../../shared/services/notification.service';
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
import { FileUploadModule } from 'primeng/fileupload';
import { ProgressBarModule } from 'primeng/progressbar';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, CardModule, InputTextModule, InputNumberModule, InputTextareaModule, DropdownModule, ButtonModule, ConfirmDialogModule, ToolbarModule, FileUploadModule, ProgressBarModule],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.scss',
  providers: [ConfirmationService]
})
export class ProductFormComponent implements OnInit, HasUnsavedChanges {
  productForm!: FormGroup;
  isEdit = false;
  productId = 0;
  saving = false;
  submitted = false;
  savedSuccessfully = false;
  uploading = false;
  imagePreview: string | null = null;

  private originalSnapshot = '';

  categoryOptions: { label: string; value: string }[] = [];

  constructor(
    private fb: FormBuilder,
    private productService: ProductService,
    private route: ActivatedRoute,
    private router: Router,
    private notification: NotificationService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.initForm();

    // Set initial snapshot immediately after initForm for both branches
    // This prevents false-positive unsaved-changes dialogs during async load
    this.originalSnapshot = JSON.stringify(this.productForm.value);

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit = true;
      this.productId = +id;
      this.productService.getProduct(this.productId).subscribe(data => {
        this.productForm.patchValue({
          name: data.name, description: data.description, brand: data.brand,
          category: data.category, price: data.price,
          stockQuantity: data.stockQuantity, imageUrl: data.imageUrl
        });
        if (data.imageUrl) {
          this.imagePreview = data.imageUrl.startsWith('http')
            ? data.imageUrl
            : environment.apiUrl.replace('/api', '') + data.imageUrl;
        }
        // Update snapshot once product data is loaded — this is the real baseline
        this.originalSnapshot = JSON.stringify(this.productForm.value);
      });
    }

    // Load categories dynamically from API
    this.productService.getCategories().subscribe({
      next: (data) => {
        this.categoryOptions = data.map(c => ({ label: c, value: c }));
      },
      error: () => {
        this.notification.error('Failed to load categories.');
      }
    });
  }

  private initForm(): void {
    this.productForm = this.fb.group({
      name: ['', [Validators.required], [this.nameValidator.bind(this)]],
      brand: ['', [Validators.required]],
      category: ['', [Validators.required]],
      price: [null, [Validators.required, Validators.min(1)]],
      stockQuantity: [0, [Validators.required, Validators.min(0)]],
      imageUrl: [''],
      description: ['']
    });
  }

  /** Async validator: checks if product name already exists */
  private nameValidator(control: AbstractControl): Observable<ValidationErrors | null> {
    if (!control.value || control.value.trim().length === 0) {
      return of(null);
    }
    return timer(300).pipe(
      switchMap(() =>
        this.productService.checkName(control.value, this.isEdit ? this.productId : undefined)
      ),
      map(exists => exists ? { nameExists: true } : null),
      catchError(() => of(null))
    );
  }

  /** Handle image file selection */
  onImageSelect(event: any): void {
    const file: File = event.files?.[0] || event.target?.files?.[0];
    if (!file) return;

    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
    if (!allowedTypes.includes(file.type)) {
      this.notification.error('Only image files (JPG, PNG, WebP, GIF) are allowed.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.notification.error('Image size must be under 5 MB.');
      return;
    }

    // Show local preview
    const reader = new FileReader();
    reader.onload = () => this.imagePreview = reader.result as string;
    reader.readAsDataURL(file);

    // Upload to server
    this.uploading = true;
    this.productService.uploadImage(file).subscribe({
      next: (path) => {
        this.productForm.patchValue({ imageUrl: path });
        this.uploading = false;
        this.notification.success('Image uploaded successfully!');
      },
      error: () => {
        this.uploading = false;
        this.notification.error('Image upload failed. Please try again.');
      }
    });
  }

  /** Remove the currently selected image */
  removeImage(): void {
    this.imagePreview = null;
    this.productForm.patchValue({ imageUrl: '' });
  }

  get f() { return this.productForm.controls; }

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
        }
      });
    });
  }

  /** Check if a field should display its error state */
  isFieldInvalid(field: string): boolean {
    const control = this.f[field];
    return control.invalid && (control.dirty || control.touched || this.submitted);
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
      else if (this.f['name'].errors?.['nameExists']) this.notification.error('A product with this name already exists');
      else if (this.f['brand'].errors) this.notification.error('Brand is required');
      else if (this.f['category'].errors) this.notification.error('Category is required');
      else if (this.f['price'].errors) this.notification.error('Price must be at least ₹1');
      else if (this.f['stockQuantity'].errors) this.notification.error('Stock quantity is required');
      return;
    }

    this.saving = true;
    const formValue = { ...this.productForm.value };
    // Send null instead of empty string for optional imageUrl (backend [Url] validator rejects "")
    if (!formValue.imageUrl) formValue.imageUrl = null;

    if (this.isEdit) {
      this.productService.updateProduct(this.productId, formValue).subscribe({
        next: () => {
          this.saving = false;
          this.savedSuccessfully = true;
          this.notification.success('Product updated successfully!');
          this.router.navigate(['/products']);
        },
        error: () => this.saving = false
      });
    } else {
      this.productService.createProduct(formValue).subscribe({
        next: () => {
          this.saving = false;
          this.savedSuccessfully = true;
          this.notification.success('Product created successfully!');
          this.router.navigate(['/products']);
        },
        error: () => this.saving = false
      });
    }
  }
}