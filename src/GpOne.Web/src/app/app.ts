import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatIconRegistry } from '@angular/material/icon';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />'
})
export class App {
  constructor() {
    // Icone "Material Symbols Rounded" (caricate in index.html) per tutti i <mat-icon>
    inject(MatIconRegistry).setDefaultFontSetClass('material-symbols-rounded');
  }
}
