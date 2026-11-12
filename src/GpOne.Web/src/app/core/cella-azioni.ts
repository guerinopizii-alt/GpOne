import { Component, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ICellRendererAngularComp } from 'ag-grid-angular';
import { ICellRendererParams } from 'ag-grid-community';

export interface AzioneRiga<T> {
  icona: string;
  titolo: string;
  esegui: (riga: T) => void;
  visibile?: (riga: T) => boolean;
  disabilitata?: (riga: T) => boolean;
}

export type ParametriAzioni<T> = ICellRendererParams<T> & { azioni: AzioneRiga<T>[] };

/** Cella AG Grid con i pulsanti-icona delle azioni sulla riga (modifica, elimina, …). */
@Component({
  selector: 'app-cella-azioni',
  imports: [MatButtonModule, MatIconModule],
  template: `
    @if (riga(); as r) {
      @for (a of azioni(); track a.icona) {
        @if (!a.visibile || a.visibile(r)) {
          <button mat-icon-button [title]="a.titolo" [attr.aria-label]="a.titolo"
                  [disabled]="a.disabilitata?.(r) ?? false" (click)="a.esegui(r)">
            <mat-icon>{{ a.icona }}</mat-icon>
          </button>
        }
      }
    }
  `,
  styles: `
    :host { display: flex; align-items: center; height: 100%; }
    button { --mat-icon-button-state-layer-size: 32px; padding: 4px; }
    mat-icon { font-size: 20px; width: 20px; height: 20px; }
  `
})
export class CellaAzioni<T> implements ICellRendererAngularComp {
  protected readonly riga = signal<T | undefined>(undefined);
  protected readonly azioni = signal<AzioneRiga<T>[]>([]);

  agInit(params: ParametriAzioni<T>) {
    this.refresh(params);
  }

  refresh(params: ParametriAzioni<T>): boolean {
    this.riga.set(params.data);
    this.azioni.set(params.azioni);
    return true;
  }
}
