import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { TranslateLoader, TranslationObject } from '@ngx-translate/core';
import { Observable, map } from 'rxjs';

import { PlatformService } from '../config/platform.service';

/** Loads `/i18n/<lang>.json` and fills the platform name into the `{{platform}}` placeholders. */
@Injectable()
export class PlatformTranslateLoader implements TranslateLoader {
  private readonly http = inject(HttpClient);
  private readonly platform = inject(PlatformService);

  getTranslation(lang: string): Observable<TranslationObject> {
    return this.http
      .get<TranslationObject>(`/i18n/${lang}.json`)
      .pipe(map((t) => fillPlatform(t, this.platform.name())));
  }
}

export function fillPlatform(value: TranslationObject, name: string): TranslationObject {
  const walk = (node: unknown): unknown => {
    if (typeof node === 'string') return node.split('{{platform}}').join(name);
    if (node && typeof node === 'object') {
      return Object.fromEntries(Object.entries(node).map(([k, v]) => [k, walk(v)]));
    }
    return node;
  };
  return walk(value) as TranslationObject;
}
