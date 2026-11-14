import { registerLocaleData } from '@angular/common';
import localeAr from '@angular/common/locales/ar';
import { bootstrapApplication } from '@angular/platform-browser';

import { App } from './app/app';
import { appConfig } from './app/app.config';

// DatePipe needs the Arabic locale data to format dates in the Arabic interface.
registerLocaleData(localeAr);

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
