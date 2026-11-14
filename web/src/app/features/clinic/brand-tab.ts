import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe } from '@ngx-translate/core';

import { ConsoleApi } from '../../core/api/console.api';
import { Clinic } from '../../core/api/models';
import { isHexColor } from '../../core/colors';
import { fieldErrors } from '../../core/http/error-codes';
import { ToastService } from '../../core/http/toast.service';
import { BrandPreview } from '../../shared/brand-preview';

/** Edit identity and brand; saved with PATCH. For an Active clinic the new brand is pushed with "Sync config". */
@Component({
  selector: 'app-brand-tab',
  imports: [
    BrandPreview,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    ReactiveFormsModule,
    TranslatePipe,
  ],
  template: `
    <div class="brand-step">
      <form [formGroup]="form" (ngSubmit)="save()" class="brand-form" novalidate>
        <div class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.nameEn' | translate }}</mat-label>
            <input matInput formControlName="nameEn" dir="ltr" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.nameAr' | translate }}</mat-label>
            <input matInput formControlName="nameAr" dir="rtl" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.taglineEn' | translate }}</mat-label>
            <input matInput formControlName="taglineEn" dir="ltr" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.taglineAr' | translate }}</mat-label>
            <input matInput formControlName="taglineAr" dir="rtl" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.supportPhone' | translate }}</mat-label>
            <input matInput formControlName="supportPhone" dir="ltr" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.domain' | translate }}</mat-label>
            <input matInput formControlName="domain" dir="ltr" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.androidId' | translate }}</mat-label>
            <input matInput formControlName="androidApplicationId" dir="ltr" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.iosId' | translate }}</mat-label>
            <input matInput formControlName="iosBundleId" dir="ltr" />
          </mat-form-field>
        </div>
        <div class="color-row">
          <label class="color-field">
            <span>{{ 'brand.primary' | translate }}</span>
            <span class="color-inputs">
              <input
                type="color"
                [value]="colorOf('primaryColor')"
                (input)="setColor('primaryColor', $any($event.target).value)"
                [attr.aria-label]="'brand.primary' | translate"
              />
              <input class="hex" formControlName="primaryColor" dir="ltr" maxlength="7" />
            </span>
          </label>
          <label class="color-field">
            <span>{{ 'brand.accent' | translate }}</span>
            <span class="color-inputs">
              <input
                type="color"
                [value]="colorOf('accentColor')"
                (input)="setColor('accentColor', $any($event.target).value)"
                [attr.aria-label]="'brand.accent' | translate"
              />
              <input class="hex" formControlName="accentColor" dir="ltr" maxlength="7" />
            </span>
          </label>
        </div>
        <div class="logo-actions">
          <label class="mat-mdc-outlined-button mdc-button mdc-button--outlined file-btn">
            <mat-icon>upload</mat-icon>
            <span>{{ 'brand.upload' | translate }}</span>
            <input
              type="file"
              accept=".svg,image/svg+xml"
              class="visually-hidden"
              (change)="onLogo($event)"
            />
          </label>
          @if (logoSvg()) {
            <button mat-button type="button" (click)="logoSvg.set(null)">
              {{ 'brand.removeLogo' | translate }}
            </button>
          }
        </div>
        @if (logoError()) {
          <p class="form-error" role="alert">{{ logoError() | translate }}</p>
        }
        @if (serverError()) {
          <p class="form-error" role="alert">{{ serverError() }}</p>
        }
        <div class="step-actions">
          <button mat-flat-button type="submit" [disabled]="form.invalid || !dirty() || saving()">
            {{ 'common.save' | translate }}
          </button>
        </div>
        @if (clinic().status === 'Active') {
          <p class="info-note"><mat-icon>info</mat-icon>{{ 'brand.syncHint' | translate }}</p>
        }
      </form>
      <app-brand-preview [brand]="values()" />
    </div>
  `,
})
export class BrandTab {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ConsoleApi);
  private readonly toast = inject(ToastService);

  readonly clinic = input.required<Clinic>();
  readonly changed = output<void>();

  protected readonly form = this.fb.nonNullable.group({
    nameEn: ['', Validators.required],
    nameAr: ['', Validators.required],
    taglineEn: [''],
    taglineAr: [''],
    supportPhone: ['', [Validators.required, Validators.pattern(/^\+?[0-9][0-9 -]{5,19}$/)]],
    domain: ['', Validators.pattern(/^([a-z0-9]([a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,}$/)],
    androidApplicationId: ['', Validators.required],
    iosBundleId: ['', Validators.required],
    primaryColor: ['', [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
    accentColor: ['', [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
  });

  protected readonly logoSvg = signal<string | null>(null);
  protected readonly logoError = signal<string | null>(null);
  protected readonly serverError = signal<string | null>(null);
  protected readonly saving = signal(false);
  private readonly formValue = signal(this.form.getRawValue());
  private readonly loaded = signal('');
  private lastSignature = '';

  protected readonly values = computed(() => ({ ...this.formValue(), logoSvg: this.logoSvg() }));
  protected readonly dirty = computed(
    () => JSON.stringify([this.formValue(), this.logoSvg()]) !== this.loaded(),
  );

  constructor() {
    this.form.valueChanges.subscribe(() => this.formValue.set(this.form.getRawValue()));
    effect(() => {
      const c = this.clinic();
      // Reloaded whenever the server copy changes (after a save or a refresh).
      const signature = `${c.updatedAt}`;
      if (signature === this.lastSignature) return;
      this.lastSignature = signature;
      this.form.reset({
        nameEn: c.nameEn,
        nameAr: c.nameAr,
        taglineEn: c.taglineEn,
        taglineAr: c.taglineAr,
        supportPhone: c.supportPhone,
        domain: c.domain,
        androidApplicationId: c.androidApplicationId,
        iosBundleId: c.iosBundleId,
        primaryColor: c.primaryColor,
        accentColor: c.accentColor,
      });
      this.logoSvg.set(c.logoSvg);
      this.formValue.set(this.form.getRawValue());
      this.loaded.set(JSON.stringify([this.formValue(), this.logoSvg()]));
    });
  }

  protected colorOf(control: 'primaryColor' | 'accentColor'): string {
    const value = this.form.controls[control].value;
    return isHexColor(value) ? value : '#000000';
  }

  protected setColor(control: 'primaryColor' | 'accentColor', value: string): void {
    this.form.controls[control].setValue(value.toUpperCase());
  }

  protected onLogo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.logoError.set(null);
    if (file.size > 100_000) {
      this.logoError.set('brand.logoTooBig');
      return;
    }
    void file.text().then((text) => {
      const ok =
        /^\s*(<\?xml[^>]*\?>\s*)?(<!--[\s\S]*?-->\s*)*<svg[\s>]/i.test(text) &&
        !/<script|\son[a-z]+\s*=|javascript:/i.test(text);
      if (ok) this.logoSvg.set(text.trim());
      else this.logoError.set('brand.logoInvalid');
    });
  }

  protected save(): void {
    this.saving.set(true);
    this.serverError.set(null);
    this.api
      .updateClinic(this.clinic().id, { ...this.form.getRawValue(), logoSvg: this.logoSvg() ?? '' })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.success('brand.saved');
          this.changed.emit();
        },
        error: (e: unknown) => {
          this.saving.set(false);
          this.serverError.set(Object.values(fieldErrors(e)).flat().join(' ') || null);
        },
      });
  }
}
