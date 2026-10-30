using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Inrichting;

/// <summary>De stappen van de startwizard (besluit 170), in deze volgorde.</summary>
public enum Startstap
{
    Welkom,
    Database,
    Administraties,
    Leden,
    Boekhouding,
    Bank,
    Contributie,
    Klaar,
}

/// <param name="Stap">De stap waar de gebruiker met <i>Later verder</i> stopte, of <c>null</c>.</param>
/// <param name="IsAfgerond">De gebruiker heeft de wizard met <i>Voltooien</i> afgesloten.</param>
public sealed record Startwizardvoortgang(Startstap? Stap, bool IsAfgerond)
{
    public static Startwizardvoortgang Nieuw { get; } = new(null, false);

    /// <summary>Bij het openen van de database aanbieden om verder te gaan (besluit 170).</summary>
    public bool KanHervatten => !IsAfgerond && Stap is not null;
}

/// <summary>De voortgang van de startwizard, per database (besluit 173).</summary>
public interface IStartwizardOpslag
{
    Task<Startwizardvoortgang> LaadAsync(CancellationToken annulering = default);

    Task BewaarAsync(Startwizardvoortgang voortgang, CancellationToken annulering = default);
}

/// <summary>
/// Wat er al is ingericht, afgeleid uit de gegevens zelf (besluit 175): zo ziet de gebruiker ook bij een bestaande database
/// welke stappen klaar zijn. Overslaan verandert hier niets aan.
/// </summary>
public sealed record Inrichtingsstatus(
    bool DatabaseOpen,
    bool NamenIngevuld,
    bool HeeftLeden,
    bool BoekhoudingVereniging,
    bool BoekhoudingStichting,
    bool BankVereniging,
    bool IncassoIngesteld,
    bool TarievenIngevuld)
{
    /// <summary>De naam die een nieuwe database aan de Vereniging geeft.</summary>
    public const string Standaardnaam = "Vereniging";

    public static Inrichtingsstatus Leeg { get; } = new(false, false, false, false, false, false, false, false);

    public bool IsKlaar(Startstap stap) => stap switch
    {
        Startstap.Database => DatabaseOpen,
        Startstap.Administraties => NamenIngevuld,
        Startstap.Leden => HeeftLeden,
        Startstap.Boekhouding => BoekhoudingVereniging, // de Stichting is optioneel (besluit 61)
        Startstap.Bank => BankVereniging,
        Startstap.Contributie => IncassoIngesteld && TarievenIngevuld,
        _ => false,
    };

    /// <summary>De eerste stap met inhoud die nog niet klaar is, of <c>null</c> als alles klaar is.</summary>
    public Startstap? EersteOpenStap() =>
        Enum.GetValues<Startstap>().Where(s => s is not (Startstap.Welkom or Startstap.Klaar)).Cast<Startstap?>().FirstOrDefault(s => !IsKlaar(s!.Value));

    public static Inrichtingsstatus Bepaal(
        IReadOnlyList<Administratie> administraties, int aantalLeden, BoekhoudingStand vereniging, BoekhoudingStand stichting, IncassoStand contributie, int jaar)
    {
        ArgumentNullException.ThrowIfNull(administraties);
        ArgumentNullException.ThrowIfNull(vereniging);
        ArgumentNullException.ThrowIfNull(stichting);
        ArgumentNullException.ThrowIfNull(contributie);
        var naam = administraties.FirstOrDefault(a => a.Soort == AdministratieSoort.Vereniging)?.Naam;
        return new Inrichtingsstatus(
            DatabaseOpen: true,
            NamenIngevuld: naam is not null && !string.Equals(naam, Standaardnaam, StringComparison.Ordinal),
            HeeftLeden: aantalLeden > 0,
            BoekhoudingVereniging: IsIngericht(vereniging),
            BoekhoudingStichting: IsIngericht(stichting),
            BankVereniging: vereniging.Dagboeken.Any(d => d.Soort == DagboekSoort.Bank && d.Iban is not null),
            IncassoIngesteld: contributie.Instellingen is not null,
            TarievenIngevuld: contributie.Tarieven.Any(t => t.Boekjaar == jaar));
    }

    /// <summary>Zoals het tabblad Boekhouding: ook <i>Leeg beginnen</i> (alleen dagboeken) telt als ingericht.</summary>
    private static bool IsIngericht(BoekhoudingStand stand) => stand.IsIngericht || stand.Dagboeken.Count > 0;

    public static async Task<Inrichtingsstatus> BepaalAsync(IDatabaseSessie? sessie, int jaar, CancellationToken annulering = default)
    {
        if (sessie is null)
        {
            return Leeg;
        }

        var administraties = await sessie.Administraties.HaalAlleAsync(annulering).ConfigureAwait(false);
        var leden = await sessie.Leden.HaalAlleAsync(annulering).ConfigureAwait(false);
        var vereniging = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging, annulering).ConfigureAwait(false);
        var stichting = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Stichting, annulering).ConfigureAwait(false);
        var contributie = await sessie.Contributie.LaadAsync(annulering).ConfigureAwait(false);
        return Bepaal(administraties, leden.Count, vereniging, stichting, contributie, jaar);
    }
}
