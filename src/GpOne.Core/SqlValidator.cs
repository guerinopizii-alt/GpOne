using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace GpOne.Core;

/// <summary>Oggetto (tabella/vista) consultabile con le sue colonne abilitate.</summary>
public record OggettoConsentito(string Schema, string Nome, IReadOnlySet<string> Colonne);

public record EsitoValidazione(bool Valida, string? Errore)
{
    public static readonly EsitoValidazione Ok = new(true, null);
    public static EsitoValidazione Rifiuta(string errore) => new(false, errore);
}

/// <summary>
/// Verifica che il testo sia una sola SELECT di sola lettura.
/// Con <c>consentiti</c> verifica anche che usi solo tabelle/viste e colonne abilitate (lato API);
/// senza, fa solo i controlli strutturali (lato agente, seconda linea di difesa).
/// </summary>
public static class SqlValidator
{
    public static EsitoValidazione Valida(string sql, IReadOnlyCollection<OggettoConsentito>? consentiti = null)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return EsitoValidazione.Rifiuta("Query vuota.");

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        var albero = parser.Parse(new StringReader(sql), out var errori);
        if (errori.Count > 0)
            return EsitoValidazione.Rifiuta($"Sintassi non valida: {errori[0].Message} (riga {errori[0].Line}).");

        if (albero is not TSqlScript { Batches: [{ Statements: [SelectStatement select] }] })
            return EsitoValidazione.Rifiuta("È consentita una sola istruzione SELECT.");

        if (select.Into is not null)
            return EsitoValidazione.Rifiuta("SELECT ... INTO non è consentito.");

        var visitatore = new Visitatore();
        select.Accept(visitatore);
        if (visitatore.Errore is not null)
            return EsitoValidazione.Rifiuta(visitatore.Errore);

        return consentiti is null ? EsitoValidazione.Ok : VerificaOggetti(visitatore, consentiti);
    }

    private static EsitoValidazione VerificaOggetti(Visitatore v, IReadOnlyCollection<OggettoConsentito> consentiti)
    {
        var colonneUsabili = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (schema, nome) in v.Tabelle)
        {
            if (schema is null && v.NomiCte.Contains(nome))
                continue;
            var oggetto = consentiti.FirstOrDefault(o =>
                string.Equals(o.Nome, nome, StringComparison.OrdinalIgnoreCase)
                && (schema is null || string.Equals(o.Schema, schema, StringComparison.OrdinalIgnoreCase)));
            if (oggetto is null)
                return EsitoValidazione.Rifiuta($"La tabella o vista '{(schema is null ? nome : $"{schema}.{nome}")}' non è consultabile.");
            colonneUsabili.UnionWith(oggetto.Colonne);
        }

        foreach (var colonna in v.Colonne)
            if (!colonneUsabili.Contains(colonna) && !v.Alias.Contains(colonna))
                return EsitoValidazione.Rifiuta($"La colonna '{colonna}' non è consultabile.");

        return EsitoValidazione.Ok;
    }

    private sealed class Visitatore : TSqlFragmentVisitor
    {
        public string? Errore { get; private set; }
        public List<(string? Schema, string Nome)> Tabelle { get; } = [];
        public HashSet<string> Colonne { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Alias { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> NomiCte { get; } = new(StringComparer.OrdinalIgnoreCase);

        private void Rifiuta(string errore) => Errore ??= errore;

        public override void Visit(NamedTableReference node)
        {
            var nome = node.SchemaObject;
            if (nome.ServerIdentifier is not null || nome.DatabaseIdentifier is not null)
                Rifiuta("Non sono consentiti riferimenti ad altri database o server.");
            else
                Tabelle.Add((nome.SchemaIdentifier?.Value, nome.BaseIdentifier.Value));
            if (node.Alias is not null)
                Alias.Add(node.Alias.Value);
        }

        public override void Visit(CommonTableExpression node)
        {
            NomiCte.Add(node.ExpressionName.Value);
            foreach (var colonna in node.Columns)
                Alias.Add(colonna.Value);
        }

        public override void Visit(QueryDerivedTable node)
        {
            if (node.Alias is not null)
                Alias.Add(node.Alias.Value);
            foreach (var colonna in node.Columns)
                Alias.Add(colonna.Value);
        }

        public override void Visit(SelectScalarExpression node)
        {
            if (node.ColumnName?.Value is { } alias)
                Alias.Add(alias);
        }

        public override void Visit(SelectStarExpression node) =>
            Rifiuta("SELECT * non è consentito: indicare le colonne.");

        public override void Visit(ColumnReferenceExpression node)
        {
            if (node.ColumnType == ColumnType.Regular && node.MultiPartIdentifier?.Identifiers is { Count: > 0 } parti)
                Colonne.Add(parti[^1].Value);
        }

        public override void Visit(FunctionCall node)
        {
            // Solo funzioni predefinite: niente funzioni utente o di altri schemi
            if (node.CallTarget is not null)
                Rifiuta($"La funzione '{node.FunctionName.Value}' non è consentita.");
        }

        /// <summary>Hint che prendono lock di modifica o li trattengono: bloccherebbero gli altri utenti del database.</summary>
        private static readonly HashSet<TableHintKind> HintNonConsentiti =
        [
            TableHintKind.UpdLock, TableHintKind.XLock, TableHintKind.TabLock, TableHintKind.TabLockX,
            TableHintKind.HoldLock, TableHintKind.Serializable, TableHintKind.RepeatableRead
        ];

        public override void Visit(TableHint node)
        {
            if (HintNonConsentiti.Contains(node.HintKind))
                Rifiuta($"L'hint {node.HintKind.ToString().ToUpperInvariant()} non è consentito: le query devono essere di sola lettura.");
        }

        // NEXT VALUE FOR fa avanzare la sequenza anche se la transazione viene annullata
        public override void Visit(NextValueForExpression node) => Rifiuta("NEXT VALUE FOR non è consentito: modifica una sequenza.");

        public override void Visit(SchemaObjectFunctionTableReference node) => Rifiuta("Le funzioni tabellari non sono consentite.");
        public override void Visit(OpenRowsetTableReference node) => Rifiuta("OPENROWSET non è consentito.");
        public override void Visit(BulkOpenRowset node) => Rifiuta("OPENROWSET non è consentito.");
        public override void Visit(OpenQueryTableReference node) => Rifiuta("OPENQUERY non è consentito.");
        public override void Visit(AdHocTableReference node) => Rifiuta("OPENDATASOURCE non è consentito.");
        public override void Visit(OpenXmlTableReference node) => Rifiuta("OPENXML non è consentito.");
        public override void Visit(FullTextTableReference node) => Rifiuta("Le funzioni full-text tabellari non sono consentite.");
        public override void Visit(VariableTableReference node) => Rifiuta("Le variabili tabella non sono consentite.");
        public override void Visit(GlobalVariableExpression node) => Rifiuta($"La variabile di sistema '{node.Name}' non è consentita.");
    }
}
