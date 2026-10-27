using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Domain.Tests.Financien.Bank;

public class BankTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private static readonly Grootboekrekening Bank = new(Ver, 1100, "Rabobank", RekeningSoort.Balans, null);
    private static readonly Grootboekrekening Energie = new(Ver, 4115, "Energie", RekeningSoort.Resultaat, null);
    private static readonly Dagboek Rabo = new(Ver, DagboekSoort.Bank, "RABO", "Rabobank betaalrekening", Bank, Iban.Maak("NL91ABNA0417164300"));
    private static readonly BankTransactiecode Overboeking = new("PMNT", "ICDT", "ESCT", "520", "RABOBANK");
    private static readonly BankTransactiecode Rente = new(null, null, null, "1025", "RABOBANK");

    private static Bankmutatie Mutatie(long centen = 4500, bool bij = false, string? tegenIban = "NL20INGB0001234567", string? naam = "Energie BV",
        string? omschrijving = "Factuur 2026-09", BankTransactiecode? code = null, int? batch = null) =>
        new(Ver, Rabo, "sleutel-1", new DateOnly(2026, 9, 14), new Bedrag(centen), bij, code ?? Overboeking,
            tegenIban, naam, omschrijving, "E2E-1", null, batch);

    [Fact]
    public void Bankdagboek_heeft_iban_alleen_voor_soort_bank()
    {
        Assert.Equal("NL91ABNA0417164300", Rabo.Iban?.Waarde);
        Assert.Throws<ArgumentException>(() => new Dagboek(Ver, DagboekSoort.Memoriaal, "MEM", "Memoriaal", null, Iban.Maak("NL91ABNA0417164300")));
    }

    [Fact]
    public void Mutatie_maakt_conceptpost_met_vaste_bankregel()
    {
        var af = Mutatie();
        var post = af.MaakPost(Energie, "Energie september");

        Assert.Equal(PostStatus.Concept, post.Status);
        Assert.Same(Rabo, post.Dagboek);
        Assert.Equal(new DateOnly(2026, 9, 14), post.Datum);
        Assert.Equal("Energie september", post.Omschrijving);
        Assert.Equal(-4500, post.Regels.Single(r => ReferenceEquals(r.Rekening, Bank)).Saldo);
        Assert.Equal(4500, post.Regels.Single(r => ReferenceEquals(r.Rekening, Energie)).Saldo);
        Assert.Empty(post.Controleer());
        Assert.Null(af.ControleerBankregel(post));

        var bij = Mutatie(bij: true).MaakPost(Energie, null);
        Assert.Equal(4500, bij.Regels.Single(r => ReferenceEquals(r.Rekening, Bank)).Saldo);
        Assert.Equal("Energie BV · Factuur 2026-09", bij.Omschrijving);
    }

    [Fact]
    public void Gewijzigde_bankregel_wordt_gesignaleerd()
    {
        var af = Mutatie();
        var post = af.MaakPost(Energie, "x");
        post.Wijzig(Rabo, post.Datum, "x", [new RegelInvoer(Bank, null, Bedrag.Nul, new Bedrag(4000)), new RegelInvoer(Energie, null, new Bedrag(4000), Bedrag.Nul)]);

        Assert.Contains("€ 45,00", af.ControleerBankregel(post), StringComparison.Ordinal);
    }

    [Fact]
    public void Koppelen_en_ontkoppelen()
    {
        var af = Mutatie();
        var post = af.MaakPost(Energie, null);

        af.Koppel(post);
        Assert.Equal(post.Id, af.PostId);
        af.Ontkoppel();
        Assert.Null(af.PostId);
    }

    [Fact]
    public void Batch_zonder_tegenpartij_krijgt_leesbare_omschrijving()
    {
        var batch = Mutatie(105000, bij: true, tegenIban: null, naam: null, omschrijving: null, batch: 42);

        Assert.Equal("Incasso (42 posten)", batch.Standaardomschrijving);
        Assert.Equal(105000, batch.Saldo);
    }

    [Fact]
    public void Transactiecode_past_op_iso_voorvoegsel_en_eigen_code()
    {
        Assert.Equal("PMNT-ICDT-ESCT", Overboeking.Iso);
        Assert.True(Overboeking.Past("pmnt-icdt-esct"));
        Assert.True(Overboeking.Past("PMNT-ICDT"));
        Assert.False(Overboeking.Past("PMNT-RCDT"));
        Assert.True(Overboeking.Past("RABOBANK 520"));
        Assert.True(Rente.Past("Rabobank 1025"));
        Assert.Null(Rente.Iso);
        Assert.Equal(Rente, BankTransactiecode.Lees(Rente.Waarde));
        Assert.Equal(Overboeking, BankTransactiecode.Lees(Overboeking.Waarde));
        Assert.Equal("Verstuurde overboeking", Overboeking.Omschrijving);
        Assert.Equal("Rabobank 1025", Rente.Omschrijving);
    }

    [Fact]
    public void Boekingsregel_vereist_minstens_een_voorwaarde()
    {
        Assert.Throws<ArgumentException>(() => new Boekingsregel(Ver, "Leeg", Energie, new BoekingsregelVoorwaarden()));
    }

    [Theory]
    [InlineData("NL20 INGB 0001 2345 67", null, null, Richting.Beide, null, null, null, true)]
    [InlineData("NL20INGB0009999999", null, null, Richting.Beide, null, null, null, false)]
    [InlineData(null, "energie", null, Richting.Af, null, null, null, true)]
    [InlineData(null, "energie", null, Richting.Bij, null, null, null, false)]
    [InlineData(null, null, "FACTUUR", Richting.Beide, null, null, null, true)]
    [InlineData(null, null, null, Richting.Af, 4000L, 5000L, null, true)]
    [InlineData(null, null, null, Richting.Af, 5000L, null, null, false)]
    [InlineData(null, null, null, Richting.Beide, null, null, "PMNT-ICDT", true)]
    [InlineData(null, "Energie", null, Richting.Beide, null, null, "PMNT-RCDT", false)]
    public void Boekingsregel_past_alleen_als_alle_voorwaarden_kloppen(
        string? iban, string? naam, string? omschrijving, Richting richting, long? van, long? tot, string? code, bool verwacht)
    {
        var regel = new Boekingsregel(Ver, "Energie", Energie, new BoekingsregelVoorwaarden(
            iban, naam, omschrijving, richting, van is null ? null : new Bedrag(van.Value), tot is null ? null : new Bedrag(tot.Value), code));

        Assert.Equal(verwacht, regel.Past(Mutatie()));
    }

    [Fact]
    public void Inactieve_regel_past_nooit()
    {
        var regel = new Boekingsregel(Ver, "Energie", Energie, new BoekingsregelVoorwaarden(NaamBevat: "Energie"));
        regel.Deactiveer();

        Assert.False(regel.Past(Mutatie()));
    }

    [Fact]
    public void Regel_maken_van_een_boeking_gebruikt_tegen_iban_en_richting()
    {
        var voorstel = BoekingsregelVoorwaarden.Van(Mutatie());
        Assert.Equal("NL20INGB0001234567", voorstel.TegenIban);
        Assert.Equal(Richting.Af, voorstel.Richting);

        var rente = BoekingsregelVoorwaarden.Van(Mutatie(bij: true, tegenIban: null, naam: null, code: Rente));
        Assert.Null(rente.TegenIban);
        Assert.Equal("RABOBANK 1025", rente.Transactiecode);
    }
}
