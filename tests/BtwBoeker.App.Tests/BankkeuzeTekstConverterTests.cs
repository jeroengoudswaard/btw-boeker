using System.Globalization;
using BtwBoeker.App.Converters;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.Tests;

public sealed class BankkeuzeTekstConverterTests
{
    private static object Tekst(object? dagboek, object? aantallen) =>
        new BankkeuzeTekstConverter().Convert([dagboek, aantallen], typeof(string), null, CultureInfo.InvariantCulture);

    private static Dagboek Betaal() => new(AdministratieSoort.Vereniging, DagboekSoort.Memoriaal, "RABO", "Betaalrekening", null);

    [Fact]
    public void Met_aantal_tussen_haakjes_achter_de_naam()
    {
        var betaal = Betaal();
        Assert.Equal("Betaalrekening (228)", Tekst(betaal, new Dictionary<int, int> { [betaal.Id] = 228 }));
    }

    [Fact]
    public void Zonder_iets_te_verwerken_alleen_de_naam()
    {
        Assert.Equal("Betaalrekening", Tekst(Betaal(), new Dictionary<int, int>()));
    }

    [Fact]
    public void Zonder_aantallen_of_rekening_geen_fout()
    {
        Assert.Equal("Betaalrekening", Tekst(Betaal(), null));
        Assert.Equal(string.Empty, Tekst(null, null));
    }
}
