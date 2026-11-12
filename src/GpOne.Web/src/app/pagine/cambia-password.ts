import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ApiService, messaggioErrore } from '../core/api.service';
import { RiquadroMessaggio } from '../core/riquadro-messaggio';

@Component({
  selector: 'app-cambia-password',
  imports: [FormsModule, RiquadroMessaggio, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <div class="pagina">
      <form class="card" (ngSubmit)="salva()">
        <h2>Cambia password</h2>
        <mat-form-field class="piena">
          <mat-label>Password attuale</mat-label>
          <input matInput type="password" name="attuale" [(ngModel)]="attuale" autocomplete="current-password" />
        </mat-form-field>
        <mat-form-field class="piena">
          <mat-label>Nuova password (almeno 8 caratteri)</mat-label>
          <input matInput type="password" name="nuova" [(ngModel)]="nuova" autocomplete="new-password" />
        </mat-form-field>
        <mat-form-field class="piena">
          <mat-label>Conferma nuova password</mat-label>
          <input matInput type="password" name="conferma" [(ngModel)]="conferma" autocomplete="new-password" />
        </mat-form-field>
        @if (messaggio(); as m) { <app-messaggio [testo]="m" [errore]="!ok()" [titolo]="ok() ? 'Fatto' : ''" /> }
        <button mat-flat-button type="submit">Salva</button>
      </form>
    </div>
  `,
  styles: `form { max-width: 420px; margin: 0 auto; }`
})
export class CambiaPassword {
  private readonly api = inject(ApiService);
  attuale = '';
  nuova = '';
  conferma = '';
  readonly messaggio = signal<string | null>(null);
  readonly ok = signal(false);

  async salva() {
    if (this.nuova !== this.conferma) {
      this.ok.set(false);
      this.messaggio.set('Le password non coincidono.');
      return;
    }
    try {
      await this.api.cambiaPassword(this.attuale, this.nuova);
      this.ok.set(true);
      this.messaggio.set('Password cambiata.');
      this.attuale = this.nuova = this.conferma = '';
    } catch (e) {
      this.ok.set(false);
      this.messaggio.set(messaggioErrore(e));
    }
  }
}
