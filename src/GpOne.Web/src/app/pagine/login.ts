import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService, messaggioErrore } from '../core/api.service';
import { RiquadroMessaggio } from '../core/riquadro-messaggio';
import { SessioneService } from '../core/sessione.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RiquadroMessaggio, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule],
  template: `
    <div class="contenitore">
      <section class="vetrina">
        <div class="marchio"><img src="img/logo.svg" alt="" width="44" height="44" />GpOne</div>
        <img class="illustrazione" src="img/login.svg" alt="" width="400" height="320" />
        <h2>Fai domande ai tuoi dati,<br />in italiano.</h2>
        <ul>
          <li><span class="bolla-icona piccola viola"><mat-icon class="icona-piena">forum</mat-icon></span>Chiedi come parleresti a un collega</li>
          <li><span class="bolla-icona piccola arancio"><mat-icon class="icona-piena">bar_chart</mat-icon></span>Tabelle e grafici in pochi secondi</li>
          <li><span class="bolla-icona piccola verde"><mat-icon class="icona-piena">verified_user</mat-icon></span>L'AI non vede mai i tuoi dati</li>
        </ul>
      </section>

      <form class="modulo" (ngSubmit)="accedi()">
        <img class="logo-mobile" src="img/logo.svg" alt="" width="56" height="56" />
        <h1>{{ primoAvvio() ? 'Benvenuto!' : 'Bentornato' }} <span class="saluto">👋</span></h1>
        <p class="muto">{{ primoAvvio() ? 'Primo avvio: crea l\\'utente amministratore' : 'Accedi con le tue credenziali' }}</p>

        <mat-form-field class="piena">
          <mat-label>Nome utente</mat-label>
          <input matInput name="username" [(ngModel)]="username" autocomplete="username" required />
        </mat-form-field>
        @if (primoAvvio()) {
          <mat-form-field class="piena">
            <mat-label>Nome e cognome</mat-label>
            <input matInput name="nomeCompleto" [(ngModel)]="nomeCompleto" />
          </mat-form-field>
          <mat-form-field class="piena">
            <mat-label>Email</mat-label>
            <input matInput name="email" type="email" [(ngModel)]="email" />
          </mat-form-field>
        }
        <mat-form-field class="piena">
          <mat-label>Password</mat-label>
          <input matInput name="password" type="password" [(ngModel)]="password"
                 [autocomplete]="primoAvvio() ? 'new-password' : 'current-password'" required />
        </mat-form-field>
        @if (primoAvvio()) {
          <mat-form-field class="piena">
            <mat-label>Conferma password</mat-label>
            <input matInput name="conferma" type="password" [(ngModel)]="conferma" autocomplete="new-password" />
          </mat-form-field>
        }

        @if (errore(); as e) { <app-messaggio [testo]="e" errore /> }

        <button mat-flat-button class="piena gradiente accedi" type="submit" [disabled]="occupato()">
          @if (occupato()) { <mat-spinner diameter="18" /> } @else { {{ primoAvvio() ? 'Crea amministratore e accedi' : 'Accedi' }} }
        </button>
      </form>
    </div>
  `,
  styles: `
    .contenitore { min-height: 100vh; display: grid; grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr); }
    .vetrina {
      display: flex; flex-direction: column; justify-content: center; padding: 48px 56px; color: #fff; position: relative; overflow: hidden;
      background:
        radial-gradient(600px 400px at 10% 10%, rgb(255 255 255 / .18), transparent 60%),
        radial-gradient(500px 400px at 90% 90%, rgb(255 165 61 / .5), transparent 60%),
        linear-gradient(140deg, #5b2bd6 0%, #7c4dff 35%, #ff4f8b 75%, #ff8a3d 100%);
    }
    .marchio { display: flex; align-items: center; gap: 12px; font-size: 26px; font-weight: 800; letter-spacing: -.02em; }
    .marchio img { border-radius: 13px; box-shadow: 0 0 0 3px rgb(255 255 255 / .35); }
    .illustrazione { width: min(400px, 100%); height: auto; margin: 32px 0 16px; align-self: center; }
    .vetrina h2 { font-size: clamp(24px, 3vw, 34px); line-height: 1.2; font-weight: 800; margin: 0 0 20px; }
    .vetrina ul { list-style: none; padding: 0; margin: 0; display: grid; gap: 12px; font-weight: 600; }
    .vetrina li { display: flex; align-items: center; gap: 12px; }
    .vetrina .bolla-icona { box-shadow: 0 4px 12px rgb(0 0 0 / .15); }

    .modulo { align-self: center; justify-self: center; width: 100%; max-width: 380px; padding: 40px 24px; }
    .modulo h1 { margin: 0 0 4px; font-size: 32px; }
    .saluto { display: inline-block; animation: saluta 1.6s ease-in-out 1; transform-origin: 70% 70%; }
    .modulo .muto { margin: 0 0 24px; font-size: 14px; }
    .logo-mobile { display: none; margin-bottom: 16px; border-radius: 16px; }
    .errore { display: flex; align-items: center; gap: 6px; }
    .accedi { height: 48px; font-size: 15px; font-weight: 700; margin-top: 4px; }

    @keyframes saluta { 0%, 100% { transform: rotate(0); } 20%, 60% { transform: rotate(16deg); } 40%, 80% { transform: rotate(-8deg); } }
    @media (max-width: 860px) {
      .contenitore { grid-template-columns: 1fr; }
      .vetrina { display: none; }
      .logo-mobile { display: block; }
    }
  `
})
export class Login implements OnInit {
  private readonly api = inject(ApiService);
  private readonly sessione = inject(SessioneService);
  private readonly router = inject(Router);

  username = '';
  password = '';
  conferma = '';
  email = '';
  nomeCompleto = '';
  readonly primoAvvio = signal(false);
  readonly errore = signal<string | null>(null);
  readonly occupato = signal(false);

  async ngOnInit() {
    try {
      this.primoAvvio.set((await this.api.stato()).primoAvvio);
    } catch (e) {
      this.errore.set(messaggioErrore(e));
    }
  }

  async accedi() {
    this.errore.set(null);
    if (this.primoAvvio() && this.password !== this.conferma) {
      this.errore.set('Le password non coincidono.');
      return;
    }
    this.occupato.set(true);
    try {
      const risposta = this.primoAvvio()
        ? await this.api.primoAmministratore({ username: this.username, password: this.password, email: this.email, nomeCompleto: this.nomeCompleto })
        : await this.api.login(this.username, this.password);
      this.sessione.accedi(risposta);

      // Con un solo cliente si entra direttamente, altrimenti si sceglie
      const clienti = await this.api.mieiClienti();
      this.sessione.clienti.set(clienti);
      if (clienti.length === 1) {
        this.sessione.scegliCliente(clienti[0]);
        await this.router.navigate(['/chiedi']);
      } else {
        await this.router.navigate([clienti.length === 0 && risposta.utente.amministratore ? '/admin/clienti' : '/scegli-cliente']);
      }
    } catch (e) {
      this.errore.set(messaggioErrore(e));
    } finally {
      this.occupato.set(false);
    }
  }
}
