import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell/shell.component';

export const routes: Routes = [
  {
    path: '',
    component: ShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'connection' },
      {
        path: 'connection',
        loadComponent: () =>
          import('./features/connection/connection-page.component').then(
            (m) => m.ConnectionPageComponent
          ),
      },
      {
        path: 'monitor',
        loadComponent: () =>
          import('./features/monitor/monitor-page.component').then((m) => m.MonitorPageComponent),
      },
      {
        path: 'jobs',
        loadComponent: () =>
          import('./features/jobs/jobs-page.component').then((m) => m.JobsPageComponent),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/settings-page.component').then((m) => m.SettingsPageComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'connection' },
];
