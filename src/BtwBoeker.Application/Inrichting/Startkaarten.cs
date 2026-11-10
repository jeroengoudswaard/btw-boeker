using System.Globalization;
using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Bijlagen;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.Application.Inrichting;

/// <summary>De kaarten op de startpagina, in deze volgorde.</summary>
public enum Startkaartsoort
{
    Inrichting,
    Bank,
    Concepten,
    Postvak,
    ZonderBon,
}

/// <param name="Administratie">De administratie waar de kaart over gaat, of <c>null</c> (inrichting).</param>
public sealed record Startkaart(Startkaartsoort Soort, AdministratieSoort? Administratie, int Aantal, string Titel, string Tekst, string Knoptekst);

/// <summary>
/// Bepaalt de kaarten op de startpagina (besluit 183): elk met de volgende stap en één knop, alleen als er iets te doen is.
/// </summary>
public static class Startkaarten
{

    public static IReadOnlyList<Startkaart> Bepaal(
        Inrichtingsstatus status, Startwizardvoortgang voortgang, BoekhoudingStand stand,
        DateOnly vandaag, IReadOnlyDictionary<AdministratieSoort, Bedrag>? drempels = null)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(voortgang);
        ArgumentNullException.ThrowIfNull(stand);
        var kaarten = new List<Startkaart>();

        if (!voortgang.IsAfgerond && status.EersteOpenStap() is not null)
        {
            var stappen = Enum.GetValues<Startstap>().Length;
            var klaar = Enum.GetValues<Startstap>().Count(status.IsKlaar);
            kaarten.Add(new(Startkaartsoort.Inrichting, null, stappen - klaar, "Inrichting afmaken",
                Tekst($"{klaar} van {stappen} stappen klaar"), "Startwizard openen"));
        }

        {
            VoegToe(kaarten, stand, Startkaartsoort.Bank, stand.Bankmutaties.Count(m => Bankoverzicht.Status(stand, m) == MutatieStatus.TeVerwerken),
                n => Enkelvoud(n, "banktransactie", "banktransacties") + " te verwerken", "Kies per transactie een rekening of maak een boekingsregel.", "Naar de bank");
            VoegToe(kaarten, stand, Startkaartsoort.Concepten, stand.Posten.Count(p => !p.IsGeboekt),
                n => Enkelvoud(n, "conceptpost", "conceptposten") + " te boeken", "Controleer de concepten en boek ze (Ctrl+B).", "Naar het journaal");
            VoegToe(kaarten, stand, Startkaartsoort.Postvak, stand.Bijlagen.Count(b => b.IsInPostvak),
                n => Enkelvoud(n, "bon", "bonnen") + " in het postvak", "Koppel de bonnen aan een post.", "Naar het postvak");
            var drempel = drempels?.GetValueOrDefault(stand.Administratie) ?? ZonderBon.StandaardDrempel; // besluit 214
            VoegToe(kaarten, stand, Startkaartsoort.ZonderBon, ZonderBon.Posten(stand, drempel).Count,
                n => Enkelvoud(n, "boeking", "boekingen") + " zonder bon", $"Uitgaven of ontvangsten boven {drempel} zonder bijlage.", "Bekijken");
        }

        return kaarten;
    }

    /// <summary>Haalt de gegevens op en bepaalt de kaarten; zonder database geen kaarten.</summary>
    public static async Task<IReadOnlyList<Startkaart>> BepaalAsync(IDatabaseSessie? sessie, DateOnly vandaag, CancellationToken annulering = default) =>
        sessie is null ? [] : (await LaadAsync(sessie, vandaag, annulering).ConfigureAwait(false)).Kaarten;

    /// <summary>
    /// Besluit 234: alles voor Start in één keer laden (de boekhouding één keer, niet ook nog eens voor de inrichtingsstatus
    /// en de kerncijfers). De kerncijfers rekenen met <see cref="Startgegevens.Boekhouding"/>.
    /// </summary>
    public static async Task<Startgegevens> LaadAsync(IDatabaseSessie sessie, DateOnly vandaag, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sessie);
        var administraties = await sessie.Administraties.HaalAlleAsync(annulering).ConfigureAwait(false);
        var voortgang = await sessie.Startwizard.LaadAsync(annulering).ConfigureAwait(false);
        var stand = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging, annulering).ConfigureAwait(false);
        var status = Inrichtingsstatus.Bepaal(stand);
        var drempels = administraties.ToDictionary(a => a.Soort, a => new Bedrag(a.ZonderBonDrempel));
        return new Startgegevens(Bepaal(status, voortgang, stand, vandaag, drempels), stand);
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

/// <summary>De kaarten van Start plus de geladen boekhouding, zodat de kerncijfers niet opnieuw hoeven te laden.</summary>
public sealed record Startgegevens(IReadOnlyList<Startkaart> Kaarten, BoekhoudingStand Boekhouding);
