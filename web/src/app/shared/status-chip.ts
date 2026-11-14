import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Coloured chip for a clinic, job or step status (labels under `status.*`). */
@Component({
  selector: 'app-status-chip',
  imports: [TranslatePipe],
  template: `<span class="chip" [class]="'chip status-' + status().toLowerCase()">{{
    'status.' + status() | translate
  }}</span>`,
})
export class StatusChip {
  readonly status = input.required<string>();
}
