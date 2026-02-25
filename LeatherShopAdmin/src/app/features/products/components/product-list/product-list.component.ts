import { Component, OnInit, Renderer2, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
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
  imports: [CommonModule, RouterLink, ReactiveFormsModule, LoadingSpinnerComponent, TableModule, ButtonModule, InputTextModule, DropdownModule, TagModule, ConfirmDialogModule, ToolbarModule, TooltipModule],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss',
  providers: [ConfirmationService]
})
export class ProductListComponent implements OnInit {
  products: Product[] = [];
  categoryOptions: any[] = [];
  brandOptions: any[] = [];
  loading = true;
  filterForm!: FormGroup;

  @ViewChild('catDropdown') catDropdown: any;
  @ViewChild('brandDropdown') brandDropdown: any;

  constructor(
    private fb: FormBuilder,
    private productService: ProductService,
    private notification: NotificationService,
    private confirmationService: ConfirmationService,
    private renderer: Renderer2
  ) {}

  ngOnInit(): void {
    this.filterForm = this.fb.group({
      searchText: [''],
      filterCategory: [null],
      filterBrand: [null]
    });

    this.loadProducts();
    this.productService.getCategories().subscribe({
      next: (data) => { this.categoryOptions = data.map(c => ({ label: c, value: c })); }
    });
    this.productService.getBrands().subscribe({
      next: (data) => { this.brandOptions = data.map(b => ({ label: b, value: b })); }
    });
  }

  /** Clear the filter text inside a dropdown's search input */
  clearDropdownFilter(dropdown: any): void {
    if (dropdown?.filterValue) {
      dropdown.filterValue = '';
      dropdown.filterBy = 'label';
      dropdown.onFilterInputChange({ target: { value: '' } });
    }
  }

  /** Set id/name on PrimeNG's internal filter input (fixes Chrome DevTools warning) */
  onDropdownShow(dropdown: any, filterId: string): void {
    const filterInput = dropdown?.filterViewChild?.nativeElement;
    if (filterInput) {
      this.renderer.setAttribute(filterInput, 'id', filterId);
      this.renderer.setAttribute(filterInput, 'name', filterId);
    }
  }

  /** Called only by Search button or Enter key */
  search(): void {
    this.loadProducts();
  }

  /** Reset all filters and reload */
  resetFilters(): void {
    this.filterForm.reset({ searchText: '', filterCategory: null, filterBrand: null });
    this.loadProducts();
  }

  loadProducts(): void {
    this.loading = true;
    const { searchText, filterCategory, filterBrand } = this.filterForm.value;
    this.productService.getProducts(filterCategory || '', filterBrand || '', searchText || '').subscribe({
      next: (data) => {
        this.products = data;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  toggleActive(product: Product): void {
    this.productService.updateProduct(product.id, { isActive: !product.isActive } as any).subscribe({
      next: () => {
        product.isActive = !product.isActive;
        this.notification.success(`Product ${product.isActive ? 'activated' : 'deactivated'}.`);
      },
      error: () => {
        // Toast shown by error interceptor
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
          },
          error: () => {
            // Toast shown by error interceptor
          }
        });
      }
    });
  }
}