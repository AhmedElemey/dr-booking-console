import { Component, computed, inject, input } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { TranslatePipe } from '@ngx-translate/core';

import { contrastText, safeColor } from '../core/colors';

export interface BrandValues {
  nameAr: string;
  nameEn: string;
  taglineAr: string;
  taglineEn: string;
  primaryColor: string;
  accentColor: string;
  logoSvg: string | null;
}

/** Live preview: a mini phone screen and a website header in the clinic's colours, in English and Arabic. */
@Component({
  selector: 'app-brand-preview',
  imports: [TranslatePipe],
  template: `
    <section class="preview-card" [attr.aria-label]="'brand.preview' | translate">
      <h3>{{ 'brand.preview' | translate }}</h3>
      @for (v of variants(); track v.lang) {
        <div class="preview-lang" [attr.dir]="v.dir" [attr.lang]="v.lang">
          <div class="preview-site" [style.background]="v.primary">
            @if (logoUrl(); as url) {
              <img class="preview-logo" [src]="url" alt="" />
            } @else {
              <span
                class="preview-mark"
                [style.background]="v.onPrimary"
                [style.color]="v.primary"
                >{{ v.initial }}</span
              >
            }
            <strong [style.color]="v.onPrimary">{{ v.name }}</strong>
            <span class="spacer"></span>
            <span class="preview-link" [style.color]="v.onPrimary">{{ v.book }}</span>
            <span class="preview-cta" [style.background]="v.accent" [style.color]="v.onAccent">{{
              v.login
            }}</span>
          </div>
          <div class="preview-phone">
            <div class="phone-top" [style.background]="v.primary" [style.color]="v.onPrimary">
              @if (logoUrl(); as url) {
                <img class="preview-logo" [src]="url" alt="" />
              }
              <strong>{{ v.name }}</strong>
              <small>{{ v.tagline }}</small>
            </div>
            <div class="phone-body">
              <div class="phone-card">
                <span class="phone-dot" [style.background]="v.accent"></span
                ><span class="phone-line"></span>
              </div>
              <div class="phone-card">
                <span class="phone-dot" [style.background]="v.primary"></span
                ><span class="phone-line short"></span>
              </div>
              <span class="phone-btn" [style.background]="v.accent" [style.color]="v.onAccent">{{
                v.book
              }}</span>
            </div>
            <div class="phone-tabs">
              <span [style.background]="v.primary"></span><span></span><span></span>
            </div>
          </div>
          <span class="preview-tag">{{ v.lang === 'en' ? 'English' : 'العربية' }}</span>
        </div>
      }
    </section>
  `,
})
export class BrandPreview {
  private readonly sanitizer = inject(DomSanitizer);

  readonly brand = input.required<BrandValues>();

  protected readonly logoUrl = computed(() => {
    const svg = this.brand().logoSvg;
    return svg
      ? this.sanitizer.bypassSecurityTrustUrl(
          `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`,
        )
      : null;
  });

  protected readonly variants = computed(() => {
    const b = this.brand();
    const primary = safeColor(b.primaryColor);
    const accent = safeColor(b.accentColor);
    const base = {
      primary,
      accent,
      onPrimary: contrastText(primary),
      onAccent: contrastText(accent),
    };
    const en = b.nameEn.trim() || 'Clinic name';
    const ar = b.nameAr.trim() || 'اسم العيادة';
    return [
      {
        ...base,
        lang: 'en',
        dir: 'ltr',
        name: en,
        initial: en.charAt(0).toUpperCase(),
        tagline: b.taglineEn || 'Book your dentist in a minute',
        book: 'Book now',
        login: 'Sign in',
      },
      {
        ...base,
        lang: 'ar',
        dir: 'rtl',
        name: ar,
        initial: ar.charAt(0),
        tagline: b.taglineAr || 'احجز طبيب أسنانك في دقيقة',
        book: 'احجز الآن',
        login: 'دخول',
      },
    ];
  });
}
