import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { Customer, CreateCustomer, UpdateCustomer } from '../models/customer.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class CustomerService {
  private baseUrl = `${environment.apiUrl}/customers`;

  constructor(private http: HttpClient) {}

  getCustomers(subscribedOnly?: boolean, search?: string): Observable<Customer[]> {
    let params = new HttpParams();
    if (subscribedOnly) params = params.set('subscribedOnly', 'true');
    if (search) params = params.set('search', search);
    return this.http.get<any>(this.baseUrl, { params }).pipe(map(res => res.data));
  }

  createCustomer(customer: CreateCustomer): Observable<any> {
    return this.http.post<any>(this.baseUrl, customer).pipe(map(res => res.data));
  }

  updateCustomer(id: number, customer: UpdateCustomer): Observable<Customer> {
    return this.http.put<any>(`${this.baseUrl}/${id}`, customer).pipe(map(res => res.data));
  }

  deleteCustomer(id: number): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/${id}`).pipe(map(res => res));
  }

  bulkImportCustomers(customers: CreateCustomer[]): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/import`, { customers }).pipe(map(res => res.data));
  }

  toggleSubscription(id: number, isSubscribed: boolean): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/subscribe`, JSON.stringify(isSubscribed), {
      headers: { 'Content-Type': 'application/json' }
    });
  }

  getSubscriberCount(): Observable<{ subscriberCount: number; totalCount: number }> {
    return this.http.get<any>(`${this.baseUrl}/count`).pipe(map(res => res.data));
  }
}
