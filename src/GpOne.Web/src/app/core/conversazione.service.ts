import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { ApiService, messaggioErrore } from './api.service';
import { RispostaDomanda, VoceStorico } from './modelli';
import { SessioneService } from './sessione.service';

/** Un passaggio della chat: la domanda (o la correzione) dell'utente e la risposta di GpOne. */
export interface Turno {
  chiave: number;
  domanda: string;
  /** Correzione inviata con "Sbagliata, riprova" (la domanda è quella originale). */
  correzione?: string;
  risposta?: RispostaDomanda;
  errore?: string;
  inAttesa: boolean;
}

/**
 * Conversazione corrente con GpOne (solo in memoria) e storico delle domande del cliente scelto.
 * La conversazione si azzera cambiando cliente o con "Nuova conversazione".
 */
@Injectable({ providedIn: 'root' })
export class ConversazioneService {
  private readonly api = inject(ApiService);
  private readonly sessione = inject(SessioneService);
  private progressivo = 0;

  readonly turni = signal<Turno[]>([]);
  readonly storico = signal<VoceStorico[]>([]);
  readonly occupato = computed(() => this.turni().some(t => t.inAttesa));

  constructor() {
    effect(() => {
      const cliente = this.sessione.cliente();
      untracked(() => {
        this.turni.set([]);
        this.storico.set([]);
        if (cliente) this.aggiornaStorico();
      });
    });
  }

  nuova() {
    if (!this.occupato()) this.turni.set([]);
  }

  chiedi(testo: string) {
    return this.esegui({ domanda: testo }, () => this.api.chiedi(testo));
  }

  /** Riapre una domanda dello storico: la query salvata viene rieseguita (senza chiamare l'AI). */
  apri(voce: VoceStorico) {
    return this.esegui({ domanda: voce.testo }, () => this.api.apri(voce.id));
  }

  riprova(precedente: RispostaDomanda, commento: string) {
    return this.esegui({ domanda: precedente.domanda, correzione: commento || 'Risposta sbagliata, riprova.' },
      () => this.api.riprova(precedente.id, commento));
  }

  rispondiChiarimento(precedente: RispostaDomanda, risposta: string) {
    return this.esegui({ domanda: precedente.domanda, correzione: `Chiarimento: ${risposta}` },
      () => this.api.rispondiChiarimento(precedente.id, risposta));
  }

  async valuta(turno: Turno, corretta: boolean) {
    const r = turno.risposta;
    if (!r) return;
    await this.api.valuta(r.id, corretta);
    this.aggiorna(turno.chiave, { risposta: { ...r, valutazione: corretta ? 'Corretta' : null } });
    this.aggiornaStorico();
  }

  async aggiornaStorico() {
    try {
      this.storico.set(await this.api.storico(30));
    } catch {
      // lo storico non è essenziale
    }
  }

  private async esegui(dati: Pick<Turno, 'domanda' | 'correzione'>, chiamata: () => Promise<RispostaDomanda>) {
    if (this.occupato()) return;
    const chiave = ++this.progressivo;
    this.turni.update(t => [...t, { chiave, ...dati, inAttesa: true }]);
    try {
      this.aggiorna(chiave, { risposta: await chiamata(), inAttesa: false });
      this.aggiornaStorico();
    } catch (e) {
      this.aggiorna(chiave, { errore: messaggioErrore(e), inAttesa: false });
    }
  }

  private aggiorna(chiave: number, modifiche: Partial<Turno>) {
    this.turni.update(t => t.map(x => x.chiave === chiave ? { ...x, ...modifiche } : x));
  }
}
