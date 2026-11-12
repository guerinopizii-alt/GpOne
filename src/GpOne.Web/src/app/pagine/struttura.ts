import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver } from '@angular/cdk/layout';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ConversazioneService } from '../core/conversazione.service';
import { VoceStorico } from '../core/modelli';
import { SessioneService } from '../core/sessione.service';

/** Layout dell'area autenticata: menu laterale con conversazione, domande recenti e amministrazione. */
@Component({
  selector: 'app-struttura',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatIconModule, MatMenuModule, MatSidenavModule, MatToolbarModule, MatTooltipModule],
  template: `
    <mat-sidenav-container class="contenitore">
      <mat-sidenav #menuLaterale [mode]="stretto() ? 'over' : 'side'" [opened]="!stretto()">
        <nav class="menu">
          <a class="logo" routerLink="/chiedi" (click)="chiudiSeStretto(menuLaterale)">
            <img src="img/logo.svg" alt="" width="36" height="36" />
            <span>GpOne</span>
          </a>

          <button class="nuova" type="button" [disabled]="conversazione.occupato()" (click)="nuova(menuLaterale)">
            <mat-icon>add</mat-icon>Nuova conversazione
          </button>

          <a class="voce" routerLink="/chiedi" routerLinkActive="attiva" (click)="chiudiSeStretto(menuLaterale)">
            <mat-icon class="icona-piena">forum</mat-icon>Chiedi
          </a>

          <div class="sezione"><mat-icon>history</mat-icon>Recenti</div>
          <div class="recenti">
            @for (v of conversazione.storico(); track v.id) {
              <button type="button" class="recente" [matTooltip]="v.testo" matTooltipPosition="right" matTooltipShowDelay="600"
                      [disabled]="conversazione.occupato()" (click)="apri(v, menuLaterale)">
                <mat-icon class="icona-piena esito {{ esito(v).classe }}">{{ esito(v).icona }}</mat-icon>
                <span class="testo">{{ v.testo }}</span>
              </button>
            } @empty {
              <p class="vuoto">Le tue domande compariranno qui.</p>
            }
          </div>

          @if (sessione.amministratore()) {
            <div class="sezione"><mat-icon>admin_panel_settings</mat-icon>Amministrazione</div>
            <a class="voce" routerLink="/admin/clienti" routerLinkActive="attiva" (click)="chiudiSeStretto(menuLaterale)">
              <mat-icon class="icona-piena">domain</mat-icon>Clienti
            </a>
            <a class="voce" routerLink="/admin/utenti" routerLinkActive="attiva" (click)="chiudiSeStretto(menuLaterale)">
              <mat-icon class="icona-piena">group</mat-icon>Utenti
            </a>
          }

          <a class="cliente" routerLink="/scegli-cliente" matTooltip="Cambia cliente" (click)="chiudiSeStretto(menuLaterale)">
            <span class="iniziale">{{ inizialeCliente() }}</span>
            <span class="dati">
              <span class="etichetta">Cliente</span>
              <span class="nome">{{ sessione.cliente()?.ragioneSociale ?? 'Scegli cliente' }}</span>
            </span>
            <mat-icon>unfold_more</mat-icon>
          </a>

          <button type="button" class="utente" [matMenuTriggerFor]="menuUtente">
            <span class="avatar">{{ inizialiUtente() }}</span>
            <span class="nome">{{ sessione.utente()?.nomeCompleto || sessione.utente()?.username }}</span>
            <mat-icon>more_horiz</mat-icon>
          </button>
          <mat-menu #menuUtente="matMenu" yPosition="above">
            <a mat-menu-item routerLink="/cambia-password" (click)="chiudiSeStretto(menuLaterale)">
              <mat-icon>key</mat-icon>Cambia password
            </a>
            <button mat-menu-item (click)="esci()"><mat-icon>logout</mat-icon>Esci</button>
          </mat-menu>
        </nav>
      </mat-sidenav>

      <mat-sidenav-content class="contenuto-app">
        @if (stretto()) {
          <mat-toolbar>
            <button mat-icon-button aria-label="Apri il menu" (click)="menuLaterale.toggle()"><mat-icon>menu</mat-icon></button>
            <img src="img/logo.svg" alt="" width="28" height="28" />
            <span class="testo-gradiente marchio">GpOne</span>
          </mat-toolbar>
        }
        <router-outlet />
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    .contenitore { height: 100%; background: transparent; }
    mat-sidenav {
      width: 272px; border: none; border-radius: 0;
      background:
        radial-gradient(400px 300px at 0% 0%, rgb(124 77 255 / .55), transparent 70%),
        radial-gradient(400px 300px at 100% 100%, rgb(255 79 139 / .35), transparent 70%),
        #1b1238;
      color: #ece7ff;
    }
    .menu { display: flex; flex-direction: column; height: 100%; padding: 18px 14px 14px; box-sizing: border-box; gap: 4px; }
    .logo { display: flex; align-items: center; gap: 10px; padding: 2px 6px 16px; color: #fff; text-decoration: none; font-size: 22px; font-weight: 800; letter-spacing: -.02em; }
    .logo img { border-radius: 11px; box-shadow: 0 6px 18px rgb(255 79 139 / .4); }

    .nuova {
      display: flex; align-items: center; justify-content: center; gap: 8px; height: 46px; margin-bottom: 10px;
      border: none; border-radius: 14px; cursor: pointer; font: inherit; font-weight: 700; color: #fff;
      background: var(--gp-gradiente); box-shadow: 0 8px 22px rgb(255 79 139 / .35); transition: transform .15s, filter .15s;
    }
    .nuova:hover:not(:disabled) { transform: translateY(-1px); filter: brightness(1.06); }
    .nuova:disabled { opacity: .5; cursor: default; }

    .voce {
      display: flex; align-items: center; gap: 12px; padding: 10px 12px; border-radius: 12px;
      color: #d9d1ff; text-decoration: none; font-weight: 600; transition: background .15s;
    }
    .voce:hover { background: rgb(255 255 255 / .07); }
    .voce.attiva { background: rgb(255 255 255 / .14); color: #fff; }
    .voce mat-icon { color: #b69cff; }
    .voce.attiva mat-icon { color: #ff86b0; }

    .sezione {
      display: flex; align-items: center; gap: 6px; margin: 14px 8px 4px;
      font-size: 11px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; color: #9d90cc;
    }
    .sezione mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .recenti { flex: 1; overflow-y: auto; min-height: 60px; margin: 0 -4px; padding: 0 4px; }
    .recenti::-webkit-scrollbar { width: 6px; }
    .recenti::-webkit-scrollbar-thumb { background: rgb(255 255 255 / .15); border-radius: 3px; }
    .recente {
      display: flex; align-items: center; gap: 10px; width: 100%; padding: 8px 10px; border: none; border-radius: 10px;
      background: transparent; color: #cfc6f5; font: inherit; font-size: 13px; text-align: left; cursor: pointer;
    }
    .recente:hover:not(:disabled) { background: rgb(255 255 255 / .07); color: #fff; }
    .recente .testo { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .esito { font-size: 18px; width: 18px; height: 18px; flex: none; }
    .esito.ok { color: #5eead4; }
    .esito.confermata { color: #ffc061; }
    .esito.ko { color: #ff86b0; }
    .vuoto { font-size: 13px; color: #9d90cc; padding: 4px 10px; margin: 0; }

    .cliente {
      display: flex; align-items: center; gap: 10px; padding: 10px; margin-top: 12px; border-radius: 14px;
      background: rgb(255 255 255 / .08); border: 1px solid rgb(255 255 255 / .1); color: #fff; text-decoration: none;
    }
    .cliente:hover { background: rgb(255 255 255 / .13); }
    .iniziale {
      display: grid; place-items: center; width: 36px; height: 36px; border-radius: 10px; flex: none;
      background: linear-gradient(135deg, var(--gp-arancio), var(--gp-rosa)); font-weight: 800;
    }
    .dati { display: flex; flex-direction: column; min-width: 0; flex: 1; }
    .dati .etichetta { font-size: 11px; color: #b5a9e6; }
    .nome { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
    .cliente mat-icon { color: #b5a9e6; }

    .utente {
      display: flex; align-items: center; gap: 10px; padding: 8px 10px; margin-top: 6px; border: none; border-radius: 12px;
      background: transparent; color: #ece7ff; font: inherit; cursor: pointer; text-align: left;
    }
    .utente:hover { background: rgb(255 255 255 / .07); }
    .utente .nome { flex: 1; }
    .avatar {
      display: grid; place-items: center; width: 32px; height: 32px; border-radius: 50%; flex: none;
      background: var(--gp-gradiente-freddo); color: #fff; font-size: 12px; font-weight: 700;
    }

    mat-toolbar { gap: 8px; background: transparent; border-bottom: 1px solid var(--mat-sys-outline-variant); backdrop-filter: blur(8px); }
    .marchio { font-weight: 800; font-size: 20px; }
  `
})
export class Struttura {
  protected readonly sessione = inject(SessioneService);
  protected readonly conversazione = inject(ConversazioneService);
  private readonly router = inject(Router);

  protected readonly stretto = toSignal(
    inject(BreakpointObserver).observe('(max-width: 959.98px)').pipe(map(s => s.matches)),
    { initialValue: false });

  protected readonly inizialeCliente = computed(() => (this.sessione.cliente()?.ragioneSociale ?? '?').trim()[0].toUpperCase());
  protected readonly inizialiUtente = computed(() => {
    const u = this.sessione.utente();
    const parti = (u?.nomeCompleto || u?.username || '?').trim().split(/\s+/);
    return (parti[0][0] + (parti[1]?.[0] ?? '')).toUpperCase();
  });

  esito(v: VoceStorico): { icona: string; classe: string } {
    if (v.valutazione === 'Corretta') return { icona: 'star', classe: 'confermata' };
    if (v.esito !== 'Ok' || v.valutazione === 'Sbagliata') return { icona: 'error', classe: 'ko' };
    return { icona: 'chat_bubble', classe: 'ok' };
  }

  nuova(menu: MatSidenav) {
    this.conversazione.nuova();
    this.router.navigate(['/chiedi']);
    this.chiudiSeStretto(menu);
  }

  apri(v: VoceStorico, menu: MatSidenav) {
    this.router.navigate(['/chiedi']);
    this.conversazione.apri(v);
    this.chiudiSeStretto(menu);
  }

  chiudiSeStretto(menu: MatSidenav) {
    if (this.stretto()) menu.close();
  }

  esci() {
    this.sessione.esci();
    this.router.navigate(['/login']);
  }
}
