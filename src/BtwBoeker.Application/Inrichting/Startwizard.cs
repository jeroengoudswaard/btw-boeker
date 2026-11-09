using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Inrichting;

public enum Startstap
{
    Welkom,
    Database,
    Administraties,
    Boekhouding,
    Bank,
    Klaar,
}

public sealed record Startwizardvoortgang(Startstap? Stap, bool IsAfgerond)
{
    public static Startwizardvoortgang Nieuw { get; } = new(null, false);

    public bool KanHervatten => !IsAfgerond && Stap is not null;
}

public interface IStartwizardOpslag
{
    Task<Startwizardvoortgang> LaadAsync(CancellationToken annulering = default);

    Task BewaarAsync(Startwizardvoortgang voortgang, CancellationToken annulering = default);
}

public sealed record Inrichtingsstatus(
    bool DatabaseOpen,
    bool NamenIngevuld,
    bool BoekhoudingVereniging,
    bool BoekhoudingStichting,
    bool BankVereniging,
    bool BankStichting = false)
{
    public const string Standaardnaam = "Vereniging";

    public static Inrichtingsstatus Leeg { get; } = new(false, false, false, false, false);

    public bool IsKlaar(Startstap stap) => stap switch
    {
        Startstap.Database => DatabaseOpen,
        Startstap.Administraties => NamenIngevuld,
        Startstap.Boekhouding => BoekhoudingVereniging,
        Startstap.Bank => BankVereniging,
        _ => false,
    };

    public Startstap? EersteOpenStap() =>
        Enum.GetValues<Startstap>().Where(s => s is not (Startstap.Welkom or Startstap.Klaar))
            .Cast<Startstap?>().FirstOrDefault(s => !IsKlaar(s!.Value));

    public static Inrichtingsstatus Bepaal(
        IReadOnlyList<Administratie> administraties, BoekhoudingStand vereniging, BoekhoudingStand stichting)
    {
        ArgumentNullException.ThrowIfNull(administraties);
        ArgumentNullException.ThrowIfNull(vereniging);
        ArgumentNullException.ThrowIfNull(stichting);
        var naam = administraties.FirstOrDefault(a => a.Soort == AdministratieSoort.Vereniging)?.Naam;
        return new Inrichtingsstatus(
            DatabaseOpen: true,
            NamenIngevuld: naam is not null && !string.Equals(naam, Standaardnaam, StringComparison.Ordinal),
            BoekhoudingVereniging: IsIngericht(vereniging),
            BoekhoudingStichting: IsIngericht(stichting),
            BankVereniging: HeeftBank(vereniging),
            BankStichting: HeeftBank(stichting));
    }

    private static bool IsIngericht(BoekhoudingStand stand) => stand.IsIngericht || stand.Dagboeken.Count > 0;

    private static bool HeeftBank(BoekhoudingStand stand) => stand.Dagboeken.Any(d => d.Soort == DagboekSoort.Bank && d.Iban is not null);

    public static async Task<Inrichtingsstatus> BepaalAsync(IDatabaseSessie? sessie, CancellationToken annulering = default)
    {
        if (sessie is null)
        {
            return Leeg;
        }

        var administraties = await sessie.Administraties.HaalAlleAsync(annulering).ConfigureAwait(false);
        var vereniging = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging, annulering).ConfigureAwait(false);
        var stichting = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Stichting, annulering).ConfigureAwait(false);
        return Bepaal(administraties, vereniging, stichting);
    }
}
