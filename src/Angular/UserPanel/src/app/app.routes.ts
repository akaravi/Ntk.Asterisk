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
  { path: '**', redirectTo: 'calls' },
];
