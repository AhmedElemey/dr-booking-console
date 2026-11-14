import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, map, of, tap } from 'rxjs';

import { ConsoleApi } from '../api/console.api';
import { Admin } from '../api/models';

const TOKEN_KEY = 'console.token';

/** Session state. The JWT is also kept in localStorage so a reload keeps the session (8 h expiry). */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ConsoleApi);
  private readonly router = inject(Router);

  readonly accessToken = signal<string | null>(this.readToken());
  readonly admin = signal<Admin | null>(null);
  readonly isAuthenticated = computed(() => this.admin() !== null);

  private readToken(): string | null {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  }

  private writeToken(token: string | null): void {
    try {
      if (token) localStorage.setItem(TOKEN_KEY, token);
      else localStorage.removeItem(TOKEN_KEY);
    } catch {
      // Storage blocked: the session just will not survive a reload.
    }
  }

  login(email: string, password: string): Observable<Admin> {
    return this.api.login(email, password).pipe(
      tap((res) => {
        this.accessToken.set(res.accessToken);
        this.writeToken(res.accessToken);
        this.admin.set(res.admin);
      }),
      map((res) => res.admin),
    );
  }

  /** Resolves the signed-in admin, restoring the session from the stored token. */
  load(): Observable<Admin | null> {
    const current = this.admin();
    if (current) return of(current);
    if (!this.accessToken()) return of(null);
    return this.api.me().pipe(
      tap((admin) => this.admin.set(admin)),
      catchError(() => {
        this.clear();
        return of(null);
      }),
    );
  }

  logout(): void {
    this.clear();
    void this.router.navigate(['/login']);
  }

  /** Called when the API rejects the token. */
  expire(): void {
    this.clear();
    if (!this.router.url.startsWith('/login')) {
      void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
    }
  }

  private clear(): void {
    this.accessToken.set(null);
    this.admin.set(null);
    this.writeToken(null);
  }
}
