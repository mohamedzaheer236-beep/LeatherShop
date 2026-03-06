import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { Product, CreateProduct } from '../models/product.model';
import { PaginatedResult } from '../../../core/models/paginated-result.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class ProductService {
  private http = inject(HttpClient);

  private baseUrl = `${environment.apiUrl}/products`;

  getProducts(
    category?: string,
    brand?: string,
    search?: string,
    page = 1,
    pageSize = 25,
  ): Observable<PaginatedResult<Product>> {
    let params = new HttpParams().set('page', page.toString()).set('pageSize', pageSize.toString());
    if (category) params = params.set('category', category);
    if (brand) params = params.set('brand', brand);
    if (search) params = params.set('search', search);
    return this.http.get<ApiResponse<PaginatedResult<Product>>>(this.baseUrl, { params }).pipe(map(res => res.data));
  }

  getProduct(id: number): Observable<Product> {
    return this.http.get<ApiResponse<Product>>(`${this.baseUrl}/${id}`).pipe(map(res => res.data));
  }

  createProduct(product: CreateProduct): Observable<Product> {
    return this.http.post<ApiResponse<Product>>(this.baseUrl, product).pipe(map(res => res.data));
  }

  updateProduct(id: number, product: Partial<Product>): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, product);
  }

  toggleActive(id: number, isActive: boolean): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, { isActive });
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  getCategories(): Observable<string[]> {
    return this.http.get<ApiResponse<string[]>>(`${this.baseUrl}/categories`).pipe(map(res => res.data));
  }

  getBrands(): Observable<string[]> {
    return this.http.get<ApiResponse<string[]>>(`${this.baseUrl}/brands`).pipe(map(res => res.data));
  }

  checkName(name: string, excludeId?: number): Observable<boolean> {
    let params = new HttpParams().set('name', name);
    if (excludeId) params = params.set('excludeId', excludeId.toString());
    return this.http.get<ApiResponse<boolean>>(`${this.baseUrl}/check-name`, { params }).pipe(map(res => res.data));
  }

  uploadImages(files: File[]): Observable<string[]> {
    const formData = new FormData();
    files.forEach(f => formData.append('files', f));
    return this.http.post<ApiResponse<string[]>>(`${this.baseUrl}/upload-images`, formData).pipe(map(res => res.data));
  }

  uploadVideo(file: File): Observable<string> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ApiResponse<string>>(`${this.baseUrl}/upload-video`, formData).pipe(map(res => res.data));
  }
}
