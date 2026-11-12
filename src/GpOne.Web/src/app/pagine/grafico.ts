import { Component, ElementRef, OnDestroy, effect, input, viewChild } from '@angular/core';
import { Chart, ChartType, registerables } from 'chart.js';
import { ColonnaRisultato, Grafico } from '../core/modelli';

Chart.register(...registerables);

const TIPI: Record<string, ChartType> = { barre: 'bar', linee: 'line', torta: 'pie' };
/** Colori vivaci GpOne (viola, rosa, arancio, ciano, verde, …), in esadecimale per poter aggiungere la trasparenza. */
const PALETTE = ['#7c4dff', '#ff4f8b', '#ffa53d', '#22c3ee', '#2dd4a7', '#a77bff', '#ff86b0', '#ffc061'];

/** Grafico dei risultati: tipo e colonne li sceglie l'AI (senza vedere i dati), i valori restano nel browser. */
@Component({
  selector: 'app-grafico',
  template: `<div class="contenitore"><canvas #tela></canvas></div>`,
  styles: `.contenitore { position: relative; height: 420px; }`
})
export class GraficoRisultato implements OnDestroy {
  readonly grafico = input.required<Grafico>();
  readonly colonne = input.required<ColonnaRisultato[]>();
  readonly righe = input.required<unknown[][]>();

  private readonly tela = viewChild.required<ElementRef<HTMLCanvasElement>>('tela');
  private chart?: Chart;

  constructor() {
    effect(() => this.disegna(this.grafico(), this.colonne(), this.righe()));
  }

  private disegna(grafico: Grafico, colonne: ColonnaRisultato[], righe: unknown[][]) {
    this.chart?.destroy();
    const indice = (nome: string) => colonne.findIndex(c => c.nome.toLowerCase() === nome.toLowerCase());
    const ix = indice(grafico.x);
    const serie = grafico.y.map(y => ({ nome: y, i: indice(y) })).filter(s => s.i >= 0);
    if (ix < 0 || serie.length === 0) return;

    const tipo = TIPI[grafico.tipo] ?? 'bar';
    const stile = getComputedStyle(this.tela().nativeElement);
    const testo = stile.getPropertyValue('--mat-sys-on-surface-variant').trim() || '#666';
    const griglia = stile.getPropertyValue('--mat-sys-outline-variant').trim() || '#ddd';

    this.chart = new Chart(this.tela().nativeElement, {
      type: tipo,
      data: {
        labels: righe.map(r => String(r[ix] ?? '')),
        datasets: serie.map((s, n) => {
          const colore = PALETTE[n % PALETTE.length];
          return tipo === 'pie'
            ? { label: colonne[s.i].nome, data: righe.map(r => Number(r[s.i] ?? 0)), backgroundColor: righe.map((_, k) => PALETTE[k % PALETTE.length]), borderWidth: 2 }
            : {
                label: colonne[s.i].nome,
                data: righe.map(r => Number(r[s.i] ?? 0)),
                backgroundColor: tipo === 'line' ? colore + '26' : colore,
                borderColor: colore,
                borderWidth: tipo === 'line' ? 3 : 0,
                borderRadius: 8,
                fill: tipo === 'line',
                tension: .35,
                pointRadius: 3,
                pointHoverRadius: 6,
                pointBackgroundColor: colore,
              };
        })
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        font: { family: "'Plus Jakarta Sans', sans-serif" },
        color: testo,
        plugins: {
          legend: { labels: { color: testo, usePointStyle: true, pointStyle: 'circle', font: { family: "'Plus Jakarta Sans', sans-serif", weight: 600 } } },
          tooltip: { backgroundColor: '#1e1b3a', padding: 12, cornerRadius: 10, titleFont: { weight: 700 } }
        },
        scales: tipo === 'pie' ? {} : {
          x: { grid: { display: false }, ticks: { color: testo } },
          y: { grid: { color: griglia }, border: { display: false }, ticks: { color: testo } }
        }
      }
    });
  }

  ngOnDestroy() {
    this.chart?.destroy();
  }
}
