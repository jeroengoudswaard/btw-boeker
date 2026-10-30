using System.Globalization;
using Verenigd.Application.Bank;
using Verenigd.Application.Bijlagen;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Debiteuren;
using Verenigd.Application.Facturen;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;

namespace Verenigd.Application.Inrichting;

/// <summary>De acht soorten kaarten op de startpagina (besluit 183), in deze volgorde.</summary>
public enum Startkaartsoort
{
    Inrichting,
    Bank,
    Concepten,
    Postvak,
    Incassorun,
    Storneringen,
    VervallenFacturen,
    ZonderBon,
}

/// <param name="Administratie">De administratie waar de kaart over gaat, of <c>null</c> (inrichting).</param>
public sealed record Startkaart(Startkaartsoort Soort, AdministratieSoort? Administratie, int Aantal, string Titel, string Tekst, string Knoptekst);

/// <summary>
/// Bepaalt de kaarten op de startpagina (besluit 183): elk met de volgende stap en één knop, alleen als er iets te doen is.
/// </summary>
public static class Startkaarten
{
    private const int AantalInrichtstappen = 6;

    public static IReadOnlyList<Startkaart> Bepaal(
        Inrichtingsstatus status, Startwizardvoortgang voortgang, BoekhoudingStand vereniging, BoekhoudingStand stichting, IncassoStand contributie,
        DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(voortgang);
        ArgumentNullException.ThrowIfNull(vereniging);
        ArgumentNullException.ThrowIfNull(stichting);
        ArgumentNullException.ThrowIfNull(contributie);
        var kaarten = new List<Startkaart>();

        if (!voortgang.IsAfgerond && status.EersteOpenStap() is not null)
        {
            var klaar = Enum.GetValues<Startstap>().Count(status.IsKlaar);
            kaarten.Add(new(Startkaartsoort.Inrichting, null, AantalInrichtstappen - klaar, "Inrichting afmaken",
                Tekst($"{klaar} van {AantalInrichtstappen} stappen klaar"), "Startwizard openen"));
        }

        foreach (var stand in new[] { vereniging, stichting })
        {
            VoegToe(kaarten, stand, Startkaartsoort.Bank, stand.Bankmutaties.Count(m => Bankoverzicht.Status(stand, m) == MutatieStatus.TeVerwerken),
                n => Enkelvoud(n, "banktransactie", "banktransacties") + " te verwerken", "Kies per transactie een rekening of maak een boekingsregel.", "Naar de bank");
            VoegToe(kaarten, stand, Startkaartsoort.Concepten, stand.Posten.Count(p => !p.IsGeboekt),
                n => Enkelvoud(n, "conceptpost", "conceptposten") + " te boeken", "Controleer de concepten en boek ze (Ctrl+B).", "Naar het journaal");
            VoegToe(kaarten, stand, Startkaartsoort.Postvak, stand.Bijlagen.Count(b => b.IsInPostvak),
                n => Enkelvoud(n, "bon", "bonnen") + " in het postvak", "Koppel de bonnen aan een post.", "Naar het postvak");
            if (stand.Administratie == AdministratieSoort.Vereniging)
            {
                VoegIncassorunToe(kaarten, contributie, vandaag);
                VoegToe(kaarten, stand, Startkaartsoort.Storneringen, Contributievorderingen.Bereken(stand).Count(v => v.IsOpen),
                    n => Enkelvoud(n, "stornering", "storneringen") + " openstaand", "Incasseer opnieuw in de volgende run of boek af.", "Naar openstaand");
            }

            VoegToe(kaarten, stand, Startkaartsoort.VervallenFacturen, Factuuroverzicht.Bereken(stand, vandaag).Count(f => f.IsVervallen),
                n => Enkelvoud(n, "vervallen factuur", "vervallen facturen"), "De betaaltermijn is verstreken.", "Naar de facturen");
            VoegToe(kaarten, stand, Startkaartsoort.ZonderBon, ZonderBon.Posten(stand, ZonderBon.StandaardDrempel).Count,
                n => Enkelvoud(n, "boeking", "boekingen") + " zonder bon", "Uitgaven of ontvangsten boven € 10 zonder bijlage.", "Bekijken");
        }

        return kaarten;
    }

    /// <summary>Haalt de gegevens op en bepaalt de kaarten; zonder database geen kaarten.</summary>
    public static async Task<IReadOnlyList<Startkaart>> BepaalAsync(IDatabaseSessie? sessie, DateOnly vandaag, CancellationToken annulering = default)
    {
        if (sessie is null)
        {
            return [];
        }

        var status = await Inrichtingsstatus.BepaalAsync(sessie, vandaag.Year, annulering).ConfigureAwait(false);
        var voortgang = await sessie.Startwizard.LaadAsync(annulering).ConfigureAwait(false);
        var vereniging = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging, annulering).ConfigureAwait(false);
        var stichting = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Stichting, annulering).ConfigureAwait(false);
        var contributie = await sessie.Contributie.LaadAsync(annulering).ConfigureAwait(false);
        return Bepaal(status, voortgang, vereniging, stichting, contributie, vandaag);
    }

    private static void VoegIncassorunToe(List<Startkaart> kaarten, IncassoStand contributie, DateOnly vandaag)
    {
        var maand = Maand.Van(vandaag);
        if (contributie.Instellingen is { } instellingen && maand >= instellingen.EersteIncassomaand
            && !contributie.Runs.Any(r => r.Maand == maand && r.Status != IncassorunStatus.Geannuleerd))
        {
            kaarten.Add(new(Startkaartsoort.Incassorun, AdministratieSoort.Vereniging, 1, $"Incassorun {maand} nog niet gemaakt",
                "Maak de run en upload het bestand bij de bank.", "Nieuwe incassorun"));
        }
    }

    private static void VoegToe(List<Startkaart> kaarten, BoekhoudingStand stand, Startkaartsoort soort, int aantal, Func<int, string> titel, string tekst, string knop)
    {
        if (aantal > 0)
        {
            kaarten.Add(new(soort, stand.Administratie, aantal, titel(aantal), tekst, knop));
        }
    }

    private static string Enkelvoud(int aantal, string een, string meer) =>
        string.Create(CultureInfo.InvariantCulture, $"{aantal} {(aantal == 1 ? een : meer)}");

    private static string Tekst(FormattableString tekst) => tekst.ToString(CultureInfo.InvariantCulture);
}
