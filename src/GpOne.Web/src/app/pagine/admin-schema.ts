import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef } from 'ag-grid-community';
import { ApiService, dettagliErrore, messaggioErrore } from '../core/api.service';
import { RiquadroMessaggio } from '../core/riquadro-messaggio';
import { opzioniGriglia, temaGriglia } from '../core/griglia';
import { ColonnaConfigurata, OggettoConfigurato } from '../core/modelli';

/** Tabelle e colonne del DB cliente che l'AI può conoscere, con le descrizioni che finiscono nel prompt. */
@Component({
  selector: 'app-admin-schema',
  imports: [FormsModule, RiquadroMessaggio, RouterLink, AgGridAngular,MatButtonModule, MatCheckboxModule, MatExpansionModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule],
  template: `
    <div class="pagina">
      <div class="card">
        <div class="riga">
          <a mat-icon-button routerLink="/admin/clienti" title="Torna ai clienti"><mat-icon>arrow_back</mat-icon></a>
          <span class="bolla-icona ciano"><mat-icon class="icona-piena">schema</mat-icon></span>
          <div><h2>Struttura per l'AI</h2><div class="muto">Cliente {{ clienteId() }} · tabelle e colonne che l'AI può conoscere</div></div>
          <span class="spazio"></span>
          <button mat-stroked-button (click)="leggi()" [disabled]="occupato()"><mat-icon>sync</mat-icon>Rileggi dal database</button>
          <button mat-flat-button class="gradiente" (click)="salva()" [disabled]="occupato()"><mat-icon>save</mat-icon>Salva</button>
        </div>
        <p class="muto">
          La struttura si legge tramite l'agente installato presso il cliente (solo nomi e tipi, nessun dato).
          Abilita solo le tabelle utili e descrivi in italiano tabelle e colonne: le query dell'AI migliorano molto.
          Le colonne disabilitate non sono note all'AI e non possono comparire nelle query.
        </p>
        @if (occupato()) { <mat-progress-bar mode="indeterminate" /> }
        @if (messaggio(); as m) { <app-messaggio [testo]="m" [errore]="!ok()" [dettagli]="dettagli()" chiudibile (chiudi)="messaggio.set(null)" /> }
        <div class="riga">
          <mat-form-field class="largo"><mat-label>Cerca tabella</mat-label><input matInput [ngModel]="cerca()" (ngModelChange)="cerca.set($event)" /></mat-form-field>
          <mat-checkbox [ngModel]="soloAbilitati()" (ngModelChange)="soloAbilitati.set($event)">Solo abilitate</mat-checkbox>
          <span class="muto">{{ abilitati() }} abilitate su {{ oggetti().length }}</span>
        </div>
      </div>

      <mat-accordion multi>
        @for (o of visibili(); track o.id) {
          <mat-expansion-panel>
            <mat-expansion-panel-header>
              <mat-panel-title>
                <mat-checkbox [(ngModel)]="o.abilitato" (click)="$event.stopPropagation()" (ngModelChange)="aggiorna()" />
                {{ o.schemaNome }}.{{ o.nome }}
              </mat-panel-title>
              <mat-panel-description>{{ o.tipo.toLowerCase() }} · {{ o.colonne.length }} colonne {{ o.descrizione ? '· ' + o.descrizione : '' }}</mat-panel-description>
            </mat-expansion-panel-header>
            <ng-template matExpansionPanelContent>
              <mat-form-field class="piena">
                <mat-label>Descrizione della tabella</mat-label>
                <input matInput [(ngModel)]="o.descrizione" />
              </mat-form-field>
              <ag-grid-angular [theme]="tema" [gridOptions]="opzioni" [columnDefs]="colonne" [rowData]="o.colonne" domLayout="autoHeight" />
            </ng-template>
          </mat-expansion-panel>
        }
      </mat-accordion>
    </div>
  `,
  styles: `
    .largo { min-width: 300px; }
  `
})
export class AdminSchema implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly tema = temaGriglia;
  protected readonly opzioni = opzioniGriglia<ColonnaConfigurata>({
    getRowId: p => String(p.data.id),
    singleClickEdit: true,
    stopEditingWhenCellsLoseFocus: true,
    enterNavigatesVerticallyAfterEdit: true,
  });
  protected readonly colonne: ColDef<ColonnaConfigurata>[] = [
    { headerName: 'Usa', field: 'abilitata', cellDataType: 'boolean', editable: true, width: 80, resizable: false },
    { headerName: 'Colonna', field: 'nome', width: 220 },
    { headerName: 'Tipo', field: 'tipoDati', width: 140, cellClass: 'cella-muta' },
    {
      headerName: 'Descrizione (clic per modificare)', field: 'descrizione', flex: 1, minWidth: 260, editable: true,
      cellEditorParams: { maxLength: 1000 },
      valueParser: p => p.newValue?.trim() || null,
      valueFormatter: p => p.value || 'Significato, unità di misura, valori ammessi…',
      cellClassRules: { 'cella-vuota': p => !p.value },
      tooltip: p => p.data?.descrizione ?? null,
    },
  ];

  readonly clienteId = input.required<string>();
  readonly oggetti = signal<OggettoConfigurato[]>([]);
  readonly cerca = signal('');
  readonly soloAbilitati = signal(false);
  readonly versione = signal(0);
  readonly occupato = signal(false);
  readonly messaggio = signal<string | null>(null);
  readonly ok = signal(true);
  readonly dettagli = signal<string | null>(null);

  readonly abilitati = computed(() => (this.versione(), this.oggetti().filter(o => o.abilitato).length));
  readonly visibili = computed(() => {
    this.versione();
    const testo = this.cerca().toLowerCase();
    return this.oggetti().filter(o =>
      (!this.soloAbilitati() || o.abilitato) && (!testo || `${o.schemaNome}.${o.nome}`.toLowerCase().includes(testo)));
  });

  private get id() { return Number(this.clienteId()); }

  ngOnInit() {
    this.esegui(async () => { await this.carica(); });
  }

  aggiorna() {
    this.versione.update(v => v + 1);
  }

  leggi() {
    this.esegui(async () => {
      const { oggetti } = await this.api.leggiSchema(this.id);
      await this.carica();
      return `Struttura aggiornata: ${oggetti} tabelle e viste.`;
    });
  }

  salva() {
    this.esegui(async () => { await this.api.salvaSchema(this.id, this.oggetti()); return 'Configurazione salvata.'; });
  }

  private async carica() {
    this.oggetti.set(await this.api.schema(this.id));
  }

  private async esegui(azione: () => Promise<string | void>) {
    this.occupato.set(true);
    try {
      const testo = await azione();
      this.ok.set(true);
      this.messaggio.set(testo ?? null);
      this.dettagli.set(null);
    } catch (e) {
      this.ok.set(false);
      this.messaggio.set(messaggioErrore(e));
      this.dettagli.set(dettagliErrore(e));
    } finally {
      this.occupato.set(false);
    }
  }
}
