import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProductService } from '../../services/product.service';
import { CreateProduct } from '../../models/product.model';
import { NotificationService } from '../../../../shared/services/notification.service';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.scss'
})
export class ProductFormComponent implements OnInit {
  isEdit = false;
  productId = 0;
  saving = false;

  product: CreateProduct = {
    name: '',
    description: '',
    brand: '',
    category: '',
    price: 0,
    stockQuantity: 0,
    imageUrl: ''
  };

  categoryOptions = ['Wallet', 'Belt', 'Bag', 'Shoes', 'Accessories'];

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
          name: data.name,
          description: data.description,
          brand: data.brand,
          category: data.category,
          price: data.price,
          stockQuantity: data.stockQuantity,
          imageUrl: data.imageUrl
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
          setTimeout(() => this.router.navigate(['/products']), 1500);
        },
        error: () => this.saving = false
      });
    } else {
      this.productService.createProduct(this.product).subscribe({
        next: () => {
          this.saving = false;
          this.notification.success('Product created successfully!');
          setTimeout(() => this.router.navigate(['/products']), 1500);
        },
        error: () => this.saving = false
      });
    }
  }
}
