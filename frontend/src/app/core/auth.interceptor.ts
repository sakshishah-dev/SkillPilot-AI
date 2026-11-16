import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token;
  const authed = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      // An expired or invalid token on a normal call signs the user out. Login failures stay on the form.
      if (err.status === 401 && !req.url.includes('/auth/login') && !req.url.includes('/auth/register')) auth.logout();
      return throwError(() => err);
    })
  );
};

export function errorText(e: unknown, fallback = 'Something went wrong. Please try again.'): string {
  const err = e as HttpErrorResponse;
  const msg = err?.error?.error;
  if (typeof msg === 'string') return msg;
  if (err?.status === 0) return 'Cannot reach the server. Check your connection and that the API is running.';
  if (err?.status === 429) return 'Too many requests. Wait a moment and try again.';
  return fallback;
}
