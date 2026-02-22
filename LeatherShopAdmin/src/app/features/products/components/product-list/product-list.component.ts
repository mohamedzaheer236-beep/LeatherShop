import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ProductService } from '../../services/product.service';
import { Product } from '../../models/product.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, LoadingSpinnerComponent],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss'
})
export class ProductListComponent implements OnInit {
  products: Product[] = [];
  categories: string[] = [];
  brands: string[] = [];
  loading = true;

  filterCategory = '';
  filterBrand = '';
  searchText = '';

  constructor(
    private productService: ProductService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadProducts();
    this.productService.getCategories().subscribe(data => this.categories = data);
    this.productService.getBrands().subscribe(data => this.brands = data);
  }

  loadProducts(): void {
    this.loading = true;
    this.productService.getProducts(this.filterCategory, this.filterBrand, this.searchText).subscribe({
      next: (data) => {
        this.products = data;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  onFilterChange(): void {
    this.loadProducts();
  }

  toggleActive(product: Product): void {
    this.productService.updateProduct(product.id, { isActive: !product.isActive } as any).subscribe({
      next: () => {
        product.isActive = !product.isActive;
        this.notification.success(`Product ${product.isActive ? 'activated' : 'deactivated'}.`);
      }
    });
  }

  deleteProduct(product: Product): void {
    if (confirm(`Delete "${product.name}"? This cannot be undone.`)) {
      this.productService.deleteProduct(product.id).subscribe({
        next: () => {
          this.products = this.products.filter(p => p.id !== product.id);
          this.notification.success('Product deleted successfully.');
        }
      });
    }
  }
}
