import { Routes } from '@angular/router';
const workspace = () => import('./workspace').then((module) => module.Workspace);
export const routes: Routes = [
  { path: 'auth/callback', loadComponent: workspace },
  ...['faults', 'documents', 'engineering', 'activity', 'health', 'integrations'].map((area) => ({
    path: 'admin/' + area,
    redirectTo: 'admin/incidents',
    pathMatch: 'full' as const,
  })),
  { path: 'admin/:area', loadComponent: workspace },
  { path: '', redirectTo: 'admin/incidents', pathMatch: 'full' },
  { path: '**', redirectTo: 'admin/incidents' },
];
