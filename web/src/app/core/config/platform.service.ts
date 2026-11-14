import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ConsoleApi } from '../api/console.api';
import { Platform } from '../api/models';

/** The platform display name (one server setting, `Platform:Name`) used everywhere in the UI. */
@Injectable({ providedIn: 'root' })
export class PlatformService {
  private readonly api = inject(ConsoleApi);
  private readonly document = inject(DOCUMENT);

  readonly name = signal('Moonlight');
  readonly provisioningMode = signal<Platform['provisioningMode']>('DryRun');

  async load(): Promise<void> {
    try {
      const platform = await firstValueFrom(this.api.platform());
      this.name.set(platform.name);
      this.provisioningMode.set(platform.provisioningMode);
    } catch {
      // Unreachable API: keep the default name; the login page will show the real problem.
    }
    this.document.title = this.name();
  }
}
