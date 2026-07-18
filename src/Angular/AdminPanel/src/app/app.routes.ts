import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell/shell.component';
import { adminRoleGuard, authGateGuard } from './core/guards/auth.guards';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login-page.component').then((m) => m.LoginPageComponent),
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGateGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'connection' },
      {
        path: 'connection',
        loadComponent: () =>
          import('./features/connection/connection-page.component').then(
            (m) => m.ConnectionPageComponent,
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
        path: 'events',
        loadComponent: () =>
          import('./features/live-events/live-events-page.component').then(
            (m) => m.LiveEventsPageComponent,
          ),
      },
      {
        path: 'queues',
        loadComponent: () =>
          import('./features/queues/queues-page.component').then((m) => m.QueuesPageComponent),
      },
      {
        path: 'queue-stats',
        loadComponent: () =>
          import('./features/queue-stats/queue-stats-page.component').then(
            (m) => m.QueueStatsPageComponent,
          ),
      },
      {
        path: 'queue-acl',
        canActivate: [adminRoleGuard],
        loadComponent: () =>
          import('./features/queue-acl/queue-acl-page.component').then(
            (m) => m.QueueAclPageComponent,
          ),
      },
      {
        path: 'settings',
        canActivate: [adminRoleGuard],
        loadComponent: () =>
          import('./features/settings/settings-page.component').then(
            (m) => m.SettingsPageComponent,
          ),
      },
    ],
  },
  { path: '**', redirectTo: 'connection' },
];
