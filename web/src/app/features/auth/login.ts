import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { AuthService } from '../../core/auth/auth.service';
import { PlatformService } from '../../core/config/platform.service';
import { errorCode } from '../../core/http/error-codes';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-login',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    ReactiveFormsModule,
    TranslatePipe,
  ],
  template: `
    <div class="login-page">
      <div class="login-lang">
        <button mat-button type="button" (click)="languages.toggle()">
          <mat-icon>translate</mat-icon>
          {{ languages.language() === 'ar' ? 'English' : 'العربية' }}
        </button>
      </div>
      <form class="login-card" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <span class="shell-logo large" aria-hidden="true"><mat-icon>nightlight</mat-icon></span>
        <h1>{{ platform.name() }}</h1>
        <p class="muted">{{ 'login.subtitle' | translate }}</p>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'login.email' | translate }}</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="username" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'login.password' | translate }}</mat-label>
          <input
            matInput
            type="password"
            formControlName="password"
            autocomplete="current-password"
          />
        </mat-form-field>

        @if (error()) {
          <p class="form-error" role="alert">{{ error() | translate }}</p>
        }
        <button mat-flat-button type="submit" [disabled]="busy()">
          @if (busy()) {
            <mat-spinner diameter="20" />
          } @else {
            {{ 'login.submit' | translate }}
          }
        </button>
      </form>
    </div>
  `,
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly platform = inject(PlatformService);
  protected readonly languages = inject(LanguageService);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.error.set('errors.VALIDATION_FAILED');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        void this.router.navigateByUrl(
          returnUrl && returnUrl.startsWith('/') ? returnUrl : '/clinics',
        );
      },
      error: (e: unknown) => {
        this.busy.set(false);
        const code = errorCode(e);
        this.error.set(
          code === 'INVALID_CREDENTIALS'
            ? 'errors.INVALID_CREDENTIALS'
            : `errors.${code === 'NETWORK' ? 'NETWORK' : 'UNKNOWN'}`,
        );
      },
    });
  }
}
