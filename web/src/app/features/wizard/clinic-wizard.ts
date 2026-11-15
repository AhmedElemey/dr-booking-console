import { JsonPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatStepperModule } from '@angular/material/stepper';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { switchMap } from 'rxjs';

import { ConsoleApi } from '../../core/api/console.api';
import { ClinicDraft, ConfigPreview, FeatureDefinition } from '../../core/api/models';
import { isHexColor } from '../../core/colors';
import { PlatformService } from '../../core/config/platform.service';
import { errorCode, fieldErrors } from '../../core/http/error-codes';
import { LanguageService } from '../../core/i18n/language.service';
import { localized } from '../../core/i18n/localized';
import { BrandPreview } from '../../shared/brand-preview';

const GROUP_ORDER = [
  'Channels',
  'Booking',
  'Notifications',
  'Language',
  'Organisation',
  'Payments',
  'Records',
  'Growth',
  'Insight',
];
const MAX_LOGO_BYTES = 100_000;

export function slugify(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40)
    .replace(/-+$/g, '');
}

/**
 * The Android id is also the payment-return URL scheme (`<id>://payment-return`) and a URI scheme
 * cannot contain `_`, so it is lowercase letters and digits only (the API enforces the same).
 */
export const ANDROID_APP_ID_PATTERN = /^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$/;

export function defaultAppId(slug: string): string {
  const compact = slug.replace(/-/g, '');
  return compact ? `com.${/^\d/.test(compact) ? 'c' : ''}${compact}.app` : '';
}

@Component({
  selector: 'app-clinic-wizard',
  imports: [
    BrandPreview,
    JsonPipe,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatStepperModule,
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
  ],
  templateUrl: './clinic-wizard.html',
})
export class ClinicWizard {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ConsoleApi);
  private readonly router = inject(Router);
  protected readonly languages = inject(LanguageService);
  protected readonly platform = inject(PlatformService);

  protected readonly identity = this.fb.nonNullable.group({
    nameEn: ['', [Validators.required, Validators.maxLength(100)]],
    nameAr: ['', [Validators.required, Validators.maxLength(100)]],
    slug: [
      '',
      [
        Validators.required,
        Validators.maxLength(40),
        Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/),
      ],
    ],
    taglineEn: ['', Validators.maxLength(200)],
    taglineAr: ['', Validators.maxLength(200)],
    supportPhone: ['', [Validators.required, Validators.pattern(/^\+?[0-9][0-9 -]{5,19}$/)]],
    domain: ['', Validators.pattern(/^([a-z0-9]([a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,}$/)],
    androidApplicationId: ['', [Validators.required, Validators.pattern(ANDROID_APP_ID_PATTERN)]],
    iosBundleId: [
      '',
      [
        Validators.required,
        Validators.pattern(/^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$/),
      ],
    ],
  });

  protected readonly brand = this.fb.nonNullable.group({
    primaryColor: ['#3F3CCF', [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
    accentColor: ['#0E9F9A', [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
  });

  protected readonly logoSvg = signal<string | null>(null);
  protected readonly logoError = signal<string | null>(null);
  protected readonly catalog = signal<FeatureDefinition[]>([]);
  protected readonly features = signal<Record<string, boolean>>({});
  protected readonly preview = signal<ConfigPreview | null>(null);
  protected readonly previewError = signal<string | null>(null);
  protected readonly previewLoading = signal(false);
  protected readonly dryRun = signal(true);
  protected readonly submitting = signal(false);

  // Mirrors of the form values so the live preview and review react to typing.
  private readonly identityValue = signal(this.identity.getRawValue());
  private readonly brandValue = signal(this.brand.getRawValue());

  protected readonly brandValues = computed(() => ({
    nameAr: this.identityValue().nameAr,
    nameEn: this.identityValue().nameEn,
    taglineAr: this.identityValue().taglineAr,
    taglineEn: this.identityValue().taglineEn,
    primaryColor: this.brandValue().primaryColor,
    accentColor: this.brandValue().accentColor,
    logoSvg: this.logoSvg(),
  }));

  protected readonly groups = computed(() => {
    const byGroup = new Map<string, FeatureDefinition[]>();
    for (const f of this.catalog()) byGroup.set(f.group, [...(byGroup.get(f.group) ?? []), f]);
    return GROUP_ORDER.filter((g) => byGroup.has(g)).map((g) => ({
      group: g,
      items: byGroup.get(g)!,
    }));
  });

  protected readonly forcedDryRun = computed(() => this.platform.provisioningMode() === 'DryRun');

  private slugEdited = false;
  private androidEdited = false;
  private iosEdited = false;

  constructor() {
    this.api.catalog().subscribe((catalog) => {
      this.catalog.set(catalog);
      this.features.set(
        Object.fromEntries(catalog.map((f) => [f.key, f.available && f.defaultEnabled])),
      );
    });

    this.identity.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.identityValue.set(this.identity.getRawValue()));
    this.brand.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.brandValue.set(this.brand.getRawValue()));

    const c = this.identity.controls;
    c.nameEn.valueChanges.pipe(takeUntilDestroyed()).subscribe((name) => {
      if (!this.slugEdited) c.slug.setValue(slugify(name));
    });
    c.slug.valueChanges.pipe(takeUntilDestroyed()).subscribe((slug) => {
      if (!this.androidEdited)
        c.androidApplicationId.setValue(defaultAppId(slug), { emitEvent: false });
      if (!this.iosEdited) c.iosBundleId.setValue(defaultAppId(slug), { emitEvent: false });
      this.identityValue.set(this.identity.getRawValue());
    });
  }

  protected markSlugEdited(): void {
    this.slugEdited = true;
  }

  protected markAppIdEdited(which: 'android' | 'ios'): void {
    if (which === 'android') this.androidEdited = true;
    else this.iosEdited = true;
  }

  protected featureName(f: FeatureDefinition): string {
    return localized(this.languages.language(), f.nameAr, f.nameEn);
  }

  protected setupNote(f: FeatureDefinition): string | null {
    return localized(this.languages.language(), f.setupNoteAr ?? '', f.setupNoteEn ?? '') || null;
  }

  protected featureDescription(f: FeatureDefinition): string {
    return localized(this.languages.language(), f.descriptionAr, f.descriptionEn);
  }

  protected toggle(key: string, enabled: boolean): void {
    this.features.update((f) => ({ ...f, [key]: enabled }));
  }

  protected setColor(control: 'primaryColor' | 'accentColor', value: string): void {
    this.brand.controls[control].setValue(value.toUpperCase());
  }

  protected colorOf(control: 'primaryColor' | 'accentColor'): string {
    const value = this.brand.controls[control].value;
    return isHexColor(value) ? value : '#000000';
  }

  protected onLogo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.logoError.set(null);
    if (file.size > MAX_LOGO_BYTES) {
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

  protected removeLogo(): void {
    this.logoSvg.set(null);
    this.logoError.set(null);
  }

  /** Builds the request from the three forms; the one place the wizard's state becomes a ClinicDraft. */
  protected draft(): ClinicDraft {
    return {
      ...this.identity.getRawValue(),
      ...this.brand.getRawValue(),
      logoSvg: this.logoSvg(),
      features: this.features(),
    };
  }

  protected onStep(index: number): void {
    if (index === 3) this.loadPreview();
  }

  private loadPreview(): void {
    this.preview.set(null);
    this.previewError.set(null);
    this.previewLoading.set(true);
    this.api.previewClinic(this.draft()).subscribe({
      next: (p) => {
        this.preview.set(p);
        this.previewLoading.set(false);
      },
      error: (e: unknown) => {
        this.previewLoading.set(false);
        const code = errorCode(e);
        this.previewError.set(
          code === 'SLUG_TAKEN'
            ? 'wizard.slugTaken'
            : code === 'VALIDATION_FAILED'
              ? 'wizard.invalid'
              : `errors.${code}`,
        );
        if (code === 'SLUG_TAKEN') this.identity.controls.slug.setErrors({ server: true });
        for (const [field, messages] of Object.entries(fieldErrors(e))) {
          const control = (
            this.identity.controls as Record<string, { setErrors(e: object): void } | undefined>
          )[field];
          control?.setErrors({ server: messages[0] });
        }
      },
    });
  }

  protected create(): void {
    this.submitting.set(true);
    this.api
      .createClinic(this.draft())
      .pipe(
        switchMap((clinic) => this.api.provision(clinic.id, this.forcedDryRun() || this.dryRun())),
      )
      .subscribe({
        next: (res) => {
          void this.router.navigate(['/clinics', res.job.clinicId], {
            state: { apiKey: res.apiKey },
          });
        },
        error: () => this.submitting.set(false),
      });
  }
}
