import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { API_ROOT } from '../config/api-url';
import { AuthService } from './auth.service';

/** Attaches the access token; a 401 on any call except login ends the session. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API_ROOT)) return next(req);
  const auth = inject(AuthService);
  const token = auth.accessToken();
  const isLogin = req.url.endsWith('/auth/login');
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isLogin && token) {
        auth.expire();
      }
      return throwError(() => error);
    }),
  );
};
