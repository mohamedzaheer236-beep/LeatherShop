import { Component, HostListener, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProductService } from '../../services/product.service';
import { CreateProduct } from '../../models/product.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { DropdownModule } from 'primeng/dropdown';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CardModule, InputTextModule, InputNumberModule, InputTextareaModule, DropdownModule, ButtonModule, MessageModule],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.scss'
})
export class ProductFormComponent implements OnInit {
  @ViewChild('productForm') productForm!: NgForm;

  isEdit = false;
  productId = 0;
  saving = false;
  submitted = false;
  savedSuccessfully = false;

  product: CreateProduct = {
    name: '', description: '', brand: '', category: '',
    price: 0, stockQuantity: 0, imageUrl: ''
  };

  // Snapshot of original values for dirty checking
  private originalProduct = '';

  categoryOptions = [
    { label: 'Wallet', value: 'Wallet' },
    { label: 'Belt', value: 'Belt' },
    { label: 'Bag', value: 'Bag' },
    { label: 'Shoes', value: 'Shoes' },
    { label: 'Accessories', value: 'Accessories' }
  ];

  constructor(
    private productService: ProductService,
    private route: ActivatedRoute,
    private router: Router,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit = true;
      this.productId = +id;
      this.productService.getProduct(this.productId).subscribe(data => {
        this.product = {
          name: data.name, description: data.description, brand: data.brand,
          category: data.category, price: data.price,
          stockQuantity: data.stockQuantity, imageUrl: data.imageUrl
        };
        this.originalProduct = JSON.stringify(this.product);
      });
    } else {
      this.originalProduct = JSON.stringify(this.product);
    }
  }

  /** True when the form has been modified from its initial state */
  get isDirty(): boolean {
    return JSON.stringify(this.product) !== this.originalProduct;
  }

  /** Guard: warn user before closing the browser tab */
  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.isDirty && !this.savedSuccessfully) {
      event.preventDefault();
    }
  }

  /** Called by CanDeactivate guard */
  canDeactivate(): boolean {
    if (!this.isDirty || this.savedSuccessfully) return true;
    return confirm('You have unsaved changes. Are you sure you want to leave?');
  }

  /** Validate before submitting */
  isFieldInvalid(fieldName: string): boolean {
    if (!this.submitted) return false;
    const control = this.productForm?.controls[fieldName];
    return !!(control && control.invalid);
  }

  onSubmit(): void {
    this.submitted = true;

    // Client-side validation
    if (!this.product.name?.trim()) {
      this.notification.error('Product name is required');
      return;
    }
    if (!this.product.brand?.trim()) {
      this.notification.error('Brand is required');
      return;
    }
    if (!this.product.category) {
      this.notification.error('Category is required');
      return;
    }
    if (!this.product.price || this.product.price < 1) {
      this.notification.error('Price must be at least ₹1');
      return;
    }
    if (this.product.stockQuantity == null || this.product.stockQuantity < 0) {
      this.notification.error('Stock quantity is required');
      return;
    }

    this.saving = true;
    if (this.isEdit) {
      this.productService.updateProduct(this.productId, this.product as any).subscribe({
        next: () => {
          this.saving = false;
          this.savedSuccessfully = true;
          this.notification.success('Product updated successfully!');
          this.router.navigate(['/products']);
        },
        error: () => this.saving = false
      });
    } else {
      this.productService.createProduct(this.product).subscribe({
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