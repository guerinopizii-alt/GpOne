import { Injectable, computed, signal } from '@angular/core';
import { ClienteBreve, LoginRisposta, Utente } from './modelli';

const CHIAVE_SESSIONE = 'gpone.sessione';
const CHIAVE_CLIENTE = 'gpone.cliente';

interface SessioneSalvata {
  token: string;
  scadenza: string;
  utente: Utente;
}

function leggi<T>(chiave: string): T | null {
  try {
    const valore = sessionStorage.getItem(chiave);
    return valore ? (JSON.parse(valore) as T) : null;
  } catch {
    return null;
  }
}

function scrivi(chiave: string, valore: unknown): void {
  try {
    if (valore === null) sessionStorage.removeItem(chiave);
    else sessionStorage.setItem(chiave, JSON.stringify(valore));
  } catch {
    // sessionStorage non disponibile: la sessione resta solo in memoria
  }
}

/** Token JWT, utente collegato e cliente scelto (in sessionStorage: chiudendo la scheda si esce). */
@Injectable({ providedIn: 'root' })
export class SessioneService {
  private readonly sessione = signal<SessioneSalvata | null>(this.sessioneValida(leggi<SessioneSalvata>(CHIAVE_SESSIONE)));
  readonly cliente = signal<ClienteBreve | null>(leggi<ClienteBreve>(CHIAVE_CLIENTE));
  readonly clienti = signal<ClienteBreve[]>([]);

  readonly utente = computed(() => this.sessione()?.utente ?? null);
  readonly autenticato = computed(() => this.sessione() !== null);
  readonly amministratore = computed(() => this.sessione()?.utente.amministratore === true);

  get token(): string | null {
    const s = this.sessioneValida(this.sessione());
    return s?.token ?? null;
  }

  accedi(risposta: LoginRisposta): void {
    const s: SessioneSalvata = { token: risposta.token, scadenza: risposta.scadenza, utente: risposta.utente };
    scrivi(CHIAVE_SESSIONE, s);
    this.sessione.set(s);
    this.scegliCliente(null);
  }

  scegliCliente(cliente: ClienteBreve | null): void {
    scrivi(CHIAVE_CLIENTE, cliente);
    this.cliente.set(cliente);
  }

  esci(): void {
    scrivi(CHIAVE_SESSIONE, null);
    this.sessione.set(null);
    this.scegliCliente(null);
    this.clienti.set([]);
  }

  private sessioneValida(s: SessioneSalvata | null): SessioneSalvata | null {
    return s && new Date(s.scadenza).getTime() > Date.now() ? s : null;
  }
}
