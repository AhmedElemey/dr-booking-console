import { Routes } from '@angular/router';

import { authGuard, guestGuard } from './core/auth/guards';
import { Shell } from './core/layout/shell';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'clinics' },
      {
        path: 'clinics',
        loadComponent: () => import('./features/clinics/clinics-list').then((m) => m.ClinicsList),
      },
      {
        path: 'clinics/new',
        loadComponent: () => import('./features/wizard/clinic-wizard').then((m) => m.ClinicWizard),
      },
      {
        path: 'clinics/:id',
        loadComponent: () => import('./features/clinic/clinic-page').then((m) => m.ClinicPage),
      },
      {
        path: 'admins',
        loadComponent: () => import('./features/admins/admins-page').then((m) => m.AdminsPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
