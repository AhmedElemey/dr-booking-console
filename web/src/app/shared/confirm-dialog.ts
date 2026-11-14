import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { TranslatePipe } from '@ngx-translate/core';

export interface ConfirmData {
  titleKey: string;
  messageKey: string;
  confirmKey: string;
  params?: object;
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ data.titleKey | translate: data.params }}</h2>
    <mat-dialog-content>{{ data.messageKey | translate: data.params }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="false">
        {{ 'common.cancel' | translate }}
      </button>
      <button mat-flat-button type="button" [mat-dialog-close]="true">
        {{ data.confirmKey | translate }}
      </button>
    </mat-dialog-actions>
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmData>(MAT_DIALOG_DATA);
}
