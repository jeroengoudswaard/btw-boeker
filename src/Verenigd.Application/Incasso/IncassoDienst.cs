using System.Globalization;
using System.Text;
using Verenigd.Application.Database;
using Verenigd.Domain.Financien;

namespace Verenigd.Application.Incasso;

/// <summary>
/// Incassoruns: berekenen (zonder iets op te slaan), definitief maken (na een back-up, in één transactie),
/// het pain.008-bestand maken en een run annuleren.
/// </summary>
public sealed class IncassoDienst(IDatabaseSessie sessie, TimeProvider tijd)
{
    private DateOnly Vandaag => DateOnly.FromDateTime(tijd.GetLocalNow().DateTime);

    public async Task<IncassoVoorstel> BerekenAsync(Maand maand, CancellationToken annulering = default)
    {
        var stand = await sessie.Contributie.LaadAsync(annulering).ConfigureAwait(false);
        return ContributieRekenaar.Bereken(stand, maand, Vandaag);
    }

    /// <summary>
    /// Maakt eerst een back-up, rekent dan opnieuw op de actuele gegevens en legt de run vast. Wijkt de nieuwe berekening
    /// af van <paramref name="voorstel"/>, dan wordt er niets opgeslagen.
    /// </summary>
    /// <exception cref="InvalidOperationException">Het voorstel is leeg of de gegevens zijn intussen gewijzigd.</exception>
    public async Task<Incassorun> MaakDefinitiefAsync(IncassoVoorstel voorstel, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(voorstel);
        if (voorstel.IsLeeg)
        {
            throw new InvalidOperationException("Er is niets te incasseren.");
        }

        await sessie.MaakBackupAsync(BackupPad(sessie.Pad, voorstel.Maand, tijd.GetLocalNow()), annulering).ConfigureAwait(false);

        Incassorun? run = null;
        await sessie.Contributie.BewaarRunAsync(
            stand =>
            {
                var actueel = ContributieRekenaar.Bereken(stand, voorstel.Maand, Vandaag);
                if (!ZelfdeInhoud(actueel, voorstel))
                {
                    throw new InvalidOperationException("De gegevens zijn gewijzigd sinds de berekening. Bereken de run opnieuw.");
                }

                run = BouwRun(actueel, tijd.GetUtcNow().UtcDateTime);
                return run;
            },
            annulering).ConfigureAwait(false);
        return run!;
    }

    /// <summary>Maakt het pain.008-bestand, controleert het tegen het schema en legt de hash vast.</summary>
    /// <exception cref="InvalidOperationException">Het bestand is ongeldig of wijkt af van het eerder gemaakte bestand.</exception>
    public async Task<IncassoBestand> MaakBestandAsync(Incassorun run, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        var stand = await sessie.Contributie.LaadAsync(annulering).ConfigureAwait(false);
        var instellingen = stand.Instellingen ?? throw new InvalidOperationException("De incassogegevens ontbreken.");
        var inhoud = Pain008Schrijver.Schrijf(run, instellingen);
        var fouten = Pain008Schrijver.Valideer(inhoud);
        if (fouten.Count > 0)
        {
            throw new InvalidOperationException("Het incassobestand voldoet niet aan het SEPA-schema:\n" + string.Join("\n", fouten.Take(5)));
        }

        var hash = Pain008Schrijver.Hash(inhoud);
        if (run.BestandHash is not null && run.BestandHash != hash)
        {
            throw new InvalidOperationException(
                "Het bestand wijkt af van het eerder gemaakte bestand van deze run (zijn de incassogegevens gewijzigd?).");
        }

        if (run.BestandHash is null)
        {
            await sessie.Contributie.LegBestandVastAsync(run.Id, hash, annulering).ConfigureAwait(false);
            run.LegBestandVast(hash);
        }

        return new IncassoBestand($"incasso-{run.Maand.Code}.xml", inhoud);
    }

    /// <summary>Besluit 60: alleen voor een run die de bank niet heeft uitgevoerd.</summary>
    public async Task AnnuleerAsync(Incassorun run, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        var nu = tijd.GetUtcNow().UtcDateTime;
        await sessie.Contributie.AnnuleerRunAsync(run.Id, nu, annulering).ConfigureAwait(false);
        if (run.Status == IncassorunStatus.Definitief)
        {
            run.Annuleer(nu);
        }
    }

    /// <summary>Voegt ontbrekende standaardcategorieën toe: <c>Jeugdlid &lt;speltak&gt;</c>, <c>Leiding</c> en <c>Vrijgesteld</c>.</summary>
    public async Task ZorgVoorStandaardcategorieenAsync(IEnumerable<string> speltakken, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(speltakken);
        var bestaand = (await sessie.Contributie.LaadAsync(annulering).ConfigureAwait(false)).Categorieen;
        var nieuw = new List<Contributiecategorie>();
        foreach (var speltak in speltakken.Distinct(StringComparer.Ordinal))
        {
            if (!bestaand.Any(c => c.Soort == CategorieSoort.Jeugdlid && c.Speltak == speltak))
            {
                nieuw.Add(new Contributiecategorie($"Jeugdlid {speltak}", CategorieSoort.Jeugdlid, speltak));
            }
        }

        foreach (var (soort, naam) in new[] { (CategorieSoort.Leiding, "Leiding"), (CategorieSoort.Vrijgesteld, "Vrijgesteld") })
        {
            if (!bestaand.Any(c => c.Soort == soort))
            {
                nieuw.Add(new Contributiecategorie(naam, soort, null));
            }
        }

        if (nieuw.Count > 0)
        {
            await sessie.Contributie.VoegCategorieenToeAsync(nieuw, annulering).ConfigureAwait(false);
        }
    }

    /// <summary>De lijst "zelf te innen" als CSV (UTF-8 met BOM, <c>;</c>-gescheiden), zonder IBAN's.</summary>
    public static string ZelfTeInnenCsv(IncassoVoorstel voorstel)
    {
        ArgumentNullException.ThrowIfNull(voorstel);
        var tekst = new StringBuilder("﻿");
        tekst.Append(Regel("Lidnummer", "Naam", "Maanden", "Bedrag", "Reden")).Append("\r\n");
        foreach (var regel in voorstel.ZelfTeInnen)
        {
            tekst.Append(Regel(regel.Lidnummer.Waarde, regel.Naam, regel.Maanden, regel.Bedrag?.ToString() ?? string.Empty, regel.Reden)).Append("\r\n");
        }

        return tekst.ToString();

        static string Regel(params string[] velden) =>
            string.Join(';', velden.Select(v => "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
    }

    /// <summary>Naast de database, bijvoorbeeld <c>leden-voor-incasso-2027-03-20270220-100000.vbak</c>.</summary>
    public static string BackupPad(string databasePad, Maand maand, DateTimeOffset tijdstip) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(databasePad))!,
            $"{Path.GetFileNameWithoutExtension(databasePad)}-voor-incasso-{maand.Code}-{tijdstip.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.vbak");

    private static Incassorun BouwRun(IncassoVoorstel voorstel, DateTime nuUtc)
    {
        var run = new Incassorun(voorstel.Maand, voorstel.Uitvoerdatum, nuUtc);
        foreach (var post in voorstel.Posten)
        {
            var incassopost = run.VoegPostToe(
                post.Betaler.Id, post.Betaler.Iban, post.Rekeninghouder, post.Betaler.Mandaatkenmerk!,
                post.Betaler.MachtigingOndertekendOp!.Value, post.Omschrijving);
            foreach (var regel in post.Regels)
            {
                incassopost.VoegRegelToe(regel.Lidnummer, regel.Maand, regel.Categorie, regel.Bedrag);
            }
        }

        return run;
    }

    private static bool ZelfdeInhoud(IncassoVoorstel a, IncassoVoorstel b) =>
        a.Uitvoerdatum == b.Uitvoerdatum
        && a.Posten.Count == b.Posten.Count
        && a.Posten.Zip(b.Posten).All(p =>
            p.First.Betaler.Id == p.Second.Betaler.Id
            && p.First.Omschrijving == p.Second.Omschrijving
            && p.First.Regels.SequenceEqual(p.Second.Regels));
}
