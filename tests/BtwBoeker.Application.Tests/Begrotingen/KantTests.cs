using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Tests.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Begrotingen;

/// <summary>Baten of lasten per resultaatrekening (besluit 152).</summary>
public sealed class KantTests
{
    [Theory]
    [InlineData(0, 50000, ResultaatKant.Baten)]   // credit in de W&V: baten
    [InlineData(30000, 0, ResultaatKant.Lasten)]  // debet: lasten
    public void Overname_uit_excel_stelt_de_kant_voor_uit_het_saldo_van_vorig_jaar(long debet, long credit, ResultaatKant verwacht)
    {
        var rekening = new VoorbladRekening(4900, "Verhuur", null, RekeningSoort.Resultaat, RekeningRol.Geen, false, Bedrag.Nul, Bedrag.Nul)
        {
            WenvDebet = new Bedrag(debet),
            WenvCredit = new Bedrag(credit),
        };

        Assert.Equal(verwacht, rekening.VoorgesteldeKant);
    }

    [Fact]
    public void Zonder_saldo_of_bij_een_balansrekening_geen_voorstel()
    {
        Assert.Null(new VoorbladRekening(4900, "X", null, RekeningSoort.Resultaat, RekeningRol.Geen, false, Bedrag.Nul, Bedrag.Nul).VoorgesteldeKant);
        Assert.Null(new VoorbladRekening(1100, "Bank", null, RekeningSoort.Balans, RekeningRol.Geen, false, Bedrag.Nul, Bedrag.Nul) { WenvCredit = new Bedrag(1) }.VoorgesteldeKant);
    }

    [Fact]
    public async Task Rekeningschema_slaat_de_gekozen_kant_op()
    {
        var data = new BoekhoudingTestdata();
        var dienst = new BoekhoudingDienst(new BoekhoudingDienstTests.FakeSessie(data), new BoekhoudingDienstTests.VasteTijd(new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero)));

        var id = await dienst.BewaarRekeningAsync(AdministratieSoort.Vereniging,
            new RekeningInvoer(null, 4900, "Verhuur materiaal", null, RekeningSoort.Resultaat, RekeningRol.Geen, true, ResultaatKant.Baten));

        Assert.Equal(ResultaatKant.Baten, data.Rekeningen.Single(r => r.Id == id).Kant);
    }

    [Fact]
    public void Sjabloonbestand_bewaart_de_kant_en_een_oud_sjabloon_zonder_kant_blijft_leesbaar()
    {
        var data = new BoekhoudingTestdata();
        data.Energie.WijzigKant(ResultaatKant.Baten); // bewust afwijkend van de standaard

        var json = Rekeningsjablonen.Schrijf(Rekeningsjablonen.UitStand(data.Stand, "Test"));
        var terug = Rekeningsjablonen.Lees(json);

        Assert.Equal(ResultaatKant.Baten, terug.Rekeningen.Single(r => r.Nummer == 4115).Kant);
        Assert.Null(terug.Rekeningen.Single(r => r.Nummer == 1100).Kant);
        // De ingebouwde sjablonen hebben geen kant: dan geldt de standaard (vanaf 8000 baten).
        Assert.All(Rekeningsjablonen.Standaard(AdministratieSoort.Vereniging).Rekeningen, r => Assert.Null(r.Kant));
    }
}
