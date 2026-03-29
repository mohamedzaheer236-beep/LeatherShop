import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { Dashboard } from '../models/dashboard.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class DashboardService {
  private http = inject(HttpClient);

  private baseUrl = `${environment.apiUrl}/dashboard`;

  getDashboard(from?: Date, to?: Date): Observable<Dashboard> {
    let params = new HttpParams();
    if (from) params = params.set('from', from.toISOString());
    if (to) params = params.set('to', to.toISOString());
    return this.http.get<ApiResponse<Dashboard>>(this.baseUrl, { params }).pipe(map(res => res.data));
  }
}
