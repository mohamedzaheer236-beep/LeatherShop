import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginResponse {
  success: boolean;
  message: string;
  data: {
    token: string;
    username: string;
    expiresAt: string;
  };
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY = 'ls_auth_token';
  private readonly USER_KEY = 'ls_auth_user';
  private readonly EXPIRES_KEY = 'ls_auth_expires';

  constructor(private http: HttpClient, private router: Router) {}

  login(username: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, { username, password }).pipe(
      tap(res => {
        if (res.success && res.data) {
          localStorage.setItem(this.TOKEN_KEY, res.data.token);
          localStorage.setItem(this.USER_KEY, res.data.username);
          localStorage.setItem(this.EXPIRES_KEY, res.data.expiresAt);
        }
      })
    );
  }

  /** Clear tokens without navigating (used by navbar safe-logout flow) */
  clearSession(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    localStorage.removeItem(this.EXPIRES_KEY);
  }

  /** Immediate logout — clears tokens and navigates (used by error interceptor on 401) */
  logout(): void {
    this.clearSession();
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  getUsername(): string | null {
    return localStorage.getItem(this.USER_KEY);
  }

  isLoggedIn(): boolean {
    const token = this.getToken();
    const expires = localStorage.getItem(this.EXPIRES_KEY);
    if (!token || !expires) return false;
    return new Date(expires) > new Date();
  }
}
