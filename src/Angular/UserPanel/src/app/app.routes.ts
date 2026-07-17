import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'calls' },
  {
    path: 'calls',
    loadComponent: () =>
      import('./features/call-forms/call-forms.component').then((m) => m.CallFormsComponent),
  },
  {
    path: 'jobs',
    loadComponent: () =>
      import('./features/jobs/jobs-list.component').then((m) => m.JobsListComponent),
  },
  { path: '**', redirectTo: 'calls' },
];
