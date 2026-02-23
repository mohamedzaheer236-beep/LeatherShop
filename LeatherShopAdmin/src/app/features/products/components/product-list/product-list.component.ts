import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ProductService } from '../../services/product.service';
import { Product } from '../../models/product.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, LoadingSpinnerComponent, TableModule, ButtonModule, InputTextModule, DropdownModule, TagModule, ConfirmDialogModule, ToolbarModule, TooltipModule],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss',
  providers: [ConfirmationService]
})
export class ProductListComponent implements OnInit {
  products: Product[] = [];
  categories: string[] = [];
  brands: string[] = [];
  categoryOptions: any[] = [];
  brandOptions: any[] = [];
  loading = true;

  filterCategory = '';
  filterBrand = '';
  searchText = '';

  constructor(
    private productService: ProductService,
    private notification: NotificationService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.loadProducts();
    this.productService.getCategories().subscribe(data => {
      this.categories = data;
      this.categoryOptions = [{ label: 'All Categories', value: '' }, ...data.map(c => ({ label: c, value: c }))];
    });
    this.productService.getBrands().subscribe(data => {
      this.brands = data;
      this.brandOptions = [{ label: 'All Brands', value: '' }, ...data.map(b => ({ label: b, value: b }))];
    });
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

  onSearch(): void {
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
    this.confirmationService.confirm({
      message: `Delete "${product.name}"? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.productService.deleteProduct(product.id).subscribe({
          next: () => {
            this.products = this.products.filter(p => p.id !== product.id);
            this.notification.success('Product deleted successfully.');
          }
        });
      }
    });
  }
}