import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef } from 'ag-grid-community';
import { CellaAzioni, ParametriAzioni } from '../core/cella-azioni';
import { formattaDataOra, opzioniGriglia, temaGriglia } from '../core/griglia';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ApiService, dettagliErrore, messaggioErrore } from '../core/api.service';
import { RiquadroMessaggio } from '../core/riquadro-messaggio';
import { ClienteGestione, UtenteGestione } from '../core/modelli';
import { SessioneService } from '../core/sessione.service';

@Component({
  selector: 'app-admin-utenti',
  imports: [FormsModule, RiquadroMessaggio, AgGridAngular, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <div class="pagina">
      @if (messaggio(); as m) { <app-messaggio [testo]="m" [errore]="!ok()" [dettagli]="dettagli()" chiudibile (chiudi)="messaggio.set(null)" /> }

      @if (inModifica(); as u) {
        <form class="card" (ngSubmit)="salva(u)">
          <h2>{{ u.id ? 'Modifica ' + u.username : 'Nuovo utente' }}</h2>
          <div class="riga">
            <mat-form-field><mat-label>Nome utente</mat-label><input matInput name="username" [(ngModel)]="u.username" autocomplete="off" /></mat-form-field>
            <mat-form-field><mat-label>Nome e cognome</mat-label><input matInput name="nome" [(ngModel)]="u.nomeCompleto" /></mat-form-field>
            <mat-form-field><mat-label>Email</mat-label><input matInput name="email" [(ngModel)]="u.email" /></mat-form-field>
          </div>
          <div class="riga">
            <mat-form-field>
              <mat-label>{{ u.id ? 'Nuova password (vuota = invariata)' : 'Password' }}</mat-label>
              <input matInput type="password" name="password" [(ngModel)]="u.password" autocomplete="new-password" />
            </mat-form-field>
            <mat-checkbox name="attivo" [(ngModel)]="u.attivo">Attivo</mat-checkbox>
            <mat-checkbox name="admin" [(ngModel)]="u.amministratore">Amministratore (vede tutti i clienti)</mat-checkbox>
          </div>
          <mat-form-field class="piena">
            <mat-label>Clienti abbinati</mat-label>
            <mat-select name="clienti" multiple [(ngModel)]="u.clientiIds">
              @for (c of clienti(); track c.id) { <mat-option [value]="c.id">{{ c.ragioneSociale }} ({{ c.codice }})</mat-option> }
            </mat-select>
          </mat-form-field>
          <div class="riga">
            <button mat-flat-button class="gradiente" type="submit"><mat-icon>save</mat-icon>Salva</button>
            <button mat-button type="button" (click)="inModifica.set(null)">Annulla</button>
          </div>
        </form>
      } @else {
        <div class="card">
          <div class="riga">
            <span class="bolla-icona rosa"><mat-icon class="icona-piena">group</mat-icon></span>
            <div><h2>Utenti</h2><div class="muto">Chi può accedere a GpOne e a quali clienti</div></div>
            <span class="spazio"></span>
            <button mat-flat-button class="gradiente" (click)="apri()"><mat-icon>person_add</mat-icon>Nuovo utente</button>
          </div>
          <ag-grid-angular [theme]="tema" [gridOptions]="opzioni" [columnDefs]="colonne" [rowData]="utenti()" domLayout="autoHeight" />
        </div>
      }
    </div>
  `
})
export class AdminUtenti implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly sessione = inject(SessioneService);

  readonly utenti = signal<UtenteGestione[]>([]);
  readonly clienti = signal<ClienteGestione[]>([]);
  readonly inModifica = signal<UtenteGestione | null>(null);
  readonly messaggio = signal<string | null>(null);
  readonly ok = signal(true);
  readonly dettagli = signal<string | null>(null);

  protected readonly tema = temaGriglia;
  protected readonly opzioni = opzioniGriglia<UtenteGestione>({
    autoSizeStrategy: { type: 'fitGridWidth' },
    getRowId: p => String(p.data.id),
  });
  protected readonly colonne: ColDef<UtenteGestione>[] = [
    { headerName: 'Utente', field: 'username', minWidth: 120 },
    { headerName: 'Nome', field: 'nomeCompleto', minWidth: 140 },
    { headerName: 'Ruolo', valueGetter: p => p.data?.amministratore ? 'Amministratore' : 'Utente', minWidth: 120, cellClassRules: { 'cella-viola': p => p.value === 'Amministratore' } },
    {
      headerName: 'Clienti', minWidth: 140,
      valueGetter: p => !p.data ? '' : p.data.amministratore ? 'Tutti' : this.nomiClienti(p.data.clientiIds),
      tooltip: true,
    },
    { headerName: 'Stato', valueGetter: p => !p.data ? '' : !p.data.attivo ? 'Disattivato' : this.bloccato(p.data) ? 'Bloccato' : 'Attivo', minWidth: 110,
      cellClassRules: { 'cella-ok': p => p.value === 'Attivo', 'cella-attenzione': p => p.value === 'Bloccato', 'cella-ko': p => p.value === 'Disattivato' } },
    {
      headerName: 'Ultimo accesso', field: 'ultimoAccesso', minWidth: 150, filter: false,
      valueFormatter: p => formattaDataOra(p.value),
    },
    {
      headerName: '', colId: 'azioni', width: 130, minWidth: 130, maxWidth: 130, sortable: false, filter: false, resizable: false,
      cellRenderer: CellaAzioni,
      cellRendererParams: {
        azioni: [
          { icona: 'lock_open', titolo: 'Sblocca', visibile: u => this.bloccato(u), esegui: u => this.sblocca(u) },
          { icona: 'edit', titolo: 'Modifica', esegui: u => this.apri(u) },
          { icona: 'delete', titolo: 'Elimina', disabilitata: u => u.id === this.sessione.utente()?.id, esegui: u => this.elimina(u) },
        ]
      } satisfies Partial<ParametriAzioni<UtenteGestione>>,
    },
  ];

  ngOnInit() {
    this.esegui(() => this.carica());
  }

  apri(u?: UtenteGestione) {
    this.messaggio.set(null);
    this.inModifica.set(u
      ? { ...u, password: null, clientiIds: [...u.clientiIds] }
      : { id: 0, username: '', email: null, nomeCompleto: null, amministratore: false, attivo: true, bloccatoFino: null, ultimoAccesso: null, password: '', clientiIds: [] });
  }

  salva(u: UtenteGestione) {
    this.esegui(async () => {
      await this.api.salvaUtente(u);
      this.inModifica.set(null);
      await this.carica();
      return 'Utente salvato.';
    });
  }

  sblocca(u: UtenteGestione) {
    this.esegui(async () => { await this.api.sbloccaUtente(u.id); await this.carica(); return `Utente ${u.username} sbloccato.`; });
  }

  elimina(u: UtenteGestione) {
    if (!confirm(`Eliminare l'utente ${u.username}?`)) return;
    this.esegui(async () => { await this.api.eliminaUtente(u.id); await this.carica(); return `Utente ${u.username} eliminato.`; });
  }

  bloccato(u: UtenteGestione) {
    return !!u.bloccatoFino && new Date(u.bloccatoFino).getTime() > Date.now();
  }

  nomiClienti(ids: number[]) {
    return this.clienti().filter(c => ids.includes(c.id)).map(c => c.codice).join(', ');
  }

  private async carica() {
    const [utenti, clienti] = await Promise.all([this.api.utenti(), this.api.clienti()]);
    this.utenti.set(utenti);
    this.clienti.set(clienti);
  }

  private async esegui(azione: () => Promise<string | void>) {
    try {
      const testo = await azione();
      this.ok.set(true);
      this.messaggio.set(testo ?? null);
      this.dettagli.set(null);
    } catch (e) {
      this.ok.set(false);
      this.messaggio.set(messaggioErrore(e));
      this.dettagli.set(dettagliErrore(e));
    }
  }
}
