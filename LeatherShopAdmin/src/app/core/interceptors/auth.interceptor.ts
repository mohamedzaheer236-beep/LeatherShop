import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse, HttpEvent } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { BehaviorSubject, Observable, throwError, filter, switchMap, take, catchError } from 'rxjs';

/**
 * JWT auth interceptor:
 * 1. Adds withCredentials to send HttpOnly refresh-token cookie.
 * 2. Attaches in-memory access token as Bearer header.
 * 3. On 401, silently refreshes the access token and retries once.
 *    Concurrent requests are queued until the refresh completes.
 */

let isRefreshing = false;
const refreshResult$ = new BehaviorSubject<string | null>(null);

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  // Always send cookies (refresh token is HttpOnly)
  req = req.clone({ withCredentials: true });

  // Skip token attachment for auth endpoints
  if (isAuthUrl(req.url)) {
    return next(req);
  }

  // Attach access token
  const token = auth.getToken();
  if (token) {
    req = addToken(req, token);
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isAuthUrl(req.url)) {
        return handle401(req, next, auth);
      }
      return throwError(() => error);
    }),
  );
};

function handle401(req: HttpRequest<unknown>, next: HttpHandlerFn, auth: AuthService): Observable<HttpEvent<unknown>> {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshResult$.next(null);

    return auth.refreshAccessToken().pipe(
      switchMap(res => {
        isRefreshing = false;
        const newToken = res.data.token;
        refreshResult$.next(newToken);
        return next(addToken(req, newToken));
      }),
      catchError(err => {
        isRefreshing = false;
        refreshResult$.next(''); // unblock queued requests
        auth.logout();
        return throwError(() => err);
      }),
    );
  }

  // Queue subsequent 401s while a refresh is already in-flight
  return refreshResult$.pipe(
    filter(token => token !== null),
    take(1),
    switchMap(token => {
      if (!token) {
        // Refresh failed — reject queued request
        return throwError(() => new HttpErrorResponse({ status: 401 }));
      }
      return next(addToken(req, token));
    }),
  );
}

function addToken(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({
    withCredentials: true,
    setHeaders: { Authorization: `Bearer ${token}` },
  });
}

function isAuthUrl(url: string): boolean {
  return url.includes('/auth/login') || url.includes('/auth/refresh') || url.includes('/auth/logout');
}
