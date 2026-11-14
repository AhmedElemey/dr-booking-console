import { Component, computed, inject, input, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslatePipe } from '@ngx-translate/core';

import { ConsoleApi } from '../../core/api/console.api';
import { Clinic, FeatureDefinition } from '../../core/api/models';
import { ToastService } from '../../core/http/toast.service';
import { LanguageService } from '../../core/i18n/language.service';
import { localized } from '../../core/i18n/localized';

const GROUP_ORDER = [
  'Channels',
  'Booking',
  'Notifications',
  'Language',
  'Organisation',
  'Payments',
  'Records',
  'Growth',
  'Insight',
];

/** Feature switches: each change is saved at once and reaches the clinic backend on its next pull. */
@Component({
  selector: 'app-features-tab',
  imports: [MatIconModule, MatSlideToggleModule, TranslatePipe],
  template: `
    <p class="info-note"><mat-icon>sync</mat-icon>{{ 'features.pullNote' | translate }}</p>
    @for (g of groups(); track g.group) {
      <section class="feature-group">
        <h3>{{ 'groups.' + g.group | translate }}</h3>
        <div class="feature-list">
          @for (f of g.items; track f.key) {
            <div class="feature-row" [class.soon]="!f.available">
              <div class="feature-text">
                <strong>{{ name(f) }}</strong>
                @if (!f.available) {
                  <span class="badge-soon">{{ 'features.comingSoon' | translate }}</span>
                }
                @if (f.createsRepo) {
                  <span class="badge-repo"
                    ><mat-icon>folder_special</mat-icon
                    >{{
                      'features.createsRepo'
                        | translate: { kind: ('repoKind.' + f.createsRepo | translate) }
                    }}</span
                  >
                }
                <small>{{ description(f) }}</small>
              </div>
              <mat-slide-toggle
                [checked]="enabled(f.key)"
                [disabled]="!f.available || saving().has(f.key)"
                (change)="change(f, $event.checked)"
                [aria-label]="name(f)"
              />
            </div>
          }
        </div>
      </section>
    }
  `,
})
export class FeaturesTab {
  private readonly api = inject(ConsoleApi);
  private readonly toast = inject(ToastService);
  private readonly languages = inject(LanguageService);

  readonly clinic = input.required<Clinic>();
  readonly changed = output<void>();

  protected readonly catalog = signal<FeatureDefinition[]>([]);
  protected readonly saving = signal<Set<string>>(new Set());
  private readonly overrides = signal<Record<string, boolean>>({});

  protected readonly groups = computed(() => {
    const byGroup = new Map<string, FeatureDefinition[]>();
    for (const f of this.catalog()) byGroup.set(f.group, [...(byGroup.get(f.group) ?? []), f]);
    return GROUP_ORDER.filter((g) => byGroup.has(g)).map((g) => ({
      group: g,
      items: byGroup.get(g)!,
    }));
  });

  constructor() {
    this.api.catalog().subscribe((c) => this.catalog.set(c));
  }

  protected name(f: FeatureDefinition): string {
    return localized(this.languages.language(), f.nameAr, f.nameEn);
  }

  protected description(f: FeatureDefinition): string {
    return localized(this.languages.language(), f.descriptionAr, f.descriptionEn);
  }

  protected enabled(key: string): boolean {
    return (
      this.overrides()[key] ?? this.clinic().features.find((f) => f.key === key)?.enabled ?? false
    );
  }

  private clearOverride(key: string): void {
    this.overrides.update((o) => Object.fromEntries(Object.entries(o).filter(([k]) => k !== key)));
  }

  protected change(f: FeatureDefinition, enabled: boolean): void {
    this.overrides.update((o) => ({ ...o, [f.key]: enabled }));
    this.saving.update((s) => new Set(s).add(f.key));
    const done = () => {
      this.saving.update((s) => {
        const next = new Set(s);
        next.delete(f.key);
        return next;
      });
    };
    this.api.setFeature(this.clinic().id, f.key, enabled).subscribe({
      next: () => {
        done();
        this.toast.success(enabled ? 'features.savedOn' : 'features.savedOff', {
          name: this.name(f),
        });
        this.changed.emit();
        this.clearOverride(f.key);
      },
      error: () => {
        done();
        this.clearOverride(f.key);
      },
    });
  }
}
