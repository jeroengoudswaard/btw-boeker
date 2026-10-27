using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Financien.Debiteuren;
using Verenigd.Domain.Gedeeld;

namespace Verenigd.Domain.Tests.Financien.Debiteuren;

public class ContributieboekingTests
{
    private static readonly Iban Iban1 = Iban.Maak("NL91ABNA0417164300");

    private static Incassorun Run() => new(new Maand(2026, 11), new DateOnly(2026, 11, 3), new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Herincassopost_verwijst_naar_de_oorspronkelijke_post_en_markeert_de_regels()
    {
        var run = Run();
        var gewoon = run.VoegPostToe(Guid.NewGuid(), Iban1, "Fam. Test", "M-1", new DateOnly(2025, 1, 1), "Contributie");
        gewoon.VoegRegelToe(Lidnummer.Parse("100001"), new Maand(2026, 11), "Jeugdlid Welpen", new Bedrag(1250));
        var her = run.VoegPostToe(Guid.NewGuid(), Iban1, "Fam. Test", "M-1", new DateOnly(2025, 1, 1), "Contributie (herincasso)", herincassoVan: "VERABC-0001");
        her.VoegRegelToe(Lidnummer.Parse("100001"), new Maand(2026, 10), "Jeugdlid Welpen", new Bedrag(1250));

        Assert.Null(gewoon.HerincassoVan);
        Assert.False(gewoon.Regels.Single().IsHerincasso);
        Assert.Equal("VERABC-0001", her.HerincassoVan);
        Assert.True(her.IsHerincasso);
        Assert.True(her.Regels.Single().IsHerincasso);
        Assert.Equal(her.HerincassoVan, her.Vordering);
        Assert.Equal(gewoon.EndToEndId, gewoon.Vordering);
    }

    [Fact]
    public void Categorie_krijgt_een_opbrengstrekening()
    {
        var categorie = new Contributiecategorie("Jeugdlid Welpen", CategorieSoort.Jeugdlid, "Welpen");
        Assert.Null(categorie.OpbrengstrekeningId);

        categorie.KoppelOpbrengstrekening(7);
        Assert.Equal(7, categorie.OpbrengstrekeningId);
        categorie.KoppelOpbrengstrekening(null);
        Assert.Null(categorie.OpbrengstrekeningId);
    }

    [Fact]
    public void Bankmutatie_bewaart_batchkenmerk_en_retourreden_en_kan_worden_verrijkt()
    {
        var bank = new Grootboekrekening(AdministratieSoort.Vereniging, 1100, "Bank", RekeningSoort.Balans, null);
        var dagboek = new Dagboek(AdministratieSoort.Vereniging, DagboekSoort.Bank, "RABO", "Rabobank", bank, Iban1);
        var mutatie = new Bankmutatie(AdministratieSoort.Vereniging, dagboek, "k", new DateOnly(2026, 11, 3), new Bedrag(500), false,
            default, null, null, null, "E2E", "M-1", null);
        Assert.Null(mutatie.BatchKenmerk);

        mutatie.Verrijk("RUN-1", "AM04");
        Assert.Equal("RUN-1", mutatie.BatchKenmerk);
        Assert.Equal("AM04", mutatie.Retourreden);

        mutatie.Verrijk(null, "MD06");
        Assert.Equal("RUN-1", mutatie.BatchKenmerk); // bestaande waarden blijven staan
        Assert.Equal("AM04", mutatie.Retourreden);
    }

    [Theory]
    [InlineData("AM04", "Onvoldoende saldo")]
    [InlineData("MD06", "Terugboeking door de betaler")]
    [InlineData("MS02", "Geweigerd door de betaler")]
    [InlineData("AC04", "Rekening opgeheven")]
    [InlineData("XX99", "Onbekende reden")]
    public void Retourreden_heeft_een_leesbare_omschrijving(string code, string omschrijving)
    {
        Assert.Equal(omschrijving, SepaRetourreden.Omschrijving(code));
        Assert.Equal($"{omschrijving} ({code})", SepaRetourreden.Weergave(code));
    }

    [Fact]
    public void Debiteurmutatie_vereist_een_vordering_en_een_bedrag()
    {
        var post = Guid.NewGuid();
        var m = new Debiteurmutatie(AdministratieSoort.Vereniging, "VERABC-0001", Guid.NewGuid(), null, DebiteurmutatieSoort.Stornering,
            new DateOnly(2026, 11, 10), 1250, post, "AM04");
        Assert.Equal(1250, m.Saldo);
        Assert.Equal(post, m.PostId);

        Assert.Throws<ArgumentException>(() => new Debiteurmutatie(AdministratieSoort.Vereniging, " ", Guid.NewGuid(), null,
            DebiteurmutatieSoort.Ontvangst, new DateOnly(2026, 11, 10), -1250, post, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Debiteurmutatie(AdministratieSoort.Vereniging, "X", Guid.NewGuid(), null,
            DebiteurmutatieSoort.Ontvangst, new DateOnly(2026, 11, 10), 0, post, null));
    }

    [Fact]
    public void Herincasso_wordt_in_een_run_verwerkt_en_komt_vrij_bij_annuleren()
    {
        var verzoek = new Herincasso("VERABC-0001", new DateTime(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc));
        Assert.True(verzoek.IsOpen);

        var run = Guid.NewGuid();
        verzoek.Verwerk(run);
        Assert.False(verzoek.IsOpen);
        Assert.Equal(run, verzoek.RunId);
        Assert.Throws<InvalidOperationException>(() => verzoek.Verwerk(Guid.NewGuid()));

        verzoek.GeefVrij();
        Assert.True(verzoek.IsOpen);
    }
}
