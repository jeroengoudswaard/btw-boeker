using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Inrichting;
using BtwBoeker.Application.Tests.Boekhouding;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Application.Tests.Inrichting;

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
        var status = Inrichtingsstatus.Bepaal(Standaardnamen, Leeg(Ver), Leeg(Sti));

        Assert.True(status.IsKlaar(Startstap.Database));
        Assert.False(status.IsKlaar(Startstap.Administraties)); // nog de standaardnaam "Vereniging"
        Assert.False(status.IsKlaar(Startstap.Boekhouding));
        Assert.False(status.IsKlaar(Startstap.Bank));
        Assert.Equal(Startstap.Administraties, status.EersteOpenStap());
    }

    [Fact]
    public void Ingerichte_administratie_heeft_alle_stappen_klaar()
    {
        var namen = new List<Administratie> { new(Ver, "Scouting Voorbeeld", Administratie.Standaardkleur(Ver)), Standaardnamen[1] };
        var boekhouding = new BoekhoudingTestdata();
        boekhouding.Bankdagboek("BNK", "NL91ABNA0417164300");

        var status = Inrichtingsstatus.Bepaal(namen, boekhouding.Stand, Leeg(Sti));

        Assert.True(status.IsKlaar(Startstap.Administraties));
        Assert.True(status.IsKlaar(Startstap.Boekhouding));
        Assert.False(status.BoekhoudingStichting); // optioneel (besluit 209)
        Assert.False(status.BankStichting);
        Assert.True(status.IsKlaar(Startstap.Bank));
        Assert.Null(status.EersteOpenStap());
    }

    [Fact]
    public void Een_administratie_zonder_bankdagboek_is_niet_ingesteld()
    {
        var boekhouding = new BoekhoudingTestdata();

        var status = Inrichtingsstatus.Bepaal(Standaardnamen, boekhouding.Stand, Leeg(Sti));

        Assert.False(status.IsKlaar(Startstap.Bank));
    }
}
