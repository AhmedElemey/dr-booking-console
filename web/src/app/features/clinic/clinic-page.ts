import { Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ConsoleApi } from '../../core/api/console.api';
import { Clinic } from '../../core/api/models';
import { contrastText } from '../../core/colors';
import { PlatformService } from '../../core/config/platform.service';
import { ToastService } from '../../core/http/toast.service';
import { LanguageService } from '../../core/i18n/language.service';
import { localized } from '../../core/i18n/localized';
import { ConfirmDialog } from '../../shared/confirm-dialog';
import { KeyDialog } from '../../shared/key-dialog';
import { StatusChip } from '../../shared/status-chip';
import { AuditTab } from './audit-tab';
import { BrandTab } from './brand-tab';
import { FeaturesTab } from './features-tab';
import { JobsTab } from './jobs-tab';
import { ProvisionDialog } from './provision-dialog';
import { ReposTab } from './repos-tab';

@Component({
  selector: 'app-clinic-page',
  imports: [
    AuditTab,
    BrandTab,
    FeaturesTab,
    JobsTab,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTabsModule,
    ReposTab,
    RouterLink,
    StatusChip,
    TranslatePipe,
  ],
  templateUrl: './clinic-page.html',
})
export class ClinicPage {
  private readonly api = inject(ConsoleApi);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly languages = inject(LanguageService);
  protected readonly platform = inject(PlatformService);

  readonly id = input.required<string>();

  protected readonly clinic = signal<Clinic | null>(null);
  protected readonly busy = signal(false);
  protected readonly auditKey = signal('');
  protected readonly running = computed(() => {
    const status = this.clinic()?.latestJob?.status;
    return status === 'Queued' || status === 'Running';
  });
  protected readonly displayName = computed(() => {
    const c = this.clinic();
    return c ? localized(this.languages.language(), c.nameAr, c.nameEn) : '';
  });
  protected readonly headerText = computed(() =>
    contrastText(this.clinic()?.primaryColor ?? '#3F3CCF'),
  );
  protected readonly provisionLabel = computed(() => {
    switch (this.clinic()?.status) {
      case 'Failed':
        return 'actions.retry';
      case 'Active':
        return 'actions.reprovision';
      default:
        return 'actions.provision';
    }
  });

  private timer: ReturnType<typeof setTimeout> | undefined;
  private keyShown = false;

  constructor() {
    this.destroyRef.onDestroy(() => clearTimeout(this.timer));
    queueMicrotask(() => this.load());
  }

  protected load(): void {
    clearTimeout(this.timer);
    this.api.clinic(this.id()).subscribe((c) => {
      this.clinic.set(c);
      this.auditKey.set(`${c.updatedAt}|${c.latestJob?.status}`);
      this.showInitialKey();
      if (c.latestJob?.status === 'Queued' || c.latestJob?.status === 'Running') {
        this.timer = setTimeout(() => this.load(), 1500);
      }
    });
  }

  /** The key a first real provisioning generated arrives through navigation state, shown exactly once. */
  private showInitialKey(): void {
    if (this.keyShown) return;
    this.keyShown = true;
    const key = (history.state as { apiKey?: string | null } | null)?.apiKey;
    if (key) {
      this.dialog.open(KeyDialog, { data: { apiKey: key }, disableClose: true, width: '560px' });
      history.replaceState({ ...history.state, apiKey: null }, '');
    }
  }

  protected repoPlan(c: Clinic): string[] {
    const on = (key: string) => c.features.find((f) => f.key === key)?.enabled;
    const kinds = [
      'backend',
      'dashboard',
      ...(on('mobileApp') ? ['mobile'] : []),
      ...(on('website') ? ['website'] : []),
    ];
    return kinds.map((k) => `${c.slug}-${k}`);
  }

  protected async provision(): Promise<void> {
    const c = this.clinic();
    if (!c) return;
    const result = await firstValueFrom(
      this.dialog
        .open<ProvisionDialog, unknown, { dryRun: boolean } | null>(ProvisionDialog, {
          data: { titleKey: this.provisionLabel(), repos: this.repoPlan(c) },
          width: '520px',
        })
        .afterClosed(),
    );
    if (!result) return;
    this.busy.set(true);
    this.api.provision(c.id, result.dryRun).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.toast.success('actions.started');
        this.load();
        if (res.apiKey)
          this.dialog.open(KeyDialog, {
            data: { apiKey: res.apiKey },
            disableClose: true,
            width: '560px',
          });
      },
      error: () => this.busy.set(false),
    });
  }

  protected syncConfig(): void {
    const c = this.clinic();
    if (!c) return;
    this.busy.set(true);
    this.api.syncConfig(c.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success('actions.started');
        this.load();
      },
      error: () => this.busy.set(false),
    });
  }

  protected async toggleSuspend(): Promise<void> {
    const c = this.clinic();
    if (!c) return;
    const suspending = c.status !== 'Suspended';
    const ok = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, unknown, boolean>(ConfirmDialog, {
          data: {
            titleKey: suspending ? 'suspend.title' : 'activate.title',
            messageKey: suspending ? 'suspend.message' : 'activate.message',
            confirmKey: suspending ? 'actions.suspend' : 'actions.activate',
          },
        })
        .afterClosed(),
    );
    if (!ok) return;
    (suspending ? this.api.suspend(c.id) : this.api.activate(c.id)).subscribe((next) => {
      this.clinic.set(next);
      this.toast.success(suspending ? 'suspend.done' : 'activate.done');
      this.load();
    });
  }

  protected async rotateKey(): Promise<void> {
    const c = this.clinic();
    if (!c) return;
    const ok = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, unknown, boolean>(ConfirmDialog, {
          data: {
            titleKey: 'rotate.title',
            messageKey: 'rotate.message',
            confirmKey: 'rotate.confirm',
          },
        })
        .afterClosed(),
    );
    if (!ok) return;
    this.api.rotateKey(c.id).subscribe((res) => {
      this.dialog.open(KeyDialog, {
        data: { apiKey: res.apiKey },
        disableClose: true,
        width: '560px',
      });
      this.load();
    });
  }
}
