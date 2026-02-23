import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
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

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CardModule, InputTextModule, InputNumberModule, InputTextareaModule, DropdownModule, ButtonModule],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.scss'
})
export class ProductFormComponent implements OnInit {
  isEdit = false;
  productId = 0;
  saving = false;

  product: CreateProduct = {
    name: '', description: '', brand: '', category: '',
    price: 0, stockQuantity: 0, imageUrl: ''
  };

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
      });
    }
  }

  onSubmit(): void {
    this.saving = true;
    if (this.isEdit) {
      this.productService.updateProduct(this.productId, this.product as any).subscribe({
        next: () => {
          this.saving = false;
          this.notification.success('Product updated successfully!');
          this.router.navigate(['/products']);
        },
        error: () => this.saving = false
      });
    } else {
      this.productService.createProduct(this.product).subscribe({
        next: () => {
          this.saving = false;
          this.notification.success('Product created successfully!');
          this.router.navigate(['/products']);
        },
        error: () => this.saving = false
      });
    }
  }
}