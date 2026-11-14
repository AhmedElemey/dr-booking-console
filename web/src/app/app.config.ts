import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { TranslateLoader, provideTranslateService } from '@ngx-translate/core';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { PlatformService } from './core/config/platform.service';
import { errorInterceptor } from './core/http/error.interceptor';
import { LanguageService } from './core/i18n/language.service';
import { PlatformTranslateLoader } from './core/i18n/platform-loader';
import { mockInterceptors } from './core/mock/mock-providers';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // The error interceptor sits outside the auth interceptor; the mock backend (demo builds
    // only) is innermost and answers instead of the network.
    provideHttpClient(withInterceptors([errorInterceptor, authInterceptor, ...mockInterceptors])),
    provideTranslateService({
      loader: { provide: TranslateLoader, useClass: PlatformTranslateLoader },
      fallbackLang: 'en',
    }),
    // The platform name must be known before the translations are filled in.
    provideAppInitializer(async () => {
      const platform = inject(PlatformService);
      const languages = inject(LanguageService);
      await platform.load();
      await new Promise<void>((resolve) =>
        languages.use(languages.stored()).subscribe(() => resolve()),
      );
    }),
  ],
};
