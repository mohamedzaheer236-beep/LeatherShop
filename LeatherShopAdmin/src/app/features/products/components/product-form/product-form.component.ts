import { Component, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
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

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, CardModule, InputTextModule, InputNumberModule, InputTextareaModule, DropdownModule, ButtonModule, ConfirmDialogModule],
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

  private originalSnapshot = '';

  categoryOptions = [
    { label: 'Wallet', value: 'Wallet' },
    { label: 'Belt', value: 'Belt' },
    { label: 'Bag', value: 'Bag' },
    { label: 'Shoes', value: 'Shoes' },
    { label: 'Accessories', value: 'Accessories' }
  ];

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
        this.originalSnapshot = JSON.stringify(this.productForm.value);
      });
    } else {
      this.originalSnapshot = JSON.stringify(this.productForm.value);
    }
  }

  private initForm(): void {
    this.productForm = this.fb.group({
      name: ['', [Validators.required]],
      brand: ['', [Validators.required]],
      category: ['', [Validators.required]],
      price: [0, [Validators.required, Validators.min(1)]],
      stockQuantity: [0, [Validators.required, Validators.min(0)]],
      imageUrl: [''],
      description: ['']
    });
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

    if (this.productForm.invalid) {
      // Show specific toast for the first invalid field
      if (this.f['name'].errors) this.notification.error('Product name is required');
      else if (this.f['brand'].errors) this.notification.error('Brand is required');
      else if (this.f['category'].errors) this.notification.error('Category is required');
      else if (this.f['price'].errors) this.notification.error('Price must be at least ₹1');
      else if (this.f['stockQuantity'].errors) this.notification.error('Stock quantity is required');
      return;
    }

    this.saving = true;
    const formValue = this.productForm.value;

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