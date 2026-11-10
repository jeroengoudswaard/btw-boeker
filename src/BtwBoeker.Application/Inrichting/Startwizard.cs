using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Inrichting;

public enum Startstap
{
    Welkom,
    Database,
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

public sealed record Inrichtingsstatus(bool DatabaseOpen, bool BoekhoudingIngericht, bool BankGekoppeld)
{
    public static Inrichtingsstatus Leeg { get; } = new(false, false, false);

    public bool IsKlaar(Startstap stap) => stap switch
    {
        Startstap.Database => DatabaseOpen,
        Startstap.Boekhouding => BoekhoudingIngericht,
        Startstap.Bank => BankGekoppeld,
        _ => false,
    };

    public Startstap? EersteOpenStap() =>
        Enum.GetValues<Startstap>().Where(s => s is not (Startstap.Welkom or Startstap.Klaar))
            .Cast<Startstap?>().FirstOrDefault(s => !IsKlaar(s!.Value));

    public static Inrichtingsstatus Bepaal(BoekhoudingStand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        return new Inrichtingsstatus(DatabaseOpen: true, BoekhoudingIngericht: IsIngericht(stand), BankGekoppeld: HeeftBank(stand));
    }

    private static bool IsIngericht(BoekhoudingStand stand) => stand.IsIngericht || stand.Dagboeken.Count > 0;

    private static bool HeeftBank(BoekhoudingStand stand) => stand.Dagboeken.Any(d => d.Soort == DagboekSoort.Bank && d.Iban is not null);

    public static async Task<Inrichtingsstatus> BepaalAsync(IDatabaseSessie? sessie, CancellationToken annulering = default)
    {
        if (sessie is null)
        {
            return Leeg;
        }

        return Bepaal(await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging, annulering).ConfigureAwait(false));
    }
}
