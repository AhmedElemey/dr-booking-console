import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ConsoleApi } from '../../core/api/console.api';
import { Admin } from '../../core/api/models';
import { AuthService } from '../../core/auth/auth.service';
import { fieldErrors } from '../../core/http/error-codes';
import { ToastService } from '../../core/http/toast.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ConfirmDialog } from '../../shared/confirm-dialog';

@Component({
  selector: 'app-admins-page',
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    ReactiveFormsModule,
    TranslatePipe,
  ],
  template: `
    <div class="page-head">
      <div>
        <h1>{{ 'admins.title' | translate }}</h1>
        <p class="muted">{{ 'admins.subtitle' | translate }}</p>
      </div>
      <button mat-flat-button type="button" (click)="adding.set(!adding())">
        <mat-icon>{{ adding() ? 'close' : 'person_add' }}</mat-icon
        >{{ (adding() ? 'common.cancel' : 'admins.add') | translate }}
      </button>
    </div>

    @if (adding()) {
      <form class="card add-admin" [formGroup]="form" (ngSubmit)="add()" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'admins.name' | translate }}</mat-label>
          <input matInput formControlName="displayName" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'login.email' | translate }}</mat-label>
          <input matInput type="email" formControlName="email" dir="ltr" autocomplete="off" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'login.password' | translate }}</mat-label>
          <input
            matInput
            type="password"
            formControlName="password"
            dir="ltr"
            autocomplete="new-password"
          />
          <mat-hint>{{ 'admins.passwordHint' | translate }}</mat-hint>
        </mat-form-field>
        <button mat-flat-button type="submit" [disabled]="form.invalid">
          {{ 'admins.create' | translate }}
        </button>
        @if (error()) {
          <p class="form-error" role="alert">{{ error() }}</p>
        }
      </form>
    }

    <table class="data-table card">
      <thead>
        <tr>
          <th>{{ 'admins.name' | translate }}</th>
          <th>{{ 'login.email' | translate }}</th>
          <th>{{ 'admins.status' | translate }}</th>
          <th>{{ 'clinics.created' | translate }}</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        @for (a of admins(); track a.id) {
          <tr>
            <td>
              {{ a.displayName }}
              @if (a.id === auth.admin()?.id) {
                <span class="chip status-draft">{{ 'admins.you' | translate }}</span>
              }
            </td>
            <td dir="ltr">{{ a.email }}</td>
            <td>
              <span
                class="chip"
                [class]="a.isActive ? 'chip status-active' : 'chip status-suspended'"
                >{{ (a.isActive ? 'admins.active' : 'admins.inactive') | translate }}</span
              >
            </td>
            <td>{{ a.createdAt | date: 'mediumDate' : undefined : languages.language() }}</td>
            <td class="row-actions">
              @if (a.isActive && a.id !== auth.admin()?.id) {
                <button mat-button type="button" (click)="deactivate(a)">
                  {{ 'admins.deactivate' | translate }}
                </button>
              }
              @if (!a.isActive) {
                <button mat-button type="button" (click)="activate(a)">
                  {{ 'admins.activate' | translate }}
                </button>
              }
            </td>
          </tr>
        }
      </tbody>
    </table>
  `,
})
export class AdminsPage {
  private readonly api = inject(ConsoleApi);
  private readonly fb = inject(FormBuilder);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthService);
  protected readonly languages = inject(LanguageService);

  protected readonly admins = signal<Admin[]>([]);
  protected readonly adding = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = this.fb.nonNullable.group({
    displayName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(10)]],
  });

  constructor() {
    this.reload();
  }

  private reload(): void {
    this.api.admins().subscribe((a) => this.admins.set(a));
  }

  protected add(): void {
    const { email, password, displayName } = this.form.getRawValue();
    this.error.set(null);
    this.api.createAdmin(email, password, displayName).subscribe({
      next: () => {
        this.toast.success('admins.created');
        this.form.reset();
        this.adding.set(false);
        this.reload();
      },
      error: (e: unknown) => this.error.set(Object.values(fieldErrors(e)).flat().join(' ') || null),
    });
  }

  protected async deactivate(a: Admin): Promise<void> {
    const ok = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, unknown, boolean>(ConfirmDialog, {
          data: {
            titleKey: 'admins.deactivateTitle',
            messageKey: 'admins.deactivateMessage',
            confirmKey: 'admins.deactivate',
            params: { email: a.email },
          },
        })
        .afterClosed(),
    );
    if (ok) this.api.deactivateAdmin(a.id).subscribe(() => this.reload());
  }

  protected activate(a: Admin): void {
    this.api.activateAdmin(a.id).subscribe(() => this.reload());
  }
}
