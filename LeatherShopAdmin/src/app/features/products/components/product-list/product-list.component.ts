import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, Renderer2, ViewChild, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { PaginatorState } from 'primeng/paginator';
import { ProductService } from '../../services/product.service';
import { Product } from '../../models/product.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { Dropdown, DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { PaginatorModule } from 'primeng/paginator';
import { DropdownAccessibilityDirective } from '../../../../shared/directives/dropdown-accessibility.directive';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [
    DecimalPipe,
    RouterLink,
    ReactiveFormsModule,
    LoadingSpinnerComponent,
    TableModule,
    ButtonModule,
    InputTextModule,
    DropdownModule,
    TagModule,
    ConfirmDialogModule,
    ToolbarModule,
    TooltipModule,
    PaginatorModule,
    DropdownAccessibilityDirective,
  ],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss',
  providers: [ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductListComponent implements OnInit {
  private fb = inject(FormBuilder);
  private productService = inject(ProductService);
  private notification = inject(NotificationService);
  private confirmationService = inject(ConfirmationService);
  private renderer = inject(Renderer2);
  private cdr = inject(ChangeDetectorRef);

  products: Product[] = [];
  categoryOptions: { label: string; value: string }[] = [];
  brandOptions: { label: string; value: string }[] = [];
  loading = true;
  errorMessage: string | null = null;
  filterForm!: FormGroup;

  // Pagination
  totalRecords = 0;
  currentPage = 1;
  pageSize = 25;

  @ViewChild('catDropdown') catDropdown!: Dropdown;
  @ViewChild('brandDropdown') brandDropdown!: Dropdown;

  ngOnInit(): void {
    this.filterForm = this.fb.group({
      searchText: [''],
      filterCategory: [null],
      filterBrand: [null],
    });

    this.loadProducts();
    this.productService.getCategories().subscribe({
      next: data => {
        this.categoryOptions = data.map(c => ({ label: c, value: c }));
        this.cdr.markForCheck();
      },
      error: () => {
        /* Toast shown by error interceptor */
      },
    });
    this.productService.getBrands().subscribe({
      next: data => {
        this.brandOptions = data.map(b => ({ label: b, value: b }));
        this.cdr.markForCheck();
      },
      error: () => {
        /* Toast shown by error interceptor */
      },
    });
  }

  /** Clear the filter text inside a dropdown's search input */
  clearDropdownFilter(dropdown: Dropdown): void {
    if (dropdown?.filterValue) {
      dropdown.filterValue = '';
      dropdown.filterBy = 'label';
      dropdown.onFilterInputChange({ target: { value: '' } });
    }
  }

  /** Set id/name on PrimeNG's internal filter input (fixes Chrome DevTools warning) */
  onDropdownShow(dropdown: Dropdown, filterId: string): void {
    const filterInput = dropdown?.filterViewChild?.nativeElement;
    if (filterInput) {
      this.renderer.setAttribute(filterInput, 'id', filterId);
      this.renderer.setAttribute(filterInput, 'name', filterId);
    }
  }

  /** Called only by Search button or Enter key */
  search(): void {
    this.currentPage = 1;
    this.loadProducts();
  }

  /** Reset all filters and reload */
  resetFilters(): void {
    this.filterForm.reset({ searchText: '', filterCategory: null, filterBrand: null });
    this.currentPage = 1;
    this.loadProducts();
  }

  loadProducts(): void {
    this.loading = true;
    this.errorMessage = null;
    const { searchText, filterCategory, filterBrand } = this.filterForm.value;
    this.productService
      .getProducts(filterCategory || '', filterBrand || '', searchText || '', this.currentPage, this.pageSize)
      .subscribe({
        next: result => {
          this.products = result.items;
          this.totalRecords = result.totalCount;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Failed to load products. Please try again.';
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  onPageChange(event: PaginatorState): void {
    this.currentPage = (event.page ?? 0) + 1;
    this.pageSize = event.rows ?? this.pageSize;
    this.loadProducts();
  }

  toggleActive(product: Product): void {
    this.productService.toggleActive(product.id, !product.isActive).subscribe({
      next: () => {
        product.isActive = !product.isActive;
        this.notification.success(`Product ${product.isActive ? 'activated' : 'deactivated'}.`);
        this.cdr.markForCheck();
      },
      error: () => {
        // Toast shown by error interceptor
      },
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
            this.cdr.markForCheck();
          },
          error: () => {
            // Toast shown by error interceptor
          },
        });
      },
    });
  }
}
