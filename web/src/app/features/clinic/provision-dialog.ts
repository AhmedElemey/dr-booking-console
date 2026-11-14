import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { TranslatePipe } from '@ngx-translate/core';

import { PlatformService } from '../../core/config/platform.service';

export interface ProvisionDialogData {
  titleKey: string;
  repos: string[];
}

/** Asks whether to run provisioning for real or as a dry run (forced when the server is in DryRun mode). */
@Component({
  selector: 'app-provision-dialog',
  imports: [MatButtonModule, MatCheckboxModule, MatDialogModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ data.titleKey | translate }}</h2>
    <mat-dialog-content>
      <p>{{ 'provision.intro' | translate }}</p>
      <ul class="repo-plan plain">
        @for (r of data.repos; track r) {
          <li>
            <code dir="ltr">{{ r }}</code>
          </li>
        }
      </ul>
      <mat-checkbox
        [checked]="dryRun()"
        [disabled]="forced()"
        (change)="dryRun.set($event.checked)"
      >
        {{ 'review.dryRun' | translate }}
      </mat-checkbox>
      <p class="muted">
        {{
          (forced() ? 'review.dryRunForced' : dryRun() ? 'review.dryRunOn' : 'review.dryRunOff')
            | translate
        }}
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="null">
        {{ 'common.cancel' | translate }}
      </button>
      <button mat-flat-button type="button" [mat-dialog-close]="{ dryRun: dryRun() }">
        {{ (dryRun() ? 'provision.runDry' : 'provision.run') | translate }}
      </button>
    </mat-dialog-actions>
  `,
})
export class ProvisionDialog {
  protected readonly data = inject<ProvisionDialogData>(MAT_DIALOG_DATA);
  private readonly platform = inject(PlatformService);
  protected readonly forced = computed(() => this.platform.provisioningMode() === 'DryRun');
  protected readonly dryRun = signal(true);

  constructor() {
    if (!this.forced()) this.dryRun.set(true);
  }
}
