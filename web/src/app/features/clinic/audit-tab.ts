import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { ConsoleApi } from '../../core/api/console.api';
import { AuditEntry } from '../../core/api/models';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-audit-tab',
  imports: [DatePipe, MatIconModule, TranslatePipe],
  template: `
    @if (entries().length === 0) {
      <div class="empty">
        <mat-icon>receipt_long</mat-icon>
        <p>{{ 'audit.empty' | translate }}</p>
      </div>
    } @else {
      <table class="data-table">
        <thead>
          <tr>
            <th>{{ 'audit.when' | translate }}</th>
            <th>{{ 'audit.who' | translate }}</th>
            <th>{{ 'audit.what' | translate }}</th>
            <th>{{ 'audit.details' | translate }}</th>
          </tr>
        </thead>
        <tbody>
          @for (e of entries(); track e.id) {
            <tr>
              <td>{{ e.at | date: 'medium' : undefined : languages.language() }}</td>
              <td dir="ltr">{{ e.actor }}</td>
              <td>{{ action(e) }}</td>
              <td>
                <code dir="ltr">{{ details(e) }}</code>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
})
export class AuditTab {
  private readonly api = inject(ConsoleApi);
  private readonly translate = inject(TranslateService);
  protected readonly languages = inject(LanguageService);

  readonly clinicId = input.required<string>();
  readonly refreshKey = input<string>('');
  protected readonly entries = signal<AuditEntry[]>([]);

  constructor() {
    effect(() => {
      this.refreshKey();
      this.api.audit(this.clinicId(), 1).subscribe((page) => this.entries.set(page.items));
    });
  }

  protected action(e: AuditEntry): string {
    const key = `audit.actions.${e.action}`;
    const text = this.translate.instant(key);
    return text === key ? e.action : text;
  }

  protected details(e: AuditEntry): string {
    const entries = Object.entries(e.details);
    return entries
      .map(([k, v]) => `${k}: ${typeof v === 'object' ? JSON.stringify(v) : String(v)}`)
      .join(', ');
  }
}
