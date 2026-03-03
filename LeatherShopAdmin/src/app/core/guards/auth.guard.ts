import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Auth guard — blocks navigation to protected routes when not authenticated.
 * On page reload the in-memory access token is lost, so we attempt a silent
 * refresh using the HttpOnly refresh-token cookie before rejecting.
 */
export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isLoggedIn()) {
    return true;
  }

  // If a previous session exists, try restoring it from the refresh token cookie
  if (auth.hasPriorSession()) {
    const restored = await auth.tryRestoreSession();
    if (restored) return true;
  }

  router.navigate(['/login']);
  return false;
};
