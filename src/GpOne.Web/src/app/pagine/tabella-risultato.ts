import { Component, computed, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridOptions } from 'ag-grid-community';
import { opzioniGriglia, temaGriglia } from '../core/griglia';
import { ColonnaRisultato } from '../core/modelli';

const NUMERICI = ['int', 'bigint', 'smallint', 'tinyint', 'decimal', 'numeric', 'money', 'smallmoney', 'float', 'real'];
const RIGHE_ALTEZZA_AUTOMATICA = 15;

type Riga = unknown[];

/** Tabella dei risultati di una domanda: ordinamento, filtri per colonna, ricerca veloce. */
@Component({
  selector: 'app-tabella-risultato',
  imports: [AgGridAngular, FormsModule, MatFormFieldModule, MatIconModule, MatInputModule],
  template: `
    <mat-form-field class="cerca" subscriptSizing="dynamic">
      <mat-icon matPrefix>search</mat-icon>
      <mat-label>Cerca nei risultati</mat-label>
      <input matInput [ngModel]="cerca()" (ngModelChange)="cerca.set($event)" />
    </mat-form-field>
    <ag-grid-angular
      [class.altezza-fissa]="!altezzaAutomatica()"
      [theme]="tema"
      [rowData]="righe()"
      [columnDefs]="definizioni()"
      [gridOptions]="opzioni"
      [domLayout]="altezzaAutomatica() ? 'autoHeight' : 'normal'"
      [quickFilterText]="cerca()" />
  `,
  styles: `
    :host { display: block; padding-top: 12px; }
    .cerca { width: min(360px, 100%); margin-bottom: 8px; }
    ag-grid-angular { width: 100%; }
    ag-grid-angular.altezza-fissa { height: 60vh; }
  `
})
export class TabellaRisultato {
  readonly colonne = input.required<ColonnaRisultato[]>();
  readonly righe = input.required<Riga[]>();

  protected readonly tema = temaGriglia;
  protected readonly cerca = signal('');
  protected readonly altezzaAutomatica = computed(() => this.righe().length <= RIGHE_ALTEZZA_AUTOMATICA);

  private readonly numero = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 2 });

  protected readonly opzioni: GridOptions<Riga> = opzioniGriglia<Riga>({
    autoSizeStrategy: { type: 'fitCellContents' },
    defaultColDef: { maxWidth: 480, tooltip: true },
  });

  protected readonly definizioni = computed<ColDef<Riga>[]>(() =>
    this.colonne().map((c, i) => {
      const numerica = NUMERICI.includes(c.tipo.toLowerCase());
      return {
        headerName: c.nome,
        colId: `c${i}`,
        valueGetter: p => p.data?.[i],
        valueFormatter: p => this.formatta(p.value, numerica),
        filter: numerica ? 'agNumberColumnFilter' : 'agTextColumnFilter',
        type: numerica ? 'rightAligned' : undefined,
        cellStyle: numerica ? { fontVariantNumeric: 'tabular-nums' } : undefined,
      };
    }));

  private formatta(valore: unknown, numerica: boolean): string {
    if (valore === null || valore === undefined) return '';
    if (typeof valore === 'number' && numerica) return this.numero.format(valore);
    if (typeof valore === 'boolean') return valore ? 'Sì' : 'No';
    return String(valore);
  }
}
