import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { toObservable, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { combineLatest, debounceTime, distinctUntilChanged, startWith, switchMap, tap } from 'rxjs';

import { ConsoleApi } from '../../core/api/console.api';
import { ClinicStatus, ClinicSummary } from '../../core/api/models';
import { contrastText } from '../../core/colors';
import { LanguageService } from '../../core/i18n/language.service';
import { localized } from '../../core/i18n/localized';
import { StatusChip } from '../../shared/status-chip';

const STATUSES: ClinicStatus[] = ['Draft', 'Provisioning', 'Active', 'Failed', 'Suspended'];

@Component({
  selector: 'app-clinics-list',
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    ReactiveFormsModule,
    RouterLink,
    StatusChip,
    TranslatePipe,
  ],
  template: `
    <div class="page-head">
      <div>
        <h1>{{ 'clinics.title' | translate }}</h1>
        <p class="muted">{{ 'clinics.subtitle' | translate }}</p>
      </div>
      <a mat-flat-button routerLink="/clinics/new"
        ><mat-icon>add</mat-icon>{{ 'clinics.new' | translate }}</a
      >
    </div>

    <div class="filters">
      <mat-form-field appearance="outline" class="grow">
        <mat-label>{{ 'clinics.search' | translate }}</mat-label>
        <mat-icon matPrefix>search</mat-icon>
        <input matInput [formControl]="search" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>{{ 'clinics.status' | translate }}</mat-label>
        <mat-select [formControl]="status">
          <mat-option value="">{{ 'clinics.allStatuses' | translate }}</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ 'status.' + s | translate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    @if (!loading() && items().length === 0) {
      <div class="empty">
        <mat-icon>local_hospital</mat-icon>
        <p>{{ 'clinics.empty' | translate }}</p>
      </div>
    }

    <div class="clinic-grid">
      @for (c of items(); track c.id) {
        <div class="clinic-card">
          <span
            class="clinic-swatch"
            [style.background]="c.primaryColor"
            [style.color]="onColor(c.primaryColor)"
          >
            {{ initial(c) }}
            <i class="accent-dot" [style.background]="c.accentColor"></i>
          </span>
          <span class="clinic-main">
            <a class="card-link" [routerLink]="['/clinics', c.id]"
              ><strong>{{ name(c) }}</strong></a
            >
            <code class="slug" dir="ltr">{{ c.slug }}</code>
          </span>
          <app-status-chip [status]="c.status" />
          <dl class="clinic-meta">
            <div>
              <dt>{{ 'clinics.features' | translate }}</dt>
              <dd>{{ c.enabledFeatureCount }}</dd>
            </div>
            <div>
              <dt>{{ 'clinics.repos' | translate }}</dt>
              <dd>
                @if (c.repos.length === 0) {
                  <span class="muted">—</span>
                }
                @for (r of c.repos; track r.kind) {
                  <a
                    class="repo-link"
                    [href]="r.htmlUrl"
                    target="_blank"
                    rel="noopener"
                    [title]="r.fullName"
                    >{{ 'repoKind.' + r.kind | translate }}</a
                  >
                }
              </dd>
            </div>
            <div>
              <dt>{{ 'clinics.created' | translate }}</dt>
              <dd>{{ c.createdAt | date: 'mediumDate' : undefined : languages.language() }}</dd>
            </div>
          </dl>
        </div>
      }
    </div>

    @if (total() > pageSize) {
      <mat-paginator
        [length]="total()"
        [pageSize]="pageSize"
        [pageIndex]="page() - 1"
        [hidePageSize]="true"
        (page)="onPage($event)"
      />
    }
  `,
})
export class ClinicsList {
  private readonly api = inject(ConsoleApi);
  protected readonly languages = inject(LanguageService);

  protected readonly statuses = STATUSES;
  protected readonly pageSize = 20;
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly status = new FormControl<ClinicStatus | ''>('', { nonNullable: true });
  protected readonly items = signal<ClinicSummary[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly loading = signal(true);

  constructor() {
    combineLatest([
      this.search.valueChanges.pipe(startWith(''), debounceTime(250), distinctUntilChanged()),
      this.status.valueChanges.pipe(startWith('' as ClinicStatus | '')),
      toObservable(this.page),
    ])
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(([q, s, p]) => this.api.clinics(q, s, p, this.pageSize)),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          this.total.set(res.total);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  protected name(c: ClinicSummary): string {
    return localized(this.languages.language(), c.nameAr, c.nameEn);
  }

  protected initial(c: ClinicSummary): string {
    return this.name(c).charAt(0).toUpperCase();
  }

  protected onColor(hex: string): string {
    return contrastText(hex);
  }

  protected onPage(e: PageEvent): void {
    this.page.set(e.pageIndex + 1);
  }
}
