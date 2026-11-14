import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { AuthService } from '../auth/auth.service';
import { PlatformService } from '../config/platform.service';
import { LanguageService } from '../i18n/language.service';

@Component({
  selector: 'app-shell',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatToolbarModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    TranslatePipe,
  ],
  template: `
    <header class="shell-bar">
      <a class="shell-brand" routerLink="/clinics">
        <span class="shell-logo" aria-hidden="true">
          <mat-icon>nightlight</mat-icon>
        </span>
        <span class="shell-brand-text">
          <strong>{{ platform.name() }}</strong>
          <small>{{ 'shell.console' | translate }}</small>
        </span>
      </a>
      <nav class="shell-nav" [attr.aria-label]="'shell.navigation' | translate">
        <a routerLink="/clinics" routerLinkActive="active">
          <mat-icon>local_hospital</mat-icon>{{ 'shell.clinics' | translate }}
        </a>
        <a routerLink="/admins" routerLinkActive="active">
          <mat-icon>admin_panel_settings</mat-icon>{{ 'shell.admins' | translate }}
        </a>
      </nav>
      <span class="spacer"></span>
      @if (platform.provisioningMode() === 'DryRun') {
        <span class="mode-pill" [title]="'shell.dryRunModeHint' | translate">
          {{ 'shell.dryRunMode' | translate }}
        </span>
      }
      <button mat-button type="button" (click)="languages.toggle()">
        <mat-icon>translate</mat-icon>
        {{ languages.language() === 'ar' ? 'English' : 'العربية' }}
      </button>
      <span class="shell-user">{{ auth.admin()?.email }}</span>
      <button
        mat-icon-button
        type="button"
        (click)="auth.logout()"
        [attr.aria-label]="'shell.logout' | translate"
      >
        <mat-icon>logout</mat-icon>
      </button>
    </header>
    <main class="page">
      <router-outlet />
    </main>
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly platform = inject(PlatformService);
  protected readonly languages = inject(LanguageService);
}
