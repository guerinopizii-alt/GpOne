export interface Utente {
  id: number;
  username: string;
  nomeCompleto: string | null;
  amministratore: boolean;
}

export interface LoginRisposta {
  token: string;
  scadenza: string;
  utente: Utente;
}

export interface ClienteBreve {
  id: number;
  codice: string;
  ragioneSociale: string;
}

export interface Grafico {
  tipo: 'barre' | 'linee' | 'torta' | string;
  x: string;
  y: string[];
}

export interface ColonnaRisultato {
  nome: string;
  tipo: string;
}

export type Valutazione = 'Corretta' | 'Sbagliata';

export interface RispostaDomanda {
  id: number;
  domanda: string;
  sql: string | null;
  spiegazione: string;
  grafico: Grafico | null;
  colonne: ColonnaRisultato[];
  righe: unknown[][];
  troncato: boolean;
  durataMs: number;
  errore: string | null;
  /** Solo lato client: valutazione data dall'utente a questa risposta. */
  valutazione?: Valutazione | null;
  /** Domanda di chiarimento dell'AI per l'utente (nessuna query eseguita). */
  chiarimento?: string | null;
  /** Per rispondere sono stati inviati all'AI risultati anonimizzati. */
  datiInviati?: boolean;
}

export interface VoceStorico {
  id: number;
  testo: string;
  sqlGenerato: string | null;
  esito: string;
  righe: number | null;
  dataOra: string;
  valutazione: Valutazione | null;
}

export interface UtenteGestione {
  id: number;
  username: string;
  email: string | null;
  nomeCompleto: string | null;
  amministratore: boolean;
  attivo: boolean;
  bloccatoFino: string | null;
  ultimoAccesso: string | null;
  password?: string | null;
  clientiIds: number[];
}

export interface ClienteGestione {
  id: number;
  codice: string;
  ragioneSociale: string;
  attivo: boolean;
  utentiIds: number[];
  chiaveAgenteGenerata: boolean;
  agenteCollegato: boolean;
  agenteUltimoContatto: string | null;
  agenteVersione: string | null;
}

export interface ConfigurazioneAi {
  clienteId: number;
  provider: 'Claude' | 'Gemini' | 'GeminiVertex' | 'Ollama';
  modello: string;
  /** Ollama: indirizzo del server; Vertex AI: regione (vuota = global). */
  endpoint: string | null;
  /** In scrittura: nuova chiave API, o JSON dell'account di servizio per Vertex AI (vuoto = invariato). Mai restituita. */
  apiKey: string | null;
  apiKeyImpostata: boolean;
  /** Solo lettura, Vertex AI: account di servizio e progetto salvati. */
  accountServizio?: string | null;
  effort: string | null;
  istruzioni: string | null;
  maxRighe: number;
  timeoutSecondi: number;
  /** Chiamate all'AI al massimo per una domanda. */
  maxChiamateAi: number;
  /** Se le query non bastano, l'AI può ricevere risultati anonimizzati (testi a token). */
  invioDatiAnonimizzati: boolean;
  maxRigheAnonimizzate: number;
}

export interface ColonnaConfigurata {
  id: number;
  oggettoId: number;
  nome: string;
  tipoDati: string;
  ordine: number;
  abilitata: boolean;
  descrizione: string | null;
}

export interface OggettoConfigurato {
  id: number;
  schemaNome: string;
  nome: string;
  tipo: string;
  abilitato: boolean;
  descrizione: string | null;
  colonne: ColonnaConfigurata[];
}
