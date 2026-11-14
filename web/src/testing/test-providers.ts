import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  EnvironmentProviders,
  Provider,
  inject,
  provideEnvironmentInitializer,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import ar from '../../public/i18n/ar.json';
import en from '../../public/i18n/en.json';
import { fillPlatform } from '../app/core/i18n/platform-loader';
import { LanguageService } from '../app/core/i18n/language.service';
import { mockBackendInterceptor } from '../app/core/mock/mock-backend';

/** Translation providers loaded with the real ar/en files, starting in `lang`. */
export function provideTestTranslations(
  lang: 'ar' | 'en' = 'en',
): (Provider | EnvironmentProviders)[] {
  return [
    provideTranslateService({ fallbackLang: 'en' }),
    provideEnvironmentInitializer(() => {
      const translate = inject(TranslateService);
      translate.setTranslation('en', fillPlatform(en, 'Moonlight'));
      translate.setTranslation('ar', fillPlatform(ar, 'Moonlight'));
      translate.use(lang);
      inject(LanguageService).use(lang);
    }),
  ];
}

/** HttpClient answered by the in-memory mock backend, plus a router with no routes. */
export function provideMockApi(): (Provider | EnvironmentProviders)[] {
  return [provideHttpClient(withInterceptors([mockBackendInterceptor])), provideRouter([])];
}

export const flatten = (obj: object, prefix = ''): string[] =>
  Object.entries(obj).flatMap(([key, value]) =>
    typeof value === 'object' && value !== null
      ? flatten(value, `${prefix}${key}.`)
      : [`${prefix}${key}`],
  );
