import { Routes } from '@angular/router';
import { Workspace } from './workspace';
export const routes: Routes = [
  { path: 'auth/callback', component: Workspace },
  { path: 'admin/:area', component: Workspace },
  { path: '', redirectTo: 'admin/incidents', pathMatch: 'full' },
  { path: '**', redirectTo: 'admin/incidents' },
];
