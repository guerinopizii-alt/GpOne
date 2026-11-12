import { Component, booleanAttribute, computed, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/**
 * Messaggio (errore o informazione) in una casella di testo a sola lettura che va a capo:
 * si può selezionare e copiare, anche tutto con il pulsante Copia. Equivale a MessaggioForm del desktop.
 */
@Component({
  selector: 'app-messaggio',
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="riquadro" [class.errore]="errore()" [attr.role]="errore() ? 'alert' : 'status'">
      <div class="intestazione">
        <mat-icon class="icona-piena">{{ errore() ? 'error' : 'info' }}</mat-icon>
        <span class="titolo">{{ titolo() || (errore() ? 'Si è verificato un errore' : 'Informazione') }}</span>
        <span class="spazio"></span>
        <button mat-button type="button" (click)="copia()"><mat-icon>content_copy</mat-icon>{{ statoCopia() }}</button>
        @if (chiudibile()) {
          <button mat-icon-button type="button" title="Chiudi" (click)="chiudi.emit()"><mat-icon>close</mat-icon></button>
        }
      </div>
      <textarea readonly [value]="contenuto()" aria-label="Testo del messaggio"></textarea>
    </div>
  `,
  styles: `
    :host { display: block; }
    .riquadro {
      border: 1px solid color-mix(in srgb, var(--mat-sys-outline-variant) 70%, transparent);
      border-left: 4px solid var(--mat-sys-primary);
      border-radius: 12px;
      padding: 6px 10px 10px 12px;
      background: var(--mat-sys-surface-container-lowest);
      margin: 8px 0;
    }
    .riquadro.errore { border-left-color: var(--mat-sys-error); background: rgb(255 79 139 / .06); }
    .intestazione { display: flex; align-items: center; gap: 8px; min-height: 40px; }
    .intestazione > mat-icon { color: var(--mat-sys-primary); }
    .errore .intestazione > mat-icon, .errore .titolo { color: var(--mat-sys-error); }
    .titolo { font-weight: 600; }
    .spazio { flex: 1; }
    textarea {
      display: block; width: 100%; box-sizing: border-box;
      font: var(--mat-sys-body-medium); color: var(--mat-sys-on-surface);
      background: var(--mat-sys-surface); border: 1px solid var(--mat-sys-outline-variant); border-radius: 8px;
      padding: 8px 10px;
      white-space: pre-wrap; overflow-wrap: anywhere; /* va a capo, anche nelle parole lunghe (URL, nomi di classi) */
      field-sizing: content; min-height: 2.6em; max-height: 16em; overflow: auto; resize: vertical;
    }
  `
})
export class RiquadroMessaggio {
  readonly testo = input.required<string>();
  /** Dettagli tecnici (es. risposta completa del server), mostrati sotto il messaggio. */
  readonly dettagli = input<string | null | undefined>(null);
  readonly errore = input(false, { transform: booleanAttribute });
  readonly titolo = input<string>('');
  readonly chiudibile = input(false, { transform: booleanAttribute });
  readonly chiudi = output<void>();

  readonly contenuto = computed(() => {
    const d = this.dettagli();
    return d?.trim() ? `${this.testo()}\n\n--- Dettagli ---\n${d}` : this.testo();
  });
  readonly statoCopia = signal('Copia');

  async copia() {
    try {
      await navigator.clipboard.writeText(this.contenuto());
      this.statoCopia.set('Copiato');
    } catch {
      this.statoCopia.set('Appunti non disponibili');
    }
    setTimeout(() => this.statoCopia.set('Copia'), 2000);
  }
}
