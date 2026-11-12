import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SessioneService } from './sessione.service';

/** Aggiunge il token JWT e il cliente scelto; con 401 torna al login. */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  const sessione = inject(SessioneService);
  const router = inject(Router);
  const headers: Record<string, string> = {};
  const token = sessione.token;
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const cliente = sessione.cliente();
  if (cliente) headers['X-Cliente-Id'] = String(cliente.id);

  return next(req.clone({ setHeaders: headers })).pipe(
    catchError((errore: unknown) => {
      if (errore instanceof HttpErrorResponse && errore.status === 401 && !req.url.startsWith('/api/auth/')) {
        sessione.esci();
        router.navigate(['/login']);
      }
      return throwError(() => errore);
    })
  );
};

export const autenticatoGuard: CanActivateFn = () => {
  const sessione = inject(SessioneService);
  return sessione.token ? true : inject(Router).createUrlTree(['/login']);
};

export const amministratoreGuard: CanActivateFn = () => {
  const sessione = inject(SessioneService);
  return sessione.token && sessione.amministratore() ? true : inject(Router).createUrlTree(['/']);
};

export const clienteGuard: CanActivateFn = () => {
  const sessione = inject(SessioneService);
  if (!sessione.token) return inject(Router).createUrlTree(['/login']);
  return sessione.cliente() ? true : inject(Router).createUrlTree(['/scegli-cliente']);
};
