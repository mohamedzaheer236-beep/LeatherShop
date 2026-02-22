import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../../shared/services/notification.service';

/**
 * HTTP error interceptor — catches all API errors and shows toast notifications.
 * Registered in app.config.ts via withInterceptors([]).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notification = inject(NotificationService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let message = 'An unexpected error occurred.';

      if (error.status === 0) {
        message = 'Unable to connect to server. Please check if the API is running.';
      } else if (error.error?.message) {
        message = error.error.message;
      } else {
        switch (error.status) {
          case 400: message = 'Bad request. Please check your input.'; break;
          case 401: message = 'Unauthorized. Please log in.'; break;
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
