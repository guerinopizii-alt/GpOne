import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ClienteBreve, ClienteGestione, ConfigurazioneAi, LoginRisposta, OggettoConfigurato,
  RispostaDomanda, UtenteGestione, VoceStorico
} from './modelli';

/** Messaggio leggibile da un errore HTTP (ProblemDetails dell'API). */
export function messaggioErrore(errore: unknown): string {
  if (errore instanceof HttpErrorResponse) {
    if (errore.status === 0) return 'Server GpOne non raggiungibile.';
    const corpo = errore.error as { detail?: string; title?: string } | null;
    return corpo?.detail ?? corpo?.title ?? `Errore ${errore.status}`;
  }
  return errore instanceof Error ? errore.message : String(errore);
}

/** Dettagli tecnici di un errore (stato HTTP, indirizzo, risposta del server), da mostrare in modo copiabile. */
export function dettagliErrore(errore: unknown): string | null {
  if (errore instanceof HttpErrorResponse) {
    if (errore.status === 0) return `Nessuna risposta da ${errore.url ?? 'server'}.`;
    const corpo = typeof errore.error === 'string' ? errore.error : JSON.stringify(errore.error, null, 2);
    return `${new Date().toLocaleString('it-IT')}\nHTTP ${errore.status} ${errore.statusText} – ${errore.url}\n${corpo ?? ''}`;
  }
  return errore instanceof Error && errore.stack ? errore.stack : null;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  // ---- Accesso ----
  stato() { return firstValueFrom(this.http.get<{ primoAvvio: boolean }>('/api/auth/stato')); }
  login(username: string, password: string) {
    return firstValueFrom(this.http.post<LoginRisposta>('/api/auth/login', { username, password }));
  }
  primoAmministratore(dati: { username: string; password: string; email?: string; nomeCompleto?: string }) {
    return firstValueFrom(this.http.post<LoginRisposta>('/api/auth/primo-amministratore', dati));
  }
  cambiaPassword(passwordAttuale: string, nuovaPassword: string) {
    return firstValueFrom(this.http.post<void>('/api/auth/cambia-password', { passwordAttuale, nuovaPassword }));
  }
  mieiClienti() { return firstValueFrom(this.http.get<ClienteBreve[]>('/api/me/clienti')); }

  // ---- Domande (cliente nell'header X-Cliente-Id, aggiunto dall'interceptor) ----
  chiedi(testo: string) { return firstValueFrom(this.http.post<RispostaDomanda>('/api/domande', { testo })); }
  /** La risposta è sbagliata: l'AI riceve la query e il commento (mai i risultati) e ne propone una nuova. */
  rispondiChiarimento(id: number, risposta: string) {
    return firstValueFrom(this.http.post<RispostaDomanda>(`/api/domande/${id}/chiarimento`, { risposta }));
  }

  riprova(id: number, commento: string) {
    return firstValueFrom(this.http.post<RispostaDomanda>(`/api/domande/${id}/riprova`, { commento }));
  }
  /** Conferma (o annulla la conferma) della risposta: le risposte corrette diventano esempi per l'AI. */
  valuta(id: number, corretta: boolean) {
    return firstValueFrom(this.http.put<void>(`/api/domande/${id}/valutazione`, { corretta }));
  }
  /** Riapre una domanda dello storico: l'API riesegue la query salvata, senza chiamare l'AI. */
  apri(id: number) { return firstValueFrom(this.http.get<RispostaDomanda>(`/api/domande/${id}`)); }
  storico(limite = 50) {
    return firstValueFrom(this.http.get<VoceStorico[]>('/api/domande', { params: new HttpParams().set('limite', limite) }));
  }
  export(id: number, formato: 'xlsx' | 'csv') {
    return firstValueFrom(this.http.get(`/api/domande/${id}/export`, {
      params: new HttpParams().set('formato', formato), responseType: 'blob', observe: 'response'
    }));
  }

  // ---- Amministrazione ----
  utenti() { return firstValueFrom(this.http.get<UtenteGestione[]>('/api/admin/utenti')); }
  salvaUtente(u: UtenteGestione) {
    return firstValueFrom(u.id ? this.http.put('/api/admin/utenti/' + u.id, u) : this.http.post('/api/admin/utenti', u));
  }
  eliminaUtente(id: number) { return firstValueFrom(this.http.delete('/api/admin/utenti/' + id)); }
  sbloccaUtente(id: number) { return firstValueFrom(this.http.post('/api/admin/utenti/' + id + '/sblocca', {})); }

  clienti() { return firstValueFrom(this.http.get<ClienteGestione[]>('/api/admin/clienti')); }
  salvaCliente(c: ClienteGestione) {
    return firstValueFrom(c.id
      ? this.http.put<{ id: number }>('/api/admin/clienti/' + c.id, c)
      : this.http.post<{ id: number }>('/api/admin/clienti', c));
  }
  eliminaCliente(id: number) { return firstValueFrom(this.http.delete('/api/admin/clienti/' + id)); }
  generaChiaveAgente(id: number) {
    return firstValueFrom(this.http.post<{ clienteId: number; chiave: string }>(`/api/admin/clienti/${id}/chiave-agente`, {}));
  }

  configurazioneAi(clienteId: number) { return firstValueFrom(this.http.get<ConfigurazioneAi>(`/api/admin/clienti/${clienteId}/ai`)); }
  salvaConfigurazioneAi(c: ConfigurazioneAi) { return firstValueFrom(this.http.put(`/api/admin/clienti/${c.clienteId}/ai`, c)); }
  /** Modelli utilizzabili con il motore indicato: chiave nuova in apiKey, oppure (vuota) quella salvata per lo stesso motore. */
  modelliAi(c: ConfigurazioneAi) { return firstValueFrom(this.http.post<string[]>(`/api/admin/clienti/${c.clienteId}/ai/modelli`, c)); }

  schema(clienteId: number) { return firstValueFrom(this.http.get<OggettoConfigurato[]>(`/api/admin/clienti/${clienteId}/schema`)); }
  leggiSchema(clienteId: number) { return firstValueFrom(this.http.post<{ oggetti: number }>(`/api/admin/clienti/${clienteId}/schema/leggi`, {})); }
  salvaSchema(clienteId: number, oggetti: OggettoConfigurato[]) {
    return firstValueFrom(this.http.put(`/api/admin/clienti/${clienteId}/schema`, oggetti));
  }
}
