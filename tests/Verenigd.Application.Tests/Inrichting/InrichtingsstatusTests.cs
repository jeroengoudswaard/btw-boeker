using Verenigd.Application.Boekhouding;
using Verenigd.Application.Incasso;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Tests.Boekhouding;
using Verenigd.Application.Tests.Incasso;
using Verenigd.Domain.Administraties;

namespace Verenigd.Application.Tests.Inrichting;

/// <summary>Besluiten 170 en 175: het ✓ per stap van de startwizard volgt uit de gegevens zelf.</summary>
public sealed class InrichtingsstatusTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private const AdministratieSoort Sti = AdministratieSoort.Stichting;

    private static readonly IReadOnlyList<Administratie> Standaardnamen =
    [
        new(Ver, "Vereniging", Administratie.Standaardkleur(Ver)),
        new(Sti, "Stichting", Administratie.Standaardkleur(Sti)),
    ];

    private static readonly IncassoStand LeegIncasso = new([], [], [], [], [], [], [], null);

    private static BoekhoudingStand Leeg(AdministratieSoort soort) => new(soort, [], [], [], [], []);

    [Fact]
    public void Zonder_database_is_niets_klaar()
    {
        var status = Inrichtingsstatus.Leeg;

        Assert.All(Enum.GetValues<Startstap>().Where(s => s is not (Startstap.Welkom or Startstap.Klaar)), s => Assert.False(status.IsKlaar(s)));
        Assert.Equal(Startstap.Database, status.EersteOpenStap());
    }

    [Fact]
    public void Een_nieuwe_database_heeft_alleen_de_database_klaar()
    {
        var status = Inrichtingsstatus.Bepaal(Standaardnamen, aantalLeden: 0, Leeg(Ver), Leeg(Sti), LeegIncasso, 2026);

        Assert.True(status.IsKlaar(Startstap.Database));
        Assert.False(status.IsKlaar(Startstap.Administraties)); // nog de standaardnaam "Vereniging"
        Assert.False(status.IsKlaar(Startstap.Leden));
        Assert.False(status.IsKlaar(Startstap.Boekhouding));
        Assert.False(status.IsKlaar(Startstap.Bank));
        Assert.False(status.IsKlaar(Startstap.Contributie));
        Assert.Equal(Startstap.Administraties, status.EersteOpenStap());
    }

    [Fact]
    public void Ingerichte_administratie_heeft_alle_stappen_klaar()
    {
        var namen = new List<Administratie> { new(Ver, "Scouting Voorbeeld", Administratie.Standaardkleur(Ver)), Standaardnamen[1] };
        var boekhouding = new BoekhoudingTestdata();
        boekhouding.Bankdagboek("BNK", "NL91ABNA0417164300");
        var incasso = new IncassoTestdata(); // incassant-ID en tarieven 2026 en 2027

        var status = Inrichtingsstatus.Bepaal(namen, aantalLeden: 3, boekhouding.Stand, Leeg(Sti), incasso.Stand, 2026);

        Assert.True(status.IsKlaar(Startstap.Administraties));
        Assert.True(status.IsKlaar(Startstap.Leden));
        Assert.True(status.IsKlaar(Startstap.Boekhouding));
        Assert.False(status.BoekhoudingStichting); // optioneel (besluit 209)
        Assert.False(status.BankStichting);
        Assert.True(status.IsKlaar(Startstap.Bank));
        Assert.True(status.IsKlaar(Startstap.Contributie));
        Assert.Null(status.EersteOpenStap());
    }

    [Fact]
    public void Een_bankdagboek_zonder_iban_of_tarieven_van_een_ander_jaar_tellen_niet()
    {
        var boekhouding = new BoekhoudingTestdata(); // alleen memoriaal en kas
        var incasso = new IncassoTestdata(); // tarieven alleen voor 2026 en 2027

        var status = Inrichtingsstatus.Bepaal(Standaardnamen, 0, boekhouding.Stand, Leeg(Sti), incasso.Stand, 2028);

        Assert.False(status.IsKlaar(Startstap.Bank));
        Assert.True(status.IncassoIngesteld);
        Assert.False(status.TarievenIngevuld);
        Assert.False(status.IsKlaar(Startstap.Contributie));
    }
}
