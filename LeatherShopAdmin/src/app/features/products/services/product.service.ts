import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { Product, CreateProduct } from '../models/product.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private baseUrl = `${environment.apiUrl}/products`;

  constructor(private http: HttpClient) {}

  getProducts(category?: string, brand?: string, search?: string): Observable<Product[]> {
    let params = new HttpParams();
    if (category) params = params.set('category', category);
    if (brand) params = params.set('brand', brand);
    if (search) params = params.set('search', search);
    return this.http.get<any>(this.baseUrl, { params }).pipe(map(res => res.data));
  }

  getProduct(id: number): Observable<Product> {
    return this.http.get<any>(`${this.baseUrl}/${id}`).pipe(map(res => res.data));
  }

  createProduct(product: CreateProduct): Observable<Product> {
    return this.http.post<any>(this.baseUrl, product).pipe(map(res => res.data));
  }

  updateProduct(id: number, product: Partial<Product>): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, product);
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  getCategories(): Observable<string[]> {
    return this.http.get<any>(`${this.baseUrl}/categories`).pipe(map(res => res.data));
  }

  getBrands(): Observable<string[]> {
    return this.http.get<any>(`${this.baseUrl}/brands`).pipe(map(res => res.data));
  }
}
