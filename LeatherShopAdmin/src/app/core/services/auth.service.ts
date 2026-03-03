import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, firstValueFrom } from 'rxjs';
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
  private http = inject(HttpClient);
  private router = inject(Router);

  private readonly USER_KEY = 'ls_auth_user';

  /** Access token stored in-memory only — never in localStorage */
  private accessToken: string | null = null;
  private accessTokenExpiry: Date | null = null;

  login(username: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiUrl}/auth/login`, { username, password }, { withCredentials: true })
      .pipe(
        tap(res => {
          if (res.success && res.data) {
            this.accessToken = res.data.token;
            this.accessTokenExpiry = new Date(res.data.expiresAt);
            localStorage.setItem(this.USER_KEY, res.data.username);
          }
        }),
      );
  }

  /**
   * Get a new access token using the HttpOnly refresh token cookie.
   * Called by the auth interceptor on 401 and by the guard on page reload.
   */
  refreshAccessToken(): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/refresh`, {}, { withCredentials: true }).pipe(
      tap(res => {
        if (res.success && res.data) {
          this.accessToken = res.data.token;
          this.accessTokenExpiry = new Date(res.data.expiresAt);
          localStorage.setItem(this.USER_KEY, res.data.username);
        }
      }),
    );
  }

  /**
   * Attempt to restore session on page reload using the refresh token cookie.
   * Returns true if a valid access token was obtained.
   */
  async tryRestoreSession(): Promise<boolean> {
    try {
      const res = await firstValueFrom(this.refreshAccessToken());
      return res.success;
    } catch {
      this.clearSession();
      return false;
    }
  }

  /** Call server to revoke refresh token cookie (fire-and-forget) */
  serverLogout(): void {
    this.http.post(`${environment.apiUrl}/auth/logout`, {}, { withCredentials: true }).subscribe();
  }

  /** Clear in-memory tokens and localStorage without navigating or API calls */
  clearSession(): void {
    this.accessToken = null;
    this.accessTokenExpiry = null;
    localStorage.removeItem(this.USER_KEY);
  }

  /** Immediate logout — revokes token, clears session, navigates to login */
  logout(): void {
    this.serverLogout();
    this.clearSession();
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.accessToken;
  }

  getUsername(): string | null {
    return localStorage.getItem(this.USER_KEY);
  }

  isLoggedIn(): boolean {
    return !!this.accessToken && !!this.accessTokenExpiry && this.accessTokenExpiry > new Date();
  }

  /** Returns true if there was a previous session that might be restorable via refresh token */
  hasPriorSession(): boolean {
    return !!localStorage.getItem(this.USER_KEY);
  }
}
