import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
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
import { ClienteGestione, ConfigurazioneAi, UtenteGestione } from '../core/modelli';

/** Modello proposto per un nuovo cliente con Claude (l'elenco completo si ottiene con "Carica modelli"). */
const MODELLO_CLAUDE_PREDEFINITO = 'claude-opus-5-5';

@Component({
  selector: 'app-admin-clienti',
  imports: [FormsModule, RiquadroMessaggio, AgGridAngular, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <div class="pagina">
      @if (messaggio(); as m) { <app-messaggio [testo]="m" [errore]="!ok()" [dettagli]="dettagli()" chiudibile (chiudi)="messaggio.set(null)" /> }

      @if (chiave(); as k) {
        <div class="card">
          <h3>Chiave dell'agente per il cliente {{ k.clienteId }}</h3>
          <p>Copiala ora nella configurazione dell'agente presso il cliente: non verrà più mostrata.
             Si consiglia di cifrarla con <code>GpOne.Agente.exe --cifra &lt;chiave&gt;</code>.</p>
          <pre class="sql">"GpOneAgente": {{ '{' }} "ServerUrl": "{{ origine }}", "ClienteId": {{ k.clienteId }}, "Chiave": "{{ k.chiave }}" {{ '}' }}</pre>
          <button mat-button (click)="chiave.set(null)">Chiudi</button>
        </div>
      }

      @if (inModifica(); as c) {
        <form class="card" (ngSubmit)="salva(c)">
          <h2>{{ c.id ? 'Modifica ' + c.ragioneSociale : 'Nuovo cliente' }}</h2>
          <div class="riga">
            <mat-form-field><mat-label>Codice</mat-label><input matInput name="codice" [(ngModel)]="c.codice" /></mat-form-field>
            <mat-form-field class="largo"><mat-label>Ragione sociale</mat-label><input matInput name="rs" [(ngModel)]="c.ragioneSociale" /></mat-form-field>
            <mat-checkbox name="attivo" [(ngModel)]="c.attivo">Attivo</mat-checkbox>
          </div>
          <mat-form-field class="piena">
            <mat-label>Utenti abbinati</mat-label>
            <mat-select name="utenti" multiple [(ngModel)]="c.utentiIds">
              @for (u of utenti(); track u.id) { <mat-option [value]="u.id">{{ u.nomeCompleto || u.username }}</mat-option> }
            </mat-select>
          </mat-form-field>

          @if (ai(); as a) {
            <h3 class="con-icona"><span class="bolla-icona piccola arancio"><mat-icon class="icona-piena">auto_awesome</mat-icon></span>Motore AI</h3>
            <p class="muto">L'AI riceve solo la struttura delle tabelle abilitate e la domanda: i dati, se servono, solo anonimizzati.</p>
            <div class="riga">
              <mat-form-field>
                <mat-label>Motore</mat-label>
                <mat-select name="provider" [(ngModel)]="a.provider" (ngModelChange)="cambiaProvider(a)">
                  <mat-option value="Claude">Claude (Anthropic)</mat-option>
                  <mat-option value="Gemini">Gemini (Google AI Studio)</mat-option>
                  <mat-option value="GeminiVertex">Gemini su Vertex AI (Google Cloud)</mat-option>
                  <mat-option value="Ollama">Ollama (locale)</mat-option>
                </mat-select>
              </mat-form-field>
              @switch (a.provider) {
                @case ('Claude') {
                  <mat-form-field>
                    <mat-label>Effort</mat-label>
                    <mat-select name="effort" [(ngModel)]="a.effort">
                      @for (e of ['low', 'medium', 'high', 'xhigh', 'max']; track e) { <mat-option [value]="e">{{ e }}</mat-option> }
                    </mat-select>
                  </mat-form-field>
                  <mat-form-field class="largo">
                    <mat-label>{{ chiaveSalvata(a) ? 'Chiave API (vuota = invariata)' : 'Chiave API' }}</mat-label>
                    <input matInput type="password" name="apiKey" [(ngModel)]="a.apiKey" autocomplete="off" />
                  </mat-form-field>
                }
                @case ('Gemini') {
                  <mat-form-field class="largo">
                    <mat-label>{{ chiaveSalvata(a) ? 'Chiave API Google AI Studio (vuota = invariata)' : 'Chiave API Google AI Studio' }}</mat-label>
                    <input matInput type="password" name="apiKeyGemini" [(ngModel)]="a.apiKey" autocomplete="off" />
                  </mat-form-field>
                }
                @case ('GeminiVertex') {
                  <mat-form-field>
                    <mat-label>Regione</mat-label>
                    <input matInput name="regione" [(ngModel)]="a.endpoint" placeholder="es. europe-west8 (vuota = global)" />
                  </mat-form-field>
                  <div class="account">
                    <input #fileAccount type="file" accept=".json,application/json" hidden (change)="caricaAccount($event, a)" />
                    <button mat-stroked-button type="button" (click)="fileAccount.click()"><mat-icon>upload_file</mat-icon>Carica JSON account di servizio</button>
                    <span class="muto">{{ a.accountServizio ?? 'Account di servizio non impostato' }}</span>
                  </div>
                }
                @default {
                  <mat-form-field class="largo">
                    <mat-label>Indirizzo Ollama</mat-label>
                    <input matInput name="endpoint" [(ngModel)]="a.endpoint" placeholder="http://localhost:11434" />
                  </mat-form-field>
                }
              }
            </div>
            @if (a.apiKeyImpostata && !chiaveSalvata(a)) {
              <p class="muto">Cambiando motore la chiave salvata del motore precedente viene eliminata: inserire quella del nuovo motore.</p>
            }
            <div class="riga">
              <mat-form-field><mat-label>Righe massime</mat-label><input matInput type="number" name="maxRighe" [(ngModel)]="a.maxRighe" /></mat-form-field>
              <mat-form-field><mat-label>Timeout query (s)</mat-label><input matInput type="number" name="timeout" [(ngModel)]="a.timeoutSecondi" /></mat-form-field>
              <mat-form-field><mat-label>Chiamate AI per domanda</mat-label><input matInput type="number" name="maxChiamateAi" [(ngModel)]="a.maxChiamateAi" min="1" max="10" /></mat-form-field>
            </div>
            <div class="riga">
              <mat-checkbox name="invioDatiAnonimizzati" [(ngModel)]="a.invioDatiAnonimizzati">
                Se le query non bastano, consenti di inviare all'AI risultati anonimizzati (testi sostituiti da token)
              </mat-checkbox>
              @if (a.invioDatiAnonimizzati) {
                <mat-form-field><mat-label>Righe anonimizzate max</mat-label><input matInput type="number" name="maxRigheAnonimizzate" [(ngModel)]="a.maxRigheAnonimizzate" min="1" max="500" /></mat-form-field>
              }
            </div>
            <mat-form-field class="piena">
              <mat-label>Indicazioni per l'AI su questo database (facoltative)</mat-label>
              <textarea matInput rows="3" name="istruzioni" [(ngModel)]="a.istruzioni"
                        placeholder="es. Le fatture annullate hanno Stato = 'A' e vanno escluse dai totali."></textarea>
            </mat-form-field>
            <div class="riga">
              <mat-form-field class="largo">
                <mat-label>Modello</mat-label>
                @if (modelli().length) {
                  <mat-select name="modello" [(ngModel)]="a.modello">
                    @for (m of modelli(); track m) { <mat-option [value]="m">{{ m }}</mat-option> }
                  </mat-select>
                } @else {
                  <input matInput name="modello" [(ngModel)]="a.modello" [placeholder]="esempioModello(a)" />
                }
              </mat-form-field>
              <button mat-stroked-button type="button" [disabled]="!puoCaricareModelli(a) || caricamentoModelli()" (click)="caricaModelli(a)">
                <mat-icon>refresh</mat-icon>{{ caricamentoModelli() ? 'Caricamento…' : 'Carica modelli' }}
              </button>
            </div>
          }

          <div class="riga">
            <button mat-flat-button class="gradiente" type="submit"><mat-icon>save</mat-icon>Salva</button>
            <button mat-button type="button" (click)="inModifica.set(null)">Annulla</button>
          </div>
        </form>
      } @else {
        <div class="card">
          <div class="riga">
            <span class="bolla-icona viola"><mat-icon class="icona-piena">domain</mat-icon></span>
            <div><h2>Clienti</h2><div class="muto">Database collegati, agenti e motore AI</div></div>
            <span class="spazio"></span>
            <button mat-flat-button class="gradiente" (click)="apri()"><mat-icon>add_business</mat-icon>Nuovo cliente</button>
          </div>
          <ag-grid-angular [theme]="tema" [gridOptions]="opzioni" [columnDefs]="colonne" [rowData]="clienti()" domLayout="autoHeight" />
        </div>
      }
    </div>
  `,
  styles: `
    .largo { min-width: 320px; }
    .account { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; min-height: 56px; }
  `
})
export class AdminClienti implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  protected readonly tema = temaGriglia;
  protected readonly opzioni = opzioniGriglia<ClienteGestione>({
    autoSizeStrategy: { type: 'fitGridWidth' },
    getRowId: p => String(p.data.id),
  });
  protected readonly colonne: ColDef<ClienteGestione>[] = [
    { headerName: 'Id', field: 'id', width: 80, minWidth: 70, filter: 'agNumberColumnFilter' },
    { headerName: 'Codice', field: 'codice', minWidth: 100 },
    { headerName: 'Ragione sociale', field: 'ragioneSociale', minWidth: 200 },
    { headerName: 'Utenti', valueGetter: p => p.data?.utentiIds.length, width: 90, minWidth: 80, filter: 'agNumberColumnFilter', type: 'rightAligned' },
    {
      headerName: 'Agente', minWidth: 240,
      valueGetter: p => {
        const c = p.data;
        if (!c) return '';
        const stato = c.agenteCollegato ? '● collegato' : c.chiaveAgenteGenerata ? '○ non collegato' : 'chiave da generare';
        return c.agenteUltimoContatto ? `${stato} · ${formattaDataOra(c.agenteUltimoContatto)} · v${c.agenteVersione}` : stato;
      },
      cellClassRules: { 'cella-ok': p => !!p.data?.agenteCollegato, 'cella-muta': p => !p.data?.agenteCollegato },
    },
    { headerName: 'Stato', valueGetter: p => p.data?.attivo ? 'Attivo' : 'Disattivato', minWidth: 110, cellClassRules: { 'cella-ok': p => p.value === 'Attivo', 'cella-ko': p => p.value !== 'Attivo' } },
    {
      headerName: '', colId: 'azioni', width: 170, minWidth: 170, maxWidth: 170, sortable: false, filter: false, resizable: false,
      cellRenderer: CellaAzioni,
      cellRendererParams: {
        azioni: [
          { icona: 'schema', titolo: "Tabelle e colonne per l'AI", esegui: c => this.router.navigate(['/admin/clienti', c.id, 'schema']) },
          { icona: 'key', titolo: 'Genera chiave agente', esegui: c => this.generaChiave(c) },
          { icona: 'edit', titolo: 'Modifica', esegui: c => this.apri(c) },
          { icona: 'delete', titolo: 'Elimina', esegui: c => this.elimina(c) },
        ]
      } satisfies Partial<ParametriAzioni<ClienteGestione>>,
    },
  ];

  /** Modelli caricati per il motore scelto (vuoto = campo libero). */
  readonly modelli = signal<string[]>([]);
  readonly caricamentoModelli = signal(false);
  readonly origine = location.origin;
  readonly clienti = signal<ClienteGestione[]>([]);
  readonly utenti = signal<UtenteGestione[]>([]);
  readonly inModifica = signal<ClienteGestione | null>(null);
  readonly ai = signal<ConfigurazioneAi | null>(null);
  /** Motore salvato sul server: la chiave salvata vale solo per lui. */
  private providerSalvato: ConfigurazioneAi['provider'] | null = null;
  private accountSalvato: string | null = null;
  readonly chiave = signal<{ clienteId: number; chiave: string } | null>(null);
  readonly messaggio = signal<string | null>(null);
  readonly ok = signal(true);
  /** Dettagli tecnici dell'ultimo errore (risposta del server), mostrati sotto il messaggio. */
  readonly dettagli = signal<string | null>(null);

  ngOnInit() {
    this.esegui(() => this.carica());
  }

  async apri(c?: ClienteGestione) {
    this.messaggio.set(null);
    this.dettagli.set(null);
    this.inModifica.set(c
      ? { ...c, utentiIds: [...c.utentiIds] }
      : { id: 0, codice: '', ragioneSociale: '', attivo: true, utentiIds: [], chiaveAgenteGenerata: false, agenteCollegato: false, agenteUltimoContatto: null, agenteVersione: null });
    this.ai.set(null);
    this.modelli.set([]);
    this.providerSalvato = null;
    await this.esegui(async () => {
      const ai: ConfigurazioneAi = c
        ? await this.api.configurazioneAi(c.id)
        : { clienteId: 0, provider: 'Claude', modello: MODELLO_CLAUDE_PREDEFINITO, endpoint: null, apiKey: null, apiKeyImpostata: false, effort: 'medium', istruzioni: null, maxRighe: 1000, timeoutSecondi: 30, maxChiamateAi: 5, invioDatiAnonimizzati: true, maxRigheAnonimizzate: 50 };
      this.providerSalvato = ai.apiKeyImpostata ? ai.provider : null;
      this.accountSalvato = ai.accountServizio ?? null;
      this.ai.set(ai);
    });
  }

  /** La chiave salvata sul server vale per il motore scelto (cambiando motore va inserita quella nuova). */
  chiaveSalvata(a: ConfigurazioneAi) {
    return a.apiKeyImpostata && a.provider === this.providerSalvato;
  }

  /** Si possono caricare i modelli se c'è la chiave (nuova o salvata per questo motore), l'account di servizio o l'indirizzo di Ollama. */
  puoCaricareModelli(a: ConfigurazioneAi) {
    return a.provider === 'Ollama' ? !!a.endpoint?.trim() : !!a.apiKey?.trim() || this.chiaveSalvata(a);
  }

  esempioModello(a: ConfigurazioneAi) {
    return a.provider === 'Claude' ? 'es. claude-opus-5-5' : a.provider === 'Ollama' ? 'es. qwen2.5-coder:14b' : 'es. gemini-2.5-pro';
  }

  /** Elenco dei modelli validi per il motore scelto (la chiave va al server, che la usa solo per interrogare il fornitore). */
  async caricaModelli(a: ConfigurazioneAi) {
    const provider = a.provider;
    this.caricamentoModelli.set(true);
    try {
      const elenco = await this.api.modelliAi({ ...a, clienteId: this.inModifica()?.id ?? 0 });
      if (a.provider !== provider) return; // motore cambiato nel frattempo
      if (!elenco.length) {
        this.ok.set(false);
        this.messaggio.set(`Nessun modello utilizzabile trovato per ${provider}.`);
        this.dettagli.set(null);
        return;
      }
      if (!elenco.includes(a.modello)) a.modello = elenco[0];
      this.modelli.set(elenco);
      this.ok.set(true);
      this.messaggio.set(null);
      this.dettagli.set(null);
    } catch (e) {
      this.ok.set(false);
      this.messaggio.set(`Impossibile caricare i modelli (${provider}): ${messaggioErrore(e)}`);
      this.dettagli.set(dettagliErrore(e));
    } finally {
      this.caricamentoModelli.set(false);
    }
  }

  cambiaProvider(a: ConfigurazioneAi) {
    this.modelli.set([]);
    a.modello = a.provider === 'Claude' ? MODELLO_CLAUDE_PREDEFINITO : '';
    a.apiKey = null;
    a.accountServizio = a.provider === this.providerSalvato ? this.accountSalvato : null;
    a.endpoint = a.provider === 'Ollama' ? 'http://localhost:11434' : null;
  }

  /** Legge il JSON dell'account di servizio Google: va al server (che lo cifra) e qui si mostra solo account e progetto. */
  async caricaAccount(evento: Event, a: ConfigurazioneAi) {
    const input = evento.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    try {
      const testo = await file.text();
      const json = JSON.parse(testo) as { type?: string; project_id?: string; client_email?: string; private_key?: string };
      if (json.type !== 'service_account' || !json.project_id || !json.client_email || !json.private_key)
        throw new Error('Il file non è la chiave JSON di un account di servizio Google Cloud.');
      a.apiKey = testo;
      a.accountServizio = `${json.client_email} (progetto ${json.project_id}) – da salvare`;
      this.ok.set(true);
      this.messaggio.set(null);
      this.dettagli.set(null);
    } catch (e) {
      this.ok.set(false);
      this.messaggio.set(e instanceof SyntaxError ? 'Il file non è un JSON valido.' : messaggioErrore(e));
      this.dettagli.set(null);
    }
  }

  salva(c: ClienteGestione) {
    this.esegui(async () => {
      const { id } = await this.api.salvaCliente(c);
      const ai = this.ai();
      if (ai) await this.api.salvaConfigurazioneAi({ ...ai, clienteId: id });
      this.inModifica.set(null);
      await this.carica();
      return 'Cliente salvato.';
    });
  }

  generaChiave(c: ClienteGestione) {
    if (c.chiaveAgenteGenerata && !confirm(`Generare una nuova chiave per ${c.ragioneSociale}? L'agente attuale smetterà di collegarsi.`)) return;
    this.esegui(async () => {
      this.chiave.set(await this.api.generaChiaveAgente(c.id));
      await this.carica();
    });
  }

  elimina(c: ClienteGestione) {
    if (!confirm(`Eliminare il cliente ${c.ragioneSociale} con la sua configurazione e lo storico delle domande?`)) return;
    this.esegui(async () => { await this.api.eliminaCliente(c.id); await this.carica(); return `Cliente ${c.ragioneSociale} eliminato.`; });
  }

  private async carica() {
    const [clienti, utenti] = await Promise.all([this.api.clienti(), this.api.utenti()]);
    this.clienti.set(clienti);
    this.utenti.set(utenti);
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
