import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
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

  getDashboard(): Observable<Dashboard> {
    return this.http.get<ApiResponse<Dashboard>>(this.baseUrl).pipe(map(res => res.data));
  }
}
