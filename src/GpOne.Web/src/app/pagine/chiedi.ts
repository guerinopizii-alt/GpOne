import { Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TextFieldModule } from '@angular/cdk/text-field';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService, dettagliErrore, messaggioErrore } from '../core/api.service';
import { RiquadroMessaggio } from '../core/riquadro-messaggio';
import { ConversazioneService, Turno } from '../core/conversazione.service';
import { SessioneService } from '../core/sessione.service';
import { GraficoRisultato } from './grafico';
import { TabellaRisultato } from './tabella-risultato';

interface Suggerimento { icona: string; colore: string; titolo: string; testo: string; }

const SUGGERIMENTI: Suggerimento[] = [
  { icona: 'trending_up', colore: 'viola', titolo: 'Andamento', testo: 'Fatturato per mese di quest\'anno confrontato con l\'anno scorso' },
  { icona: 'workspace_premium', colore: 'rosa', titolo: 'Classifiche', testo: 'I 10 clienti con il fatturato più alto' },
  { icona: 'shopping_cart', colore: 'arancio', titolo: 'Ordini', testo: 'Ordini aperti raggruppati per stato' },
  { icona: 'inventory_2', colore: 'ciano', titolo: 'Prodotti', testo: 'I prodotti più venduti negli ultimi 3 mesi' },
];

/** Chat con GpOne: domande in linguaggio naturale, risposte con tabella, grafico e SQL. */
@Component({
  selector: 'app-chiedi',
  imports: [FormsModule, TextFieldModule, RiquadroMessaggio, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTabsModule, MatTooltipModule,
    GraficoRisultato, TabellaRisultato],
  template: `
    <div class="flusso" #flusso>
      <div class="colonna">
        @if (conversazione.turni().length === 0) {
          <section class="benvenuto">
            <img src="img/benvenuto.svg" alt="" width="300" height="200" />
            <h1>Ciao {{ nome() }}, <span class="testo-gradiente">cosa vuoi sapere?</span></h1>
            <p class="sottotitolo">
              Chiedi in italiano qualsiasi cosa sui dati di <strong>{{ sessione.cliente()?.ragioneSociale }}</strong>:
              rispondo con tabelle e grafici.
            </p>
            <div class="privacy">
              <mat-icon class="icona-piena">verified_user</mat-icon>
              L'AI vede la struttura del database: se servono dati, le vengono inviati solo anonimizzati.
            </div>
            <div class="suggerimenti">
              @for (s of suggerimenti; track s.testo) {
                <button type="button" class="suggerimento" (click)="invia(s.testo)">
                  <span class="bolla-icona piccola {{ s.colore }}"><mat-icon class="icona-piena">{{ s.icona }}</mat-icon></span>
                  <span class="testo">
                    <span class="titolo">{{ s.titolo }}</span>
                    <span>{{ s.testo }}</span>
                  </span>
                </button>
              }
            </div>
          </section>
        }

        @for (t of conversazione.turni(); track t.chiave) {
          <div class="messaggio utente">
            <div class="fumetto">
              @if (t.correzione) {
                <span class="etichetta-correzione"><mat-icon>edit_note</mat-icon>Correzione</span>
                {{ t.correzione }}
              } @else {
                {{ t.domanda }}
              }
            </div>
            <div class="avatar utente" [attr.aria-label]="nome()">{{ iniziali() }}</div>
          </div>

          <div class="messaggio gpone">
            <img class="avatar" src="img/logo.svg" alt="GpOne" width="36" height="36" />
            <div class="risposta">
              @if (t.inAttesa) {
                <div class="in-attesa">
                  <span class="puntini"><i></i><i></i><i></i></span>
                  Preparo la query e la eseguo sul database…
                </div>
              } @else if (t.errore) {
                <app-messaggio [testo]="t.errore" errore />
              } @else if (t.risposta; as r) {
                @if (r.chiarimento) {
                  <p class="spiegazione"><mat-icon class="in-linea">help</mat-icon> {{ r.chiarimento }}</p>
                } @else if (r.spiegazione) { <p class="spiegazione">{{ r.spiegazione }}</p> }
                @if (r.datiInviati) {
                  <span class="etichetta-stato attenzione" matTooltip="Le query non bastavano: all'AI sono stati inviati risultati con i testi sostituiti da token, mai i valori reali.">
                    <mat-icon>shield</mat-icon>dati anonimizzati inviati all'AI
                  </span>
                }
                @if (r.errore) { <app-messaggio [testo]="r.errore" errore titolo="La query non è andata a buon fine" /> }

                @if (r.sql) {
                  <div class="scheda">
                    <div class="intestazione">
                      <span class="etichetta-stato viola"><mat-icon>table_rows</mat-icon>{{ r.righe.length }} righe</span>
                      @if (r.troncato) { <span class="etichetta-stato attenzione"><mat-icon>content_cut</mat-icon>troncato</span> }
                      <span class="etichetta-stato neutro"><mat-icon>timer</mat-icon>{{ (r.durataMs / 1000).toFixed(1) }} s</span>
                      <span class="spazio"></span>
                      @if (r.colonne.length) {
                        <button mat-icon-button matTooltip="Scarica Excel" (click)="esporta(r.id, 'xlsx')"><mat-icon class="excel">table_view</mat-icon></button>
                        <button mat-icon-button matTooltip="Scarica CSV" (click)="esporta(r.id, 'csv')"><mat-icon class="csv">download</mat-icon></button>
                      }
                    </div>
                    <mat-tab-group [selectedIndex]="r.grafico && r.righe.length ? 1 : 0" animationDuration="200ms" mat-stretch-tabs="false">
                      @if (r.colonne.length) {
                        <mat-tab>
                          <ng-template mat-tab-label><mat-icon>grid_on</mat-icon>Tabella</ng-template>
                          <app-tabella-risultato [colonne]="r.colonne" [righe]="r.righe" />
                        </mat-tab>
                      }
                      @if (r.grafico && r.righe.length) {
                        <mat-tab>
                          <ng-template mat-tab-label><mat-icon>bar_chart</mat-icon>Grafico</ng-template>
                          <app-grafico [grafico]="r.grafico" [colonne]="r.colonne" [righe]="r.righe" />
                        </mat-tab>
                      }
                      <mat-tab>
                        <ng-template mat-tab-label><mat-icon>code</mat-icon>SQL</ng-template>
                        <pre class="sql">{{ r.sql }}</pre>
                      </mat-tab>
                    </mat-tab-group>
                  </div>
                }

                <div class="valutazione">
                  @if (r.chiarimento) {
                    <mat-form-field class="piena" subscriptSizing="dynamic">
                      <mat-label>La tua risposta</mat-label>
                      <textarea matInput cdkTextareaAutosize cdkAutosizeMinRows="1" [(ngModel)]="rispostaChiarimento"
                                (keydown.control.enter)="rispondiChiarimento(t)" placeholder="es. il 2025"></textarea>
                    </mat-form-field>
                    <div class="riga">
                      <button mat-flat-button class="gradiente" [disabled]="conversazione.occupato() || !rispostaChiarimento.trim()"
                              (click)="rispondiChiarimento(t)"><mat-icon>send</mat-icon>Rispondi</button>
                    </div>
                  } @else if (r.valutazione === 'Corretta') {
                    <span class="etichetta-stato ok"><mat-icon class="icona-piena">verified</mat-icon>Confermata: diventa un esempio per le prossime domande</span>
                    <button mat-button [disabled]="conversazione.occupato()" (click)="valuta(t, false)">Annulla</button>
                  } @else if (inCorrezione() === t.chiave) {
                    <mat-form-field class="piena" subscriptSizing="dynamic">
                      <mat-label>Cosa c'è di sbagliato?</mat-label>
                      <textarea matInput cdkTextareaAutosize cdkAutosizeMinRows="2" [(ngModel)]="commento"
                                (keydown.control.enter)="riprova(t)"
                                placeholder="es. il fatturato va calcolato senza le note di credito"></textarea>
                      <mat-hint>Il commento va all'AI insieme alla query, mai i risultati.</mat-hint>
                    </mat-form-field>
                    <div class="riga">
                      <button mat-flat-button class="gradiente" [disabled]="conversazione.occupato()" (click)="riprova(t)"><mat-icon>refresh</mat-icon>Riprova</button>
                      <button mat-button (click)="inCorrezione.set(null)">Annulla</button>
                    </div>
                  } @else {
                    <span class="muto">È corretta?</span>
                    @if (r.sql && !r.errore) {
                      <button mat-icon-button class="pollice su" matTooltip="Sì, confermala" [disabled]="conversazione.occupato()" (click)="valuta(t, true)">
                        <mat-icon>thumb_up</mat-icon>
                      </button>
                    }
                    <button mat-icon-button class="pollice giu" matTooltip="No, correggila" [disabled]="conversazione.occupato()" (click)="apriCorrezione(t)">
                      <mat-icon>thumb_down</mat-icon>
                    </button>
                  }
                </div>
              }
            </div>
          </div>
        }
      </div>
    </div>

    @if (avviso(); as a) {
      <div class="avviso-pagina"><app-messaggio [testo]="a.testo" [dettagli]="a.dettagli" errore chiudibile (chiudi)="avviso.set(null)" /></div>
    }
    <form class="compositore" (ngSubmit)="invia()">
      <div class="campo">
        <mat-icon class="icona-campo">auto_awesome</mat-icon>
        <textarea name="testo" [(ngModel)]="testo" cdkTextareaAutosize cdkAutosizeMinRows="1" cdkAutosizeMaxRows="6"
                  [placeholder]="'Chiedi qualcosa su ' + (sessione.cliente()?.ragioneSociale ?? 'i tuoi dati') + '…'"
                  (keydown.enter)="invioDaTastiera($event)" aria-label="Domanda"></textarea>
        <button type="submit" class="invia" [disabled]="conversazione.occupato() || !testo.trim()" aria-label="Invia">
          <mat-icon class="icona-piena">arrow_upward</mat-icon>
        </button>
      </div>
      <div class="nota">Invio per inviare · Maiusc+Invio per andare a capo</div>
    </form>
  `,
  styles: `
    :host { display: flex; flex-direction: column; height: 100%; min-height: 0; }
    .flusso { flex: 1; overflow-y: auto; min-height: 0; }
    .avviso-pagina { max-width: 980px; width: 100%; margin: 0 auto; padding: 0 20px; box-sizing: border-box; }
    .colonna { max-width: 980px; margin: 0 auto; padding: 28px 20px 12px; }

    /* Benvenuto */
    .benvenuto { text-align: center; padding: 12px 0 8px; animation: entra .4s ease-out; }
    .benvenuto img { width: min(300px, 80%); height: auto; }
    .benvenuto h1 { margin: 8px 0 6px; font-size: clamp(24px, 4vw, 34px); line-height: 1.2; }
    .sottotitolo { color: var(--mat-sys-on-surface-variant); margin: 0 auto 14px; max-width: 560px; font-size: 15px; }
    .privacy {
      display: inline-flex; align-items: center; gap: 8px; padding: 8px 14px; border-radius: 999px;
      background: rgb(45 212 167 / .14); color: #0f8f6e; font-size: 13px; font-weight: 600;
    }
    .privacy mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .suggerimenti { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 12px; margin-top: 26px; text-align: left; }
    .suggerimento {
      display: flex; gap: 12px; align-items: flex-start; padding: 14px; cursor: pointer; font: inherit; color: inherit;
      background: var(--mat-sys-surface-container-lowest); border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 16px; box-shadow: var(--gp-ombra); transition: transform .15s, box-shadow .15s, border-color .15s;
    }
    .suggerimento:hover { transform: translateY(-2px); border-color: var(--gp-viola); box-shadow: 0 12px 28px rgb(124 77 255 / .18); }
    .suggerimento .testo { display: flex; flex-direction: column; gap: 2px; font-size: 13px; color: var(--mat-sys-on-surface-variant); }
    .suggerimento .titolo { font-weight: 700; color: var(--mat-sys-on-surface); font-size: 14px; }

    /* Messaggi */
    .messaggio { display: flex; gap: 12px; margin: 18px 0; animation: entra .25s ease-out; }
    .messaggio.utente { justify-content: flex-end; }
    .avatar { width: 36px; height: 36px; border-radius: 12px; flex: none; }
    .avatar.utente {
      display: grid; place-items: center; font-weight: 700; font-size: 13px; color: #fff;
      background: var(--gp-gradiente-freddo);
    }
    .fumetto {
      max-width: min(640px, 80%); padding: 12px 16px; border-radius: 18px 18px 4px 18px;
      background: var(--gp-gradiente); color: #fff; font-size: 15px; line-height: 1.45;
      box-shadow: 0 8px 22px rgb(255 79 139 / .25); white-space: pre-wrap;
    }
    .etichetta-correzione { display: flex; align-items: center; gap: 4px; font-size: 12px; font-weight: 700; opacity: .9; margin-bottom: 2px; }
    .etichetta-correzione mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .risposta { flex: 1; min-width: 0; }
    .spiegazione { margin: 6px 0 12px; font-size: 15px; line-height: 1.55; }
    .scheda {
      background: var(--mat-sys-surface-container-lowest); border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 18px; box-shadow: var(--gp-ombra); padding: 12px 16px 16px;
    }
    .intestazione { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .etichetta-stato mat-icon { font-size: 15px; width: 15px; height: 15px; }
    mat-icon.excel { color: #1d9b5c; }
    mat-icon.csv { color: var(--gp-viola); }
    .mat-mdc-tab-group mat-icon { margin-right: 6px; font-size: 20px; width: 20px; height: 20px; }
    pre.sql { margin-top: 12px; }
    .in-linea { vertical-align: middle; width: 20px; height: 20px; font-size: 20px; }
    .valutazione { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; margin-top: 10px; }
    .pollice.su:not(:disabled) { color: #14b88f; }
    .pollice.giu:not(:disabled) { color: var(--gp-rosa); }

    .in-attesa { display: flex; align-items: center; gap: 12px; color: var(--mat-sys-on-surface-variant); padding: 8px 0; }
    .puntini { display: inline-flex; gap: 5px; }
    .puntini i { width: 9px; height: 9px; border-radius: 50%; animation: salta 1s infinite ease-in-out; }
    .puntini i:nth-child(1) { background: var(--gp-viola); }
    .puntini i:nth-child(2) { background: var(--gp-rosa); animation-delay: .15s; }
    .puntini i:nth-child(3) { background: var(--gp-arancio); animation-delay: .3s; }

    /* Casella della domanda, fissa in basso */
    .compositore { padding: 8px 20px 14px; }
    .campo {
      max-width: 940px; margin: 0 auto; display: flex; align-items: flex-end; gap: 10px;
      padding: 8px 8px 8px 16px; border-radius: 22px;
      background: var(--mat-sys-surface-container-lowest);
      border: 2px solid transparent;
      background-image: linear-gradient(var(--mat-sys-surface-container-lowest), var(--mat-sys-surface-container-lowest)), var(--gp-gradiente);
      background-origin: border-box; background-clip: padding-box, border-box;
      box-shadow: 0 10px 30px rgb(124 77 255 / .18);
    }
    .icona-campo { color: var(--gp-viola); margin-bottom: 9px; }
    textarea {
      flex: 1; border: none; outline: none; resize: none; background: transparent; color: inherit;
      font-family: inherit; font-size: 15px; line-height: 1.5; padding: 8px 0;
    }
    .invia {
      width: 42px; height: 42px; border-radius: 14px; border: none; cursor: pointer; display: grid; place-items: center;
      color: #fff; background: var(--gp-gradiente); box-shadow: 0 6px 16px rgb(255 79 139 / .35); transition: transform .15s, opacity .15s;
    }
    .invia:hover:not(:disabled) { transform: scale(1.06); }
    .invia:disabled { opacity: .35; cursor: default; box-shadow: none; }
    .nota { text-align: center; font-size: 11px; color: var(--mat-sys-on-surface-variant); margin-top: 6px; }

    @keyframes entra { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
    @keyframes salta { 0%, 80%, 100% { transform: translateY(0); opacity: .5; } 40% { transform: translateY(-6px); opacity: 1; } }
    @media (prefers-reduced-motion: reduce) { * { animation: none !important; transition: none !important; } }
    @media (max-width: 600px) {
      .colonna { padding: 16px 12px 8px; }
      .compositore { padding: 6px 10px 10px; }
      .avatar.utente { display: none; }
      .fumetto { max-width: 88%; }
    }
  `
})
export class Chiedi {
  private readonly api = inject(ApiService);
  protected readonly sessione = inject(SessioneService);
  protected readonly conversazione = inject(ConversazioneService);
  private readonly flusso = viewChild.required<ElementRef<HTMLElement>>('flusso');

  protected readonly suggerimenti = SUGGERIMENTI;
  testo = '';
  commento = '';
  rispostaChiarimento = '';
  readonly inCorrezione = signal<number | null>(null);
  /** Errore di un'azione (valutazione, export), mostrato sopra la casella della domanda. */
  readonly avviso = signal<{ testo: string; dettagli: string | null } | null>(null);

  protected readonly nome = computed(() => {
    const u = this.sessione.utente();
    return (u?.nomeCompleto || u?.username || '').split(' ')[0];
  });
  protected readonly iniziali = computed(() => {
    const u = this.sessione.utente();
    const parti = (u?.nomeCompleto || u?.username || '?').trim().split(/\s+/);
    return (parti[0][0] + (parti[1]?.[0] ?? '')).toUpperCase();
  });

  constructor() {
    // A ogni nuovo messaggio (o risposta arrivata) si scorre in fondo
    effect(() => {
      this.conversazione.turni();
      setTimeout(() => {
        const el = this.flusso().nativeElement;
        el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' });
      });
    });
  }

  invioDaTastiera(evento: Event) {
    if ((evento as KeyboardEvent).shiftKey) return;
    evento.preventDefault();
    this.invia();
  }

  invia(testo = this.testo) {
    if (!testo.trim() || this.conversazione.occupato()) return;
    this.testo = '';
    this.conversazione.chiedi(testo.trim());
  }

  apriCorrezione(t: Turno) {
    this.commento = '';
    this.inCorrezione.set(t.chiave);
  }

  rispondiChiarimento(t: Turno) {
    const risposta = this.rispostaChiarimento.trim();
    if (!t.risposta || !risposta || this.conversazione.occupato()) return;
    this.rispostaChiarimento = '';
    this.conversazione.rispondiChiarimento(t.risposta, risposta);
  }

  riprova(t: Turno) {
    if (!t.risposta || this.conversazione.occupato()) return;
    this.inCorrezione.set(null);
    this.conversazione.riprova(t.risposta, this.commento.trim());
  }

  async valuta(t: Turno, corretta: boolean) {
    try {
      await this.conversazione.valuta(t, corretta);
    } catch (e) {
      this.avviso.set({ testo: messaggioErrore(e), dettagli: dettagliErrore(e) });
    }
  }

  async esporta(id: number, formato: 'xlsx' | 'csv') {
    try {
      const risposta = await this.api.export(id, formato);
      const nome = /filename="?([^";]+)"?/.exec(risposta.headers.get('content-disposition') ?? '')?.[1] ?? `GpOne.${formato}`;
      const url = URL.createObjectURL(risposta.body!);
      const link = document.createElement('a');
      link.href = url;
      link.download = nome;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      this.avviso.set({ testo: messaggioErrore(e), dettagli: dettagliErrore(e) });
    }
  }
}
