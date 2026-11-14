import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

export interface KeyDialogData {
  apiKey: string;
}

/** Shows a freshly generated clinic API key. The plaintext is not stored, so this is the only time. */
@Component({
  selector: 'app-key-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'key.title' | translate }}</h2>
    <mat-dialog-content>
      <p class="warn-note">
        <mat-icon>warning</mat-icon>
        {{ 'key.warning' | translate }}
      </p>
      <div class="key-box">
        <code dir="ltr">{{ data.apiKey }}</code>
        <button mat-stroked-button type="button" (click)="copy()">
          <mat-icon>{{ copied() ? 'check' : 'content_copy' }}</mat-icon>
          {{ (copied() ? 'key.copied' : 'key.copy') | translate }}
        </button>
      </div>
      <p class="muted">{{ 'key.usage' | translate }}</p>
      <pre class="code-block" dir="ltr">
Console__ClinicId=&lt;clinic id&gt;
Console__ApiKey={{ data.apiKey }}</pre>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button type="button" mat-dialog-close>{{ 'key.saved' | translate }}</button>
    </mat-dialog-actions>
  `,
})
export class KeyDialog {
  protected readonly data = inject<KeyDialogData>(MAT_DIALOG_DATA);
  protected readonly copied = signal(false);

  protected async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.data.apiKey);
      this.copied.set(true);
    } catch {
      // Clipboard blocked: the key stays selectable in the box.
    }
  }
}
