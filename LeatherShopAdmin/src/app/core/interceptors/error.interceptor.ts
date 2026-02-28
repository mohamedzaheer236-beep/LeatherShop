import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../../shared/services/notification.service';
import { AuthService } from '../services/auth.service';
import { SignalRService } from '../services/signalr.service';

/**
 * HTTP error interceptor — catches all API errors and shows toast notifications.
 * On 401, clears auth state and redirects to login.
 * Registered in app.config.ts via withInterceptors([]).
 */
let isLoggingOut = false;

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notification = inject(NotificationService);
  const auth = inject(AuthService);
  const signalR = inject(SignalRService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // On 401, redirect to login (skip if already on login page, prevent concurrent logouts)
      if (error.status === 401 && !req.url.includes('/auth/login')) {
        if (!isLoggingOut) {
          isLoggingOut = true;
          signalR.stop(); // fire-and-forget — close hub before clearing tokens
          auth.logout();
          // Reset flag after a short delay to allow future logouts if user logs in again
          setTimeout(() => (isLoggingOut = false), 2000);
        }
        return throwError(() => error);
      }

      // Skip toast for login failures — login component shows inline error
      if (error.status === 401 && req.url.includes('/auth/login')) {
        return throwError(() => error);
      }

      let message = 'An unexpected error occurred.';

      if (error.status === 0) {
        message = 'Unable to connect to server. Please check if the API is running.';
      } else if (error.error?.message) {
        message = error.error.message;
      } else {
        switch (error.status) {
          case 400: message = 'Bad request. Please check your input.'; break;
          case 401: message = 'Session expired. Please log in again.'; break;
          case 403: message = 'Access denied.'; break;
          case 404: message = 'Resource not found.'; break;
          case 409: message = 'Conflict. Resource already exists.'; break;
          case 500: message = 'Server error. Please try again later.'; break;
        }
      }

      notification.error(message);
      return throwError(() => error);
    })
  );
};
