import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

import { ConsoleApi } from '../../core/api/console.api';
import { Clinic, Job, JobStep, StepStatus } from '../../core/api/models';
import { LanguageService } from '../../core/i18n/language.service';
import { StatusChip } from '../../shared/status-chip';

const ICONS: Record<StepStatus, string> = {
  Pending: 'radio_button_unchecked',
  Running: 'autorenew',
  Succeeded: 'check_circle',
  Skipped: 'skip_next',
  Failed: 'error',
};

/** Provisioning history with a live step timeline; refreshed with the clinic while a job runs. */
@Component({
  selector: 'app-jobs-tab',
  imports: [DatePipe, MatIconModule, StatusChip, TranslatePipe],
  template: `
    @if (jobs().length === 0) {
      <div class="empty">
        <mat-icon>history</mat-icon>
        <p>{{ 'jobs.empty' | translate }}</p>
      </div>
    }
    @for (job of jobs(); track job.id; let first = $first) {
      <section class="job-card">
        <header>
          <app-status-chip [status]="job.status" />
          @if (job.dryRun) {
            <span class="chip status-dry">{{ 'jobs.dryRun' | translate }}</span>
          }
          <span class="muted"
            >{{ job.createdAt | date: 'medium' : undefined : languages.language() }} ·
            {{ job.startedBy }}</span
          >
        </header>
        <ol class="timeline">
          @for (s of job.steps; track s.name) {
            <li [class]="'step-' + s.status.toLowerCase()">
              <mat-icon [class.spin]="s.status === 'Running'">{{ icon(s.status) }}</mat-icon>
              <div>
                <strong
                  >{{ 'jobs.steps.' + prefix(s) | translate }}
                  @if (kind(s)) {
                    <span dir="ltr" class="step-kind">{{ 'repoKind.' + kind(s) | translate }}</span>
                  }
                </strong>
                @if (s.message) {
                  <p>{{ s.message }}</p>
                }
              </div>
            </li>
          }
        </ol>
      </section>
    }
  `,
})
export class JobsTab {
  private readonly api = inject(ConsoleApi);
  protected readonly languages = inject(LanguageService);

  readonly clinic = input.required<Clinic>();
  protected readonly jobs = signal<Job[]>([]);

  constructor() {
    effect(() => {
      const c = this.clinic();
      // The clinic is re-read while a job runs, so the history follows its progress.
      this.api.jobs(c.id).subscribe((jobs) => this.jobs.set(jobs));
    });
  }

  protected icon(status: StepStatus): string {
    return ICONS[status];
  }

  protected prefix(step: JobStep): string {
    return step.name.split(':')[0];
  }

  protected kind(step: JobStep): string | null {
    const kind = step.name.split(':')[1];
    return kind ? kind.charAt(0).toUpperCase() + kind.slice(1) : null;
  }
}
