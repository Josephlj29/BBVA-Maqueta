import { Routes } from '@angular/router';

/**
 * Every feature is lazily loaded. The playground and the sandbox runner are used far less
 * often than the movements screen, and neither should cost anything on first paint.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'movimientos' },
  {
    path: 'movimientos',
    title: 'Movimientos · BBVA Business Reconciliation',
    loadComponent: () =>
      import('./features/movements/movements-page').then((m) => m.MovementsPage),
  },
  {
    path: 'playground',
    title: 'API Playground · BBVA Business Reconciliation',
    loadComponent: () =>
      import('./features/playground/playground-page').then((m) => m.PlaygroundPage),
  },
  {
    path: 'sandbox',
    title: 'Plan de pruebas Sandbox · BBVA Business Reconciliation',
    loadComponent: () => import('./features/sandbox/sandbox-page').then((m) => m.SandboxPage),
  },
  { path: '**', redirectTo: 'movimientos' },
];
