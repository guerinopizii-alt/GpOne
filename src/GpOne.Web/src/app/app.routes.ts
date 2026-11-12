import { Routes } from '@angular/router';
import { amministratoreGuard, autenticatoGuard, clienteGuard } from './core/sicurezza';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pagine/login').then(m => m.Login) },
  {
    path: '',
    loadComponent: () => import('./pagine/struttura').then(m => m.Struttura),
    canActivate: [autenticatoGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'chiedi' },
      { path: 'scegli-cliente', loadComponent: () => import('./pagine/scegli-cliente').then(m => m.ScegliCliente) },
      { path: 'chiedi', canActivate: [clienteGuard], loadComponent: () => import('./pagine/chiedi').then(m => m.Chiedi) },
      { path: 'cambia-password', loadComponent: () => import('./pagine/cambia-password').then(m => m.CambiaPassword) },
      { path: 'admin/utenti', canActivate: [amministratoreGuard], loadComponent: () => import('./pagine/admin-utenti').then(m => m.AdminUtenti) },
      { path: 'admin/clienti', canActivate: [amministratoreGuard], loadComponent: () => import('./pagine/admin-clienti').then(m => m.AdminClienti) },
      {
        path: 'admin/clienti/:clienteId/schema', canActivate: [amministratoreGuard],
        loadComponent: () => import('./pagine/admin-schema').then(m => m.AdminSchema)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
