import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import { Clinic } from '../../core/api/models';

@Component({
  selector: 'app-repos-tab',
  imports: [MatIconModule, TranslatePipe],
  template: `
    @if (clinic().repos.length === 0) {
      <div class="empty">
        <mat-icon>folder_off</mat-icon>
        <p>{{ 'repos.empty' | translate }}</p>
      </div>
    } @else {
      <ul class="repo-cards">
        @for (r of clinic().repos; track r.kind) {
          <li>
            <mat-icon>{{ icon(r.kind) }}</mat-icon>
            <span class="repo-info">
              <small>{{ 'repoKind.' + r.kind | translate }}</small>
              <a [href]="r.htmlUrl" target="_blank" rel="noopener" dir="ltr">{{ r.fullName }}</a>
            </span>
            <mat-icon class="muted">open_in_new</mat-icon>
          </li>
        }
      </ul>
    }
  `,
})
export class ReposTab {
  readonly clinic = input.required<Clinic>();

  protected icon(kind: string): string {
    return (
      { Backend: 'dns', Dashboard: 'dashboard', Mobile: 'smartphone', Website: 'language' }[kind] ??
      'folder'
    );
  }
}
