import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Direction, Directionality } from '@angular/cdk/bidi';
import { TranslateService } from '@ngx-translate/core';

import { AppLang } from '../api/models';

const LANGUAGE_KEY = 'console.language';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);
  private readonly directionality = inject(Directionality);

  readonly language = signal<AppLang>('en');
  readonly direction = computed<Direction>(() => (this.language() === 'ar' ? 'rtl' : 'ltr'));

  /** The remembered language (a per-browser convenience); English by default for super admins. */
  stored(): AppLang {
    try {
      return localStorage.getItem(LANGUAGE_KEY) === 'ar' ? 'ar' : 'en';
    } catch {
      return 'en';
    }
  }

  use(language: AppLang) {
    this.language.set(language);
    this.document.documentElement.lang = language;
    this.document.documentElement.dir = this.direction();
    // Overlays (dialogs, menus) read the root Directionality, sampled once at startup otherwise.
    this.directionality.valueSignal.set(this.direction());
    try {
      localStorage.setItem(LANGUAGE_KEY, language);
    } catch {
      // Storage unavailable: the choice just is not remembered.
    }
    return this.translate.use(language);
  }

  toggle(): void {
    this.use(this.language() === 'ar' ? 'en' : 'ar').subscribe();
  }
}
