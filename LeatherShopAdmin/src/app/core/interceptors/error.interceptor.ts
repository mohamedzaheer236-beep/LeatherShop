import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../../shared/services/notification.service';

/**
 * HTTP error interceptor — catches API errors and shows toast notifications.
 * 401 handling (refresh + logout) lives in the auth interceptor, not here.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notification = inject(NotificationService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // 401 is handled by authInterceptor (refresh → retry → logout).
      // Login failures are handled inline by the login component.
      if (error.status === 401) {
        return throwError(() => error);
      }

      let message = 'An unexpected error occurred.';

      if (error.status === 0) {
        message = 'Unable to connect to server. Please check if the API is running.';
      } else if (error.error?.message) {
        message = error.error.message;
      } else {
        switch (error.status) {
          case 400:
            message = 'Bad request. Please check your input.';
            break;
          case 403:
            message = 'Access denied.';
            break;
          case 404:
            message = 'Resource not found.';
            break;
          case 409:
            message = 'Conflict. Resource already exists.';
            break;
          case 500:
            message = 'Server error. Please try again later.';
            break;
        }
      }

      notification.error(message);
      return throwError(() => error);
    }),
  );
};
