import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, BehaviorSubject, tap, firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { LoginData } from '../../features/auth/models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private readonly USER_KEY = 'ls_auth_user';

  /** Access token stored in-memory only — never in localStorage */
  private accessToken: string | null = null;
  private accessTokenExpiry: Date | null = null;

  /** Reactive auth state — components subscribe to react to login/logout/session restore */
  private readonly _isAuthenticated$ = new BehaviorSubject<boolean>(false);
  readonly isAuthenticated$ = this._isAuthenticated$.asObservable();

  login(username: string, password: string): Observable<ApiResponse<LoginData>> {
    return this.http
      .post<ApiResponse<LoginData>>(`${environment.apiUrl}/auth/login`, { username, password }, { withCredentials: true })
      .pipe(
        tap(res => {
          if (res.success && res.data) {
            this.setSession(res.data);
          }
        }),
      );
  }

  /**
   * Get a new access token using the HttpOnly refresh token cookie.
   * Called by the auth interceptor on 401 and by the guard on page reload.
   */
  refreshAccessToken(): Observable<ApiResponse<LoginData>> {
    return this.http.post<ApiResponse<LoginData>>(`${environment.apiUrl}/auth/refresh`, {}, { withCredentials: true }).pipe(
      tap(res => {
        if (res.success && res.data) {
          this.setSession(res.data);
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
    this._isAuthenticated$.next(false);
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

  /** Centralizes token + localStorage writes and emits auth state change */
  private setSession(data: LoginData): void {
    this.accessToken = data.token;
    this.accessTokenExpiry = new Date(data.expiresAt);
    localStorage.setItem(this.USER_KEY, data.username);
    this._isAuthenticated$.next(true);
  }
}
