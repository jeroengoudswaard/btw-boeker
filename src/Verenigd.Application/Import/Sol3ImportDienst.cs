using System.Globalization;
using Verenigd.Application.Database;

namespace Verenigd.Application.Import;

/// <summary>Resultaat van het herkennen van één gekozen bestand (stap 1 van de wizard).</summary>
public sealed record BestandHerkenning(string Pad, Sol3Export? Export, int AantalRegels, string? Fout);

/// <summary>Ingelezen exports plus het voorbeeldoverzicht.</summary>
public sealed record ImportAnalyse(Sol3Import Import, ImportVoorstel Voorstel, DateOnly Importdatum);

public sealed record ImportResultaat(int Nieuw, int Gewijzigd, int Uitgeschreven, string Backup);

/// <summary>
/// De SOL3-import: bestanden herkennen, analyseren (zonder wijzigingen) en uitvoeren. Vóór het uitvoeren wordt
/// altijd een back-up gemaakt; mislukt die, dan wordt er niets geïmporteerd.
/// </summary>
public sealed class Sol3ImportDienst(IDatabaseSessie sessie, TimeProvider tijd, Sol3ImportOpties? opties = null)
{
    private readonly Sol3ImportOpties _opties = opties ?? new Sol3ImportOpties();

    public static BestandHerkenning HerkenBestand(string pad)
    {
        try
        {
            var tabel = LeesTabel(pad);
            var export = ExportHerkenning.Herken(tabel.Kolommen);
            if (export is null)
            {
                return new BestandHerkenning(pad, null, tabel.Regels.Count,
                    "De kopregel komt niet overeen met een van de vier SOL3-exports.");
            }

            var ontbrekend = ExportHerkenning.OntbrekendeKolommen(export.Value, tabel.Kolommen);
            return new BestandHerkenning(pad, export, tabel.Regels.Count,
                ontbrekend.Count == 0 ? null : "Ontbrekende kolommen: " + string.Join(", ", ontbrekend) + ".");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Sol3ImportException)
        {
            return new BestandHerkenning(pad, null, 0, ex is Sol3ImportException ? ex.Message : "Het bestand kan niet worden gelezen.");
        }
    }

    /// <summary>Leest de vier exports en maakt het voorbeeldoverzicht. Wijzigt niets.</summary>
    /// <exception cref="Sol3ImportException">Een export ontbreekt (alle vier zijn verplicht) of is ongeldig.</exception>
    public async Task<ImportAnalyse> AnalyseerAsync(IReadOnlyDictionary<Sol3Export, string> paden, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(paden);
        var tabellen = new Dictionary<Sol3Export, CsvTabel>();
        foreach (var export in Enum.GetValues<Sol3Export>())
        {
            if (!paden.TryGetValue(export, out var pad))
            {
                throw new Sol3ImportException($"{Hoofdletter(ExportHerkenning.Omschrijving(export))} ontbreekt; alle vier de exports zijn verplicht.");
            }

            var tabel = LeesTabel(pad);
            if (ExportHerkenning.Herken(tabel.Kolommen) != export
                || ExportHerkenning.OntbrekendeKolommen(export, tabel.Kolommen).Count > 0)
            {
                throw new Sol3ImportException($"'{Path.GetFileName(pad)}' is geen geldige {ExportHerkenning.Omschrijving(export)}.");
            }

            tabellen[export] = tabel;
        }

        var import = Sol3Inlezer.Lees(tabellen, _opties);
        var importdatum = DateOnly.FromDateTime(tijd.GetLocalNow().DateTime);
        var stand = await sessie.LedenImport.LaadAsync(annulering).ConfigureAwait(false);
        return new ImportAnalyse(import, ImportPlanner.Plan(stand, import, importdatum), importdatum);
    }

    public async Task<ImportResultaat> VoerUitAsync(ImportAnalyse analyse, ImportKeuzes keuzes, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(analyse);
        ArgumentNullException.ThrowIfNull(keuzes);
        var backup = BackupPad(sessie.Pad, tijd.GetLocalNow());
        await sessie.MaakBackupAsync(backup, annulering).ConfigureAwait(false);

        ImportWijzigingen? wijzigingen = null;
        await sessie.LedenImport.PasToeAsync(
            stand => wijzigingen = ImportToepasser.PasToe(stand, analyse.Import, keuzes, analyse.Importdatum, tijd.GetUtcNow().UtcDateTime),
            annulering).ConfigureAwait(false);

        return new ImportResultaat(wijzigingen!.NieuwePersonen.Count, wijzigingen.Gewijzigd, wijzigingen.Uitgeschreven, backup);
    }

    /// <summary>Naast de database, bijvoorbeeld <c>leden-voor-import-20261027-101500.vbak</c>.</summary>
    public static string BackupPad(string databasePad, DateTimeOffset tijdstip) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(databasePad))!,
            $"{Path.GetFileNameWithoutExtension(databasePad)}-voor-import-{tijdstip.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.vbak");

    private static CsvTabel LeesTabel(string pad)
    {
        using var stroom = File.OpenRead(pad);
        return Sol3CsvLezer.Lees(stroom);
    }

    private static string Hoofdletter(string tekst) => char.ToUpperInvariant(tekst[0]) + tekst[1..];
}
