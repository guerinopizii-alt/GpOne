import { isDevMode } from '@angular/core';
import { formatDate } from '@angular/common';
import {
  CellStyleModule, CheckboxEditorModule, ClientSideRowModelModule, ColumnAutoSizeModule, GridOptions, LocaleModule,
  ModuleRegistry, NumberFilterModule, QuickFilterModule, TextEditorModule, TextFilterModule, TooltipModule,
  ValidationModule, themeQuartz
} from 'ag-grid-community';
import { AG_GRID_LOCALE_IT } from '@ag-grid-community/locale';

/** Configurazione AG Grid condivisa da tutte le tabelle dell'app. */
ModuleRegistry.registerModules([
  ClientSideRowModelModule, TextFilterModule, NumberFilterModule, QuickFilterModule,
  ColumnAutoSizeModule, CellStyleModule, TooltipModule, LocaleModule,
  TextEditorModule, CheckboxEditorModule,
  ...(isDevMode() ? [ValidationModule] : [])
]);

/** Tema agganciato ai token del tema Material: segue anche il passaggio chiaro/scuro. */
export const temaGriglia = themeQuartz.withParams({
  fontFamily: 'inherit',
  fontSize: 13,
  backgroundColor: 'var(--mat-sys-surface-container-lowest)',
  foregroundColor: 'var(--mat-sys-on-surface)',
  accentColor: 'var(--mat-sys-primary)',
  borderColor: 'var(--mat-sys-outline-variant)',
  headerBackgroundColor: 'color-mix(in srgb, #7c4dff 10%, var(--mat-sys-surface-container-lowest))',
  headerTextColor: 'var(--mat-sys-on-surface)',
  headerFontWeight: 700,
  oddRowBackgroundColor: 'color-mix(in srgb, #7c4dff 3%, var(--mat-sys-surface-container-lowest))',
  rowHoverColor: 'color-mix(in srgb, #ff4f8b 9%, transparent)',
  selectedRowBackgroundColor: 'color-mix(in srgb, #7c4dff 14%, transparent)',
  wrapperBorderRadius: 14,
  spacing: 6,
});

/** Opzioni comuni: testi in italiano, testo selezionabile, ordinamento e filtri su tutte le colonne. */
export function opzioniGriglia<T>(altre: GridOptions<T> = {}): GridOptions<T> {
  return {
    localeText: AG_GRID_LOCALE_IT,
    enableCellTextSelection: true,
    ensureDomOrder: true,
    tooltipShowDelay: 500,
    ...altre,
    defaultColDef: { sortable: true, resizable: true, filter: true, ...altre.defaultColDef },
  };
}

export function formattaDataOra(valore: string | null | undefined): string {
  return valore ? formatDate(valore, 'dd/MM/yyyy HH:mm', 'en-US') : '';
}
