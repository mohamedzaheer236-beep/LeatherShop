import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, Renderer2, ViewChild, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { trigger, transition, style, animate } from '@angular/animations';
import { ProductService } from '../../services/product.service';
import { Product } from '../../models/product.model';
import { NotificationService } from '../../../../shared/services/notification.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { Dropdown, DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { TooltipModule } from 'primeng/tooltip';
import { CalendarModule } from 'primeng/calendar';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    FormsModule,
    LoadingSpinnerComponent,
    TableModule,
    ButtonModule,
    InputTextModule,
    DropdownModule,
    TagModule,
    ConfirmDialogModule,
    TooltipModule,
    CalendarModule,
  ],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss',
  providers: [ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
  animations: [
    trigger('filterAnimation', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(-12px)' }),
        animate('250ms cubic-bezier(0.4, 0, 0.2, 1)', style({ opacity: 1, transform: 'translateY(0)' })),
      ]),
      transition(':leave', [
        animate('200ms cubic-bezier(0.4, 0, 0.2, 1)', style({ opacity: 0, transform: 'translateY(-8px)' })),
      ]),
    ]),
  ],
})
export class ProductListComponent implements OnInit {
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

  // Pagination & sorting (lazy table)
  totalRecords = 0;
  pageSize = 25;
  sortField = 'createdAt';
  sortOrder = -1;

  // Column filter state
  showFilters = false;
  filters = {
    name: '',
    category: '',
    brand: '',
    priceMin: '',
    priceMax: '',
    stockMin: '',
    stockMax: '',
    isActive: '',
    dateFrom: null as Date | null,
    dateTo: null as Date | null,
  };
  hasActiveFilters = false;

  activeFilterOptions = [
    { label: 'All', value: '' },
    { label: 'Active', value: 'true' },
    { label: 'Inactive', value: 'false' },
  ];

  @ViewChild('catDropdown') catDropdown!: Dropdown;
  @ViewChild('brandDropdown') brandDropdown!: Dropdown;

  ngOnInit(): void {
    this.loadProducts(1);
    this.productService.getCategories().subscribe({
      next: data => {
        this.categoryOptions = [
          { label: 'All', value: '' },
          ...data.map(c => ({ label: c, value: c })),
        ];
        this.cdr.markForCheck();
      },
      error: () => {},
    });
    this.productService.getBrands().subscribe({
      next: data => {
        this.brandOptions = [
          { label: 'All', value: '' },
          ...data.map(b => ({ label: b, value: b })),
        ];
        this.cdr.markForCheck();
      },
      error: () => {},
    });
  }

  /** Set id/name on PrimeNG's internal filter input (fixes Chrome DevTools warning) */
  onDropdownShow(dropdown: Dropdown, filterId: string): void {
    const filterInput = dropdown?.filterViewChild?.nativeElement;
    if (filterInput) {
      this.renderer.setAttribute(filterInput, 'id', filterId);
      this.renderer.setAttribute(filterInput, 'name', filterId);
    }
  }

  // ─── Lazy Load & Filtering ───

  onLazyLoad(event: TableLazyLoadEvent): void {
    const page = Math.floor((event.first ?? 0) / (event.rows ?? this.pageSize)) + 1;
    this.pageSize = event.rows ?? this.pageSize;
    if (event.sortField) {
      this.sortField = event.sortField as string;
      this.sortOrder = event.sortOrder ?? -1;
    }
    this.loadProducts(page);
  }

  applyFilters(): void {
    this.updateHasActiveFilters();
    this.loadProducts(1);
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
    if (!this.showFilters && this.hasActiveFilters) {
      this.resetAll();
    }
    this.cdr.markForCheck();
  }

  resetAll(): void {
    this.sortField = 'createdAt';
    this.sortOrder = -1;
    this.filters = { name: '', category: '', brand: '', priceMin: '', priceMax: '', stockMin: '', stockMax: '', isActive: '', dateFrom: null, dateTo: null };
    this.hasActiveFilters = false;
    this.loadProducts(1);
  }

  loadProducts(page = 1): void {
    this.loading = true;
    this.errorMessage = null;
    this.cdr.markForCheck();
    const sortOrderStr = this.sortOrder === 1 ? 'asc' : 'desc';
    this.productService
      .getProducts(
        this.filters.category || undefined,
        this.filters.brand || undefined,
        undefined,
        page,
        this.pageSize,
        this.sortField,
        sortOrderStr,
        this.getActiveFilters(),
      )
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

  toggleActive(product: Product): void {
    this.productService.toggleActive(product.id, !product.isActive).subscribe({
      next: () => {
        product.isActive = !product.isActive;
        this.notification.success(`Product ${product.isActive ? 'activated' : 'deactivated'}.`);
        this.cdr.markForCheck();
      },
      error: () => {
        this.notification.error('Failed to update product status.');
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
            this.notification.error('Failed to delete product.');
          },
        });
      },
    });
  }

  // ─── Private Helpers ───

  private getActiveFilters(): Record<string, string> | undefined {
    const active: Record<string, string> = {};
    const f = this.filters;
    if (f.name.trim()) active['name'] = f.name.trim();
    if (f.priceMin !== '' && f.priceMin != null) active['priceMin'] = String(f.priceMin);
    if (f.priceMax !== '' && f.priceMax != null) active['priceMax'] = String(f.priceMax);
    if (f.stockMin !== '' && f.stockMin != null) active['stockMin'] = String(f.stockMin);
    if (f.stockMax !== '' && f.stockMax != null) active['stockMax'] = String(f.stockMax);
    if (f.isActive) active['isActive'] = f.isActive;
    if (f.dateFrom) {
      const d = f.dateFrom;
      active['dateFrom'] = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    if (f.dateTo) {
      const d = f.dateTo;
      active['dateTo'] = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    return Object.keys(active).length > 0 ? active : undefined;
  }

  private updateHasActiveFilters(): void {
    const f = this.filters;
    this.hasActiveFilters = !!(f.name.trim() || f.category || f.brand || f.isActive ||
      (f.priceMin !== '' && f.priceMin != null) || (f.priceMax !== '' && f.priceMax != null) ||
      (f.stockMin !== '' && f.stockMin != null) || (f.stockMax !== '' && f.stockMax != null) ||
      f.dateFrom || f.dateTo);
  }
}
