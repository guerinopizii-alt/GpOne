import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

const COLORI = [
  'linear-gradient(135deg, #7c4dff, #a77bff)', 'linear-gradient(135deg, #ff4f8b, #ff86b0)',
  'linear-gradient(135deg, #ff8a3d, #ffc061)', 'linear-gradient(135deg, #1fb6e0, #5ad8f5)',
  'linear-gradient(135deg, #14b88f, #4fe3bd)',
];
import { ApiService, messaggioErrore } from '../core/api.service';
import { RiquadroMessaggio } from '../core/riquadro-messaggio';
import { ClienteBreve } from '../core/modelli';
import { SessioneService } from '../core/sessione.service';

@Component({
  selector: 'app-scegli-cliente',
  imports: [FormsModule, RiquadroMessaggio, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <div class="pagina">
      <div class="titolo-pagina">
        <span class="bolla-icona"><mat-icon class="icona-piena">apartment</mat-icon></span>
        <div>
          <h2>Scegli il cliente</h2>
          <div class="sottotitolo">Su quali dati vuoi fare domande?</div>
        </div>
      </div>
      @if (errore(); as e) { <app-messaggio [testo]="e" errore /> }
      @if (sessione.clienti().length === 0 && !errore()) {
        <div class="card vuoto">
          <mat-icon>sentiment_dissatisfied</mat-icon>
          <p>Nessun cliente abbinato al tuo utente: chiedi all'amministratore di abilitarti.</p>
        </div>
      } @else {
        <mat-form-field class="cerca" subscriptSizing="dynamic">
          <mat-icon matPrefix>search</mat-icon>
          <mat-label>Cerca cliente</mat-label>
          <input matInput [ngModel]="cerca()" (ngModelChange)="cerca.set($event)" />
        </mat-form-field>
        <div class="griglia-clienti">
          @for (c of visibili(); track c.id; let i = $index) {
            <button type="button" class="cliente" [class.scelto]="c.id === sessione.cliente()?.id" (click)="scegli(c)">
              <span class="iniziale" [style.background]="colore(i)">{{ c.ragioneSociale.trim()[0].toUpperCase() }}</span>
              <span class="dati">
                <span class="nome">{{ c.ragioneSociale }}</span>
                <span class="codice">{{ c.codice }}</span>
              </span>
              @if (c.id === sessione.cliente()?.id) {
                <mat-icon class="icona-piena spunta">check_circle</mat-icon>
              } @else {
                <mat-icon class="freccia">arrow_forward</mat-icon>
              }
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .pagina { max-width: 900px; }
    .cerca { width: min(420px, 100%); margin-bottom: 16px; }
    .griglia-clienti { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 14px; }
    .cliente {
      display: flex; align-items: center; gap: 14px; padding: 16px; cursor: pointer; text-align: left; font: inherit; color: inherit;
      background: var(--mat-sys-surface-container-lowest); border: 2px solid transparent; border-radius: 18px;
      box-shadow: var(--gp-ombra); transition: transform .15s, border-color .15s, box-shadow .15s;
    }
    .cliente:hover { transform: translateY(-2px); border-color: color-mix(in srgb, var(--gp-viola) 50%, transparent); box-shadow: 0 12px 28px rgb(124 77 255 / .18); }
    .cliente.scelto { border-color: var(--gp-viola); }
    .iniziale { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 14px; color: #fff; font-size: 20px; font-weight: 800; flex: none; }
    .dati { display: flex; flex-direction: column; min-width: 0; flex: 1; }
    .nome { font-weight: 700; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .codice { font-size: 12px; color: var(--mat-sys-on-surface-variant); }
    .freccia { color: var(--mat-sys-on-surface-variant); }
    .spunta { color: var(--gp-viola); }
    .vuoto { display: flex; align-items: center; gap: 12px; color: var(--mat-sys-on-surface-variant); }
  `
})
export class ScegliCliente implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  protected readonly sessione = inject(SessioneService);

  readonly cerca = signal('');
  readonly errore = signal<string | null>(null);
  readonly visibili = computed(() => {
    const testo = this.cerca().toLowerCase();
    return this.sessione.clienti().filter(c => !testo || `${c.codice} ${c.ragioneSociale}`.toLowerCase().includes(testo));
  });

  async ngOnInit() {
    try {
      this.sessione.clienti.set(await this.api.mieiClienti());
    } catch (e) {
      this.errore.set(messaggioErrore(e));
    }
  }

  colore(indice: number) {
    return COLORI[indice % COLORI.length];
  }

  scegli(cliente: ClienteBreve) {
    this.sessione.scegliCliente(cliente);
    this.router.navigate(['/chiedi']);
  }
}
