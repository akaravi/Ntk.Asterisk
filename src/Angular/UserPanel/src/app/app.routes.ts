import { Routes } from '@angular/router';
import { authGateGuard } from './core/guards/auth.guards';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login-page.component').then((m) => m.LoginPageComponent),
  },
  { path: '', pathMatch: 'full', redirectTo: 'calls' },
  {
    path: 'calls',
    canActivate: [authGateGuard],
    loadComponent: () =>
      import('./features/call-forms/call-forms.component').then((m) => m.CallFormsComponent),
  },
  {
    path: 'jobs',
    canActivate: [authGateGuard],
    loadComponent: () =>
      import('./features/jobs/jobs-list.component').then((m) => m.JobsListComponent),
  },
  {
    path: 'webphone/buddies',
    canActivate: [authGateGuard],
    loadComponent: () =>
      import('./features/webphone/webphone-buddies.component').then((m) => m.WebphoneBuddiesComponent),
  },
  {
    path: 'webphone/cdr',
    canActivate: [authGateGuard],
    loadComponent: () =>
      import('./features/webphone/webphone-cdr.component').then((m) => m.WebphoneCdrComponent),
  },
  {
    path: 'webphone/recordings',
    canActivate: [authGateGuard],
    loadComponent: () =>
      import('./features/webphone/webphone-recordings.component').then(
        (m) => m.WebphoneRecordingsComponent,
      ),
  },
  { path: '**', redirectTo: 'calls' },
];
